using System.Globalization;
using System.Text;
using ColonnadeGrid.Models;
using NpgsqlTypes;

namespace ColonnadeGrid.LargeData.Api.Data;

public sealed record SqlParameterSpec(string Name, object Value, NpgsqlDbType Type);

public sealed record SqlStatement(string Text, IReadOnlyList<SqlParameterSpec> Parameters);

/// <summary>The SQL needed to answer one <see cref="DataRequest"/>.</summary>
/// <param name="Count">Returns one row: the number of issues matching the filters.</param>
/// <param name="Page">
/// Returns the page's rows. When grouped, each row has a trailing
/// <c>group_key</c> text column, and rows are ordered by the group column first.
/// </param>
/// <param name="GroupTotals">When grouped, returns (group_key, count) for every group matching the filters; otherwise <c>null</c>.</param>
public sealed record IssueQuery(SqlStatement Count, SqlStatement Page, SqlStatement? GroupTotals);

/// <summary>
/// Translates the grid's <see cref="DataRequest"/> into parameterized SQL.
/// <para>
/// Identifiers come only from <see cref="IssueColumns"/> and every value is a
/// bound parameter, so nothing from the request is concatenated into SQL.
/// </para>
/// <para>
/// Filter semantics mirror <c>InMemoryDataProvider</c> so the grid behaves
/// the same against either source — including its null handling, where a
/// null value compares as less than anything (so <c>NotEquals</c> and
/// <c>LessThan</c> match nulls, and ascending sorts put nulls first).
/// </para>
/// <para>
/// When grouped, rows are ordered by the group column first, so each group is
/// contiguous across pages as the grid's paging contract requires. That
/// orders groups by key, where <c>InMemoryDataProvider</c> orders them by
/// first appearance.
/// </para>
/// </summary>
public static class IssueQueryBuilder
{
    public const int MaxTake = 10_000;

    private const string SelectColumns =
        "id, title, status::text, priority::text, project, assignee, story_points, created_at, due_date, " +
        "estimate_hours, time_spent, cycle_time";

    /// <summary>The number of columns in <see cref="SelectColumns"/>, which is also the ordinal of a grouped page's group_key.</summary>
    public const int SelectColumnCount = 12;

    public static IssueQuery Build(DataRequest request)
    {
        if (request.Skip < 0)
        {
            throw new InvalidQueryException("Skip cannot be negative.");
        }

        var take = Math.Clamp(request.Take, 1, MaxTake);
        var groupColumn = request.GroupByPropertyName is { } groupBy ? IssueColumns.Resolve(groupBy) : null;

        var filterParameters = new List<SqlParameterSpec>();
        var where = BuildWhere(request.Filters, filterParameters);

        var count = new SqlStatement($"SELECT count(*) FROM issues{where}", filterParameters);

        var pageSql = new StringBuilder("SELECT ").Append(SelectColumns);
        if (groupColumn is not null)
        {
            pageSql.Append(", ").Append(groupColumn.Sql).Append("::text AS group_key");
        }

        pageSql.Append(" FROM issues").Append(where)
            .Append(" ORDER BY ").Append(BuildOrderTerms(request.Sort, groupColumn))
            .Append(" LIMIT @take OFFSET @skip");

        var pageParameters = new List<SqlParameterSpec>(filterParameters)
        {
            new("take", take, NpgsqlDbType.Integer),
            new("skip", request.Skip, NpgsqlDbType.Integer)
        };

        var groupTotals = groupColumn is null
            ? null
            : new SqlStatement(
                $"SELECT {groupColumn.Sql}::text, count(*) FROM issues{where} GROUP BY {groupColumn.Sql}",
                filterParameters);

        return new IssueQuery(count, new SqlStatement(pageSql.ToString(), pageParameters), groupTotals);
    }

    public const int MaxGroupsPerRequest = 500;
    public const int MaxGroupPagesPerRequest = 100;

    /// <summary>
    /// SQL for one batch of groups with their row counts, ordered by key (in the
    /// sort's direction when sorting by the grouped column). Each row is
    /// (group_key, row_count, group_count, total_rows): the last two are window
    /// totals over every group, computed before LIMIT, so one query also answers
    /// "how many more groups are there". <c>Totals</c> returns the same two totals
    /// for a batch past the last group, which has no rows to carry them.
    /// </summary>
    public static (SqlStatement Page, SqlStatement Totals) BuildGroupList(GroupListRequest request)
    {
        if (request.Skip < 0)
        {
            throw new InvalidQueryException("Skip cannot be negative.");
        }

        var groupColumn = IssueColumns.Resolve(request.GroupByPropertyName);
        var col = $"issues.{groupColumn.Sql}";
        var parameters = new List<SqlParameterSpec>();
        var where = BuildWhere(request.Filters, parameters);

        var totals = new SqlStatement(
            $"SELECT (SELECT count(*) FROM (SELECT 1 FROM issues{where} GROUP BY {col}) g), (SELECT count(*) FROM issues{where})",
            parameters);

        var pageParameters = new List<SqlParameterSpec>(parameters);
        var take = AddParameter(pageParameters, Math.Clamp(request.Take, 1, MaxGroupsPerRequest), NpgsqlDbType.Integer);
        var skip = AddParameter(pageParameters, request.Skip, NpgsqlDbType.Integer);
        var page = new SqlStatement(
            $"SELECT {col}::text, count(*), count(*) OVER (), (sum(count(*)) OVER ())::bigint FROM issues{where} " +
            $"GROUP BY {col} ORDER BY {OrderTerm(groupColumn, GroupDirection(request.Sort, groupColumn))} " +
            $"LIMIT {take} OFFSET {skip}",
            pageParameters);

        return (page, totals);
    }

    /// <summary>
    /// SQL for a page of rows from each of several groups. <c>Rows</c> is one
    /// UNION ALL query with a branch per group — each an index range scan on
    /// the group column — whose rows carry (page_index, row_index) so they can be
    /// split back out in order. <c>Counts</c> returns (group_key, row_count) for
    /// each requested group that has rows.
    /// </summary>
    public static (SqlStatement Rows, SqlStatement Counts) BuildGroupPages(GroupPagesRequest request)
    {
        if (request.Pages.Count is 0 or > MaxGroupPagesPerRequest)
        {
            throw new InvalidQueryException($"Request between 1 and {MaxGroupPagesPerRequest} group pages at a time.");
        }

        var groupColumn = IssueColumns.Resolve(request.GroupByPropertyName);
        var parameters = new List<SqlParameterSpec>();
        var where = BuildWhere(request.Filters, parameters);
        var conditions = request.Pages.Select(page => BuildGroupCondition(groupColumn, page.GroupKey, parameters)).ToList();

        // One GROUP BY over the requested groups' rows. (A count(*) FILTER per
        // group took ~98 ms vs ~13 ms at 100 groups: every filter runs on every row.)
        var col = $"issues.{groupColumn.Sql}";
        var counts = new SqlStatement(
            $"SELECT {col}::text, count(*) FROM issues{AndWhere(where, $"({string.Join(" OR ", conditions)})")} GROUP BY {col}",
            parameters);

        var rowParameters = new List<SqlParameterSpec>(parameters);
        var orderTerms = BuildOrderTerms(request.Sort, groupColumn: null);
        var branches = new List<string>();
        for (var i = 0; i < request.Pages.Count; i++)
        {
            var page = request.Pages[i];
            if (page.Skip < 0)
            {
                throw new InvalidQueryException("Skip cannot be negative.");
            }

            var take = AddParameter(rowParameters, Math.Clamp(page.Take, 0, MaxTake), NpgsqlDbType.Integer);
            var skip = AddParameter(rowParameters, page.Skip, NpgsqlDbType.Integer);
            branches.Add(
                $"(SELECT {i} AS page_index, row_number() OVER (ORDER BY {orderTerms}) AS row_index, {SelectColumns} " +
                $"FROM issues{AndWhere(where, conditions[i])} ORDER BY {orderTerms} LIMIT {take} OFFSET {skip})");
        }

        // Wrapped in a subquery: with a single branch, a trailing ORDER BY would
        // attach to that branch's own SELECT, which already has one.
        var rows = new SqlStatement(
            $"SELECT * FROM ({string.Join(" UNION ALL ", branches)}) AS pages ORDER BY page_index, row_index",
            rowParameters);
        return (rows, counts);
    }

    /// <summary>
    /// The condition selecting one group's rows. Keys are what <see cref="BuildGroupList"/>
    /// returns: the column's text exactly as Postgres prints it, or
    /// <see cref="GroupKeys.Null"/> for null. Any other key — including another
    /// spelling of a real value, like "todo" or "05" — matches nothing, so a
    /// group's rows and its count (matched by that text) always agree.
    /// </summary>
    private static string BuildGroupCondition(IssueColumn column, string key, List<SqlParameterSpec> parameters)
    {
        if (key == GroupKeys.Null)
        {
            return $"issues.{column.Sql} IS NULL";
        }

        var canonicalKey = column.Kind switch
        {
            ColumnKind.Text => key,
            ColumnKind.Integer when int.TryParse(key, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
                => number.ToString(CultureInfo.InvariantCulture),
            ColumnKind.Enum when Enum.TryParse(column.ClrEnumType!, key, ignoreCase: false, out var parsed)
                                 && Enum.IsDefined(column.ClrEnumType!, parsed)
                => parsed.ToString(),
            // Dates aren't groupable in this sample.
            _ => null
        };

        // Also rules out NUL characters, which Postgres text can't hold.
        if (canonicalKey != key || key.Contains('\0'))
        {
            return "FALSE";
        }

        return TryBuildTypedValue(column, key, parameters) is { } value
            ? $"issues.{column.Sql} = {value}"
            : "FALSE";
    }

    /// <summary>The most distinct values a stats request can ask for.</summary>
    public const int MaxStatsValues = 1_000;

    /// <summary>
    /// SQL for a column's filter-editor stats, over the rows matching the other
    /// columns' filters. <c>Summary</c> returns one row: (min, max, empty count,
    /// total count). <c>Values</c>, when value counts are requested, returns
    /// (value, count) for up to one more than the requested number of distinct
    /// non-empty values, in the column's sort order — the extra row shows the list
    /// was cut off.
    /// </summary>
    public static (SqlStatement Summary, SqlStatement? Values) BuildColumnStats(ColumnStatsRequest request)
    {
        var column = IssueColumns.Resolve(request.PropertyName);
        var col = $"issues.{column.Sql}";
        var empty = column.Kind == ColumnKind.Text ? $"({col} IS NULL OR {col} = '')" : $"{col} IS NULL";

        var parameters = new List<SqlParameterSpec>();
        var otherFilters = request.Filters.Where(f => f.PropertyName != request.PropertyName).ToList();
        var where = BuildWhere(otherFilters, parameters);

        // min()/max() run on the typed column, so enums sort in declaration order;
        // enums and text are then read back as text, everything else as its .NET type.
        var readsAsText = column.Kind is ColumnKind.Enum or ColumnKind.Text;
        var minMax = readsAsText ? $"min({col})::text, max({col})::text" : $"min({col}), max({col})";
        var summary = new SqlStatement(
            $"SELECT {minMax}, count(*) FILTER (WHERE {empty}), count(*) FROM issues{where}",
            parameters);

        if (!request.IncludeValueCounts)
        {
            return (summary, null);
        }

        var valueParameters = new List<SqlParameterSpec>(parameters);
        var limit = AddParameter(valueParameters, Math.Clamp(request.MaxValueCount, 0, MaxStatsValues) + 1, NpgsqlDbType.Integer);
        var values = new SqlStatement(
            $"SELECT {(readsAsText ? $"{col}::text" : col)}, count(*) FROM issues{AndWhere(where, $"NOT {empty}")} " +
            $"GROUP BY {col} ORDER BY {col} ASC LIMIT {limit}",
            valueParameters);
        return (summary, values);
    }

    private static string WithEmpty(IssueColumn column, string condition, bool includeEmpty) =>
        !includeEmpty ? condition
        : column.Kind == ColumnKind.Text ? $"({column.Sql} IS NULL OR {column.Sql} = '' OR {condition})"
        : $"({column.Sql} IS NULL OR {condition})";

    private static string RequireTypedValue(IssueColumn column, FilterDescriptor filter, string value, List<SqlParameterSpec> parameters) =>
        TryBuildTypedValue(column, value, parameters)
        ?? throw new InvalidQueryException($"'{value}' isn't a valid value for '{filter.PropertyName}'.");

    private static SortDirection GroupDirection(SortDescriptor? sort, IssueColumn groupColumn) =>
        sort is { Direction: not SortDirection.None } && IssueColumns.Resolve(sort.PropertyName) == groupColumn
            ? sort.Direction
            : SortDirection.Ascending;

    private static string AndWhere(string where, string condition) =>
        where.Length == 0 ? $" WHERE {condition}" : $"{where} AND {condition}";

    private static string BuildWhere(IReadOnlyList<FilterDescriptor> filters, List<SqlParameterSpec> parameters)
    {
        if (filters.Count == 0)
        {
            return "";
        }

        return " WHERE " + string.Join(" AND ", filters.Select(f => BuildFilter(f, parameters)));
    }

    private static string BuildFilter(FilterDescriptor filter, List<SqlParameterSpec> parameters)
    {
        var column = IssueColumns.Resolve(filter.PropertyName);
        var col = column.Sql;
        // Text columns are matched directly so the trigram/btree indexes can
        // be used; everything else is matched on its text representation.
        var colText = column.Kind == ColumnKind.Text ? col : $"{col}::text";

        switch (filter.Operator)
        {
            case FilterOperator.IsEmpty:
                return column.Kind == ColumnKind.Text ? $"({col} IS NULL OR {col} = '')" : $"{col} IS NULL";
            case FilterOperator.IsNotEmpty:
                return column.Kind == ColumnKind.Text ? $"({col} IS NOT NULL AND {col} <> '')" : $"{col} IS NOT NULL";

            case FilterOperator.In:
            {
                // A value that can't be a value of the column can't match, so it's dropped.
                var values = (filter.Values ?? [])
                    .Select(value => TryBuildTypedValue(column, value, parameters))
                    .OfType<string>()
                    .ToList();
                return WithEmpty(column, values.Count == 0 ? "FALSE" : $"{col} IN ({string.Join(", ", values)})", filter.IncludeEmpty);
            }

            case FilterOperator.Between:
            {
                var bounds = new List<string>();
                if (!string.IsNullOrEmpty(filter.Value))
                {
                    bounds.Add($"{col} >= {RequireTypedValue(column, filter, filter.Value, parameters)}");
                }

                if (!string.IsNullOrEmpty(filter.ValueTo))
                {
                    bounds.Add($"{col} <= {RequireTypedValue(column, filter, filter.ValueTo, parameters)}");
                }

                var range = bounds.Count == 0 ? $"{col} IS NOT NULL" : $"({string.Join(" AND ", bounds)})";
                return WithEmpty(column, range, filter.IncludeEmpty);
            }

            case FilterOperator.WithinLast:
            {
                if (column.Kind is not (ColumnKind.Timestamp or ColumnKind.Date))
                {
                    throw new InvalidQueryException($"'{filter.PropertyName}' isn't a date, so it can't be filtered by a relative period.");
                }

                // "Now" is the API's clock, taken per query, so a saved "last 30 days" stays relative.
                var now = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
                if (!RelativeDatePeriod.TryGetStart(filter.Value, now, out var start))
                {
                    throw new InvalidQueryException($"'{filter.Value}' isn't a period like P30D.");
                }

                var period = $"({col} >= {AddParameter(parameters, start, NpgsqlDbType.Timestamp)} " +
                             $"AND {col} <= {AddParameter(parameters, now, NpgsqlDbType.Timestamp)})";
                return WithEmpty(column, period, filter.IncludeEmpty);
            }
        }

        if (filter.Value is null)
        {
            throw new InvalidQueryException($"The {filter.Operator} filter on '{filter.PropertyName}' requires a value.");
        }

        switch (filter.Operator)
        {
            case FilterOperator.Contains:
                return $"{colText} ILIKE '%' || {AddParameter(parameters, EscapeLike(filter.Value), NpgsqlDbType.Text)} || '%'";
            case FilterOperator.StartsWith:
                return $"{colText} ILIKE {AddParameter(parameters, EscapeLike(filter.Value), NpgsqlDbType.Text)} || '%'";
        }

        var op = filter.Operator switch
        {
            FilterOperator.Equals => "=",
            FilterOperator.NotEquals => "<>",
            FilterOperator.GreaterThan => ">",
            FilterOperator.LessThan => "<",
            _ => throw new InvalidQueryException($"Unsupported filter operator '{filter.Operator}'.")
        };

        string comparison;
        if (TryBuildTypedValue(column, filter.Value, parameters) is { } typedValue)
        {
            comparison = $"{col} {op} {typedValue}";
        }
        else
        {
            // Same fallback as InMemoryDataProvider: a value that doesn't
            // convert to the column's type is compared as case-insensitive text.
            comparison = $"lower({colText}) {op} lower({AddParameter(parameters, filter.Value, NpgsqlDbType.Text)})";
        }

        return filter.Operator is FilterOperator.NotEquals or FilterOperator.LessThan
            ? $"({col} IS NULL OR {comparison})"
            : comparison;
    }

    /// <summary>Adds <paramref name="value"/> converted to the column's type, returning the SQL placeholder, or <c>null</c> if it doesn't convert.</summary>
    private static string? TryBuildTypedValue(IssueColumn column, string value, List<SqlParameterSpec> parameters)
    {
        switch (column.Kind)
        {
            case ColumnKind.Text:
                return AddParameter(parameters, value, NpgsqlDbType.Text);

            case ColumnKind.Integer when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number):
                return AddParameter(parameters, number, NpgsqlDbType.Integer);

            case ColumnKind.Double when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var real):
                return AddParameter(parameters, real, NpgsqlDbType.Double);

            // The "c" format ("d.hh:mm:ss") the grid sends durations in.
            case ColumnKind.Interval when TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var duration):
                return AddParameter(parameters, duration, NpgsqlDbType.Interval);

            case ColumnKind.Enum when Enum.TryParse(column.ClrEnumType!, value, ignoreCase: true, out var parsed)
                                      && Enum.IsDefined(column.ClrEnumType!, parsed):
                return $"{AddParameter(parameters, parsed.ToString()!, NpgsqlDbType.Text)}::{column.PgEnumType}";

            // Dates compare as timestamps, matching the in-memory provider's
            // DateTime comparison (so "2026-09-13 12:00" is after a due date of 2026-09-13).
            case ColumnKind.Timestamp or ColumnKind.Date
                when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date):
                return AddParameter(parameters, DateTime.SpecifyKind(date, DateTimeKind.Unspecified), NpgsqlDbType.Timestamp);

            default:
                return null;
        }
    }

    private static string BuildOrderTerms(SortDescriptor? sort, IssueColumn? groupColumn)
    {
        var sortColumn = sort is { Direction: not SortDirection.None } ? IssueColumns.Resolve(sort.PropertyName) : null;
        var terms = new List<string>();

        if (groupColumn is not null)
        {
            // Sorting by the grouped column orders the groups themselves.
            var groupDirection = sortColumn == groupColumn ? sort!.Direction : SortDirection.Ascending;
            terms.Add(OrderTerm(groupColumn, groupDirection));
        }

        if (sortColumn is not null && sortColumn != groupColumn)
        {
            terms.Add(OrderTerm(sortColumn, sort!.Direction));
        }

        // A unique tiebreaker keeps OFFSET paging stable: without it, rows
        // with equal sort keys can move between pages from one query to the next.
        if (sortColumn != IssueColumns.Id)
        {
            terms.Add("issues.id ASC");
        }

        return string.Join(", ", terms);
    }

    // Table-qualified on purpose: a bare ORDER BY name binds to an output
    // column first, and the select list's `status::text` is output as
    // "status" — which would sort enums alphabetically instead of in
    // declaration order.
    private static string OrderTerm(IssueColumn column, SortDirection direction) =>
        direction == SortDirection.Descending
            ? $"issues.{column.Sql} DESC NULLS LAST"
            : $"issues.{column.Sql} ASC NULLS FIRST";

    private static string AddParameter(List<SqlParameterSpec> parameters, object value, NpgsqlDbType type)
    {
        var name = $"p{parameters.Count}";
        parameters.Add(new SqlParameterSpec(name, value, type));
        return "@" + name;
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
