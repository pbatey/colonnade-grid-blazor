using System.Diagnostics;
using ColonnadeGrid.LargeData.Shared;
using ColonnadeGrid.Models;
using Npgsql;

namespace ColonnadeGrid.LargeData.Api.Data;

/// <summary>Runs <see cref="IssueQueryBuilder"/>'s SQL against Postgres and shapes the results for the API.</summary>
public sealed class IssueRepository(NpgsqlDataSource dataSource)
{
    private const int GroupKeyOrdinal = IssueQueryBuilder.SelectColumnCount;

    public async Task<IssuePage> QueryAsync(DataRequest request, CancellationToken cancellationToken)
    {
        var query = IssueQueryBuilder.Build(request);
        var stopwatch = Stopwatch.StartNew();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        // One round trip for the count, the page, and (when grouped) the group totals.
        await using var batch = new NpgsqlBatch(connection);
        batch.BatchCommands.Add(ToBatchCommand(query.Count));
        batch.BatchCommands.Add(ToBatchCommand(query.Page));
        if (query.GroupTotals is not null)
        {
            batch.BatchCommands.Add(ToBatchCommand(query.GroupTotals));
        }

        await using var reader = await batch.ExecuteReaderAsync(cancellationToken);

        await reader.ReadAsync(cancellationToken);
        var totalCount = (int)reader.GetInt64(0);

        await reader.NextResultAsync(cancellationToken);
        var items = new List<Issue>();
        var groupKeys = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadIssue(reader));
            if (query.GroupTotals is not null)
            {
                groupKeys.Add(ReadGroupKey(reader, GroupKeyOrdinal));
            }
        }

        IReadOnlyList<DataGroup>? groups = null;
        if (query.GroupTotals is not null)
        {
            await reader.NextResultAsync(cancellationToken);
            var groupTotals = new Dictionary<string, int>();
            while (await reader.ReadAsync(cancellationToken))
            {
                groupTotals[ReadGroupKey(reader, 0)] = (int)reader.GetInt64(1);
            }

            groups = BuildGroups(groupKeys, groupTotals);
        }

        return new IssuePage(items, totalCount, groups, stopwatch.Elapsed.TotalMilliseconds);
    }

    public async Task<IssueGroupList> ListGroupsAsync(GroupListRequest request, CancellationToken cancellationToken)
    {
        var (pageStatement, totalsStatement) = IssueQueryBuilder.BuildGroupList(request);
        var stopwatch = Stopwatch.StartNew();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var groups = new List<GroupSummary>();
        long totalGroupCount = 0;
        long totalCount = 0;
        await using (var command = ToCommand(pageStatement, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var key = ReadGroupKey(reader, 0);
                groups.Add(new GroupSummary(key, DisplayTextFor(key), (int)reader.GetInt64(1)));
                totalGroupCount = reader.GetInt64(2);
                totalCount = reader.GetInt64(3);
            }
        }

        // A batch past the last group has no rows to carry the totals.
        if (groups.Count == 0 && request.Skip > 0)
        {
            await using var command = ToCommand(totalsStatement, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            totalGroupCount = reader.GetInt64(0);
            totalCount = reader.GetInt64(1);
        }

        return new IssueGroupList(groups, (int)totalGroupCount, (int)totalCount, stopwatch.Elapsed.TotalMilliseconds);
    }

    public async Task<IssueGroupPages> GetGroupPagesAsync(GroupPagesRequest request, CancellationToken cancellationToken)
    {
        var (rowsStatement, countsStatement) = IssueQueryBuilder.BuildGroupPages(request);
        var stopwatch = Stopwatch.StartNew();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var batch = new NpgsqlBatch(connection);
        batch.BatchCommands.Add(ToBatchCommand(countsStatement));
        batch.BatchCommands.Add(ToBatchCommand(rowsStatement));

        await using var reader = await batch.ExecuteReaderAsync(cancellationToken);

        var countsByKey = new Dictionary<string, int>();
        while (await reader.ReadAsync(cancellationToken))
        {
            countsByKey[ReadGroupKey(reader, 0)] = (int)reader.GetInt64(1);
        }

        await reader.NextResultAsync(cancellationToken);
        var itemsByPage = request.Pages.Select(_ => new List<Issue>()).ToArray();
        while (await reader.ReadAsync(cancellationToken))
        {
            // Columns 0-1 are page_index and row_index; the issue's columns follow.
            itemsByPage[reader.GetInt32(0)].Add(ReadIssue(reader, offset: 2));
        }

        var pages = request.Pages
            .Select((page, i) => new GroupPage<Issue>(page.GroupKey, itemsByPage[i], countsByKey.GetValueOrDefault(page.GroupKey)))
            .ToList();

        return new IssueGroupPages(pages, stopwatch.Elapsed.TotalMilliseconds);
    }

    public async Task<IssueColumnStats> GetColumnStatsAsync(ColumnStatsRequest request, CancellationToken cancellationToken)
    {
        var (summaryStatement, valuesStatement) = IssueQueryBuilder.BuildColumnStats(request);
        var stopwatch = Stopwatch.StartNew();

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var batch = new NpgsqlBatch(connection);
        batch.BatchCommands.Add(ToBatchCommand(summaryStatement));
        if (valuesStatement is not null)
        {
            batch.BatchCommands.Add(ToBatchCommand(valuesStatement));
        }

        await using var reader = await batch.ExecuteReaderAsync(cancellationToken);

        await reader.ReadAsync(cancellationToken);
        var min = ReadFilterValue(reader, 0);
        var max = ReadFilterValue(reader, 1);
        var emptyCount = (int)reader.GetInt64(2);
        var totalCount = (int)reader.GetInt64(3);

        List<ColumnValueCount>? values = null;
        var hasMoreValues = false;
        if (valuesStatement is not null)
        {
            await reader.NextResultAsync(cancellationToken);
            values = [];
            while (await reader.ReadAsync(cancellationToken))
            {
                values.Add(new ColumnValueCount(ReadFilterValue(reader, 0)!, (int)reader.GetInt64(1)));
            }

            // The query returns one extra row when there are more values than asked for.
            var maxValueCount = Math.Clamp(request.MaxValueCount, 0, IssueQueryBuilder.MaxStatsValues);
            hasMoreValues = values.Count > maxValueCount;
            if (hasMoreValues)
            {
                values.RemoveRange(maxValueCount, values.Count - maxValueCount);
            }
        }

        return new IssueColumnStats(
            new ColumnStats(min, max, emptyCount, totalCount, values, hasMoreValues),
            stopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// A value as filter text (<see cref="FilterValues.Format"/>), so the grid reads it the same way as its own.
    /// Npgsql reads a <c>date</c> as <see cref="DateOnly"/>; <see cref="Issue.DueDate"/> is a
    /// <see cref="DateTime"/>, so it's formatted as one, like the in-memory provider would.
    /// </summary>
    private static string? ReadFilterValue(NpgsqlDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return FilterValues.Format(value is DateOnly date ? date.ToDateTime(TimeOnly.MinValue) : value);
    }

    /// <summary>
    /// Turns the page's per-row group keys into contiguous <see cref="DataGroup"/>
    /// ranges: <see cref="DataGroup.Count"/> is the group's rows on this page,
    /// and <see cref="DataGroup.TotalCount"/> its rows across all pages, which
    /// the grid shows in the group header.
    /// </summary>
    private static List<DataGroup> BuildGroups(List<string> groupKeys, Dictionary<string, int> groupTotals)
    {
        var groups = new List<DataGroup>();
        var start = 0;
        for (var i = 1; i <= groupKeys.Count; i++)
        {
            if (i < groupKeys.Count && groupKeys[i] == groupKeys[start])
            {
                continue;
            }

            var key = groupKeys[start];
            groups.Add(new DataGroup(
                key,
                DisplayText: DisplayTextFor(key),
                Count: i - start,
                StartIndex: start,
                TotalCount: groupTotals.GetValueOrDefault(key)));
            start = i;
        }

        return groups;
    }

    /// <summary>A group's key is its value as text, or <see cref="GroupKeys.Null"/> for null (see <see cref="IssueQueryBuilder.BuildGroupPages"/>).</summary>
    private static string ReadGroupKey(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? GroupKeys.Null : reader.GetString(ordinal);

    private static string DisplayTextFor(string groupKey) => groupKey == GroupKeys.Null ? "(empty)" : groupKey;

    private static Issue ReadIssue(NpgsqlDataReader reader, int offset = 0) => new()
    {
        Id = reader.GetInt32(offset),
        Title = reader.GetString(offset + 1),
        Status = Enum.Parse<IssueStatus>(reader.GetString(offset + 2)),
        Priority = Enum.Parse<IssuePriority>(reader.GetString(offset + 3)),
        Project = reader.GetString(offset + 4),
        Assignee = reader.IsDBNull(offset + 5) ? null : reader.GetString(offset + 5),
        StoryPoints = reader.IsDBNull(offset + 6) ? null : reader.GetInt32(offset + 6),
        CreatedAt = reader.GetDateTime(offset + 7),
        DueDate = reader.IsDBNull(offset + 8) ? null : reader.GetDateTime(offset + 8),
        EstimateHours = reader.IsDBNull(offset + 9) ? null : reader.GetDouble(offset + 9),
        TimeSpent = reader.IsDBNull(offset + 10) ? null : reader.GetFieldValue<TimeSpan>(offset + 10),
        CycleTime = reader.IsDBNull(offset + 11) ? null : reader.GetFieldValue<TimeSpan>(offset + 11)
    };

    private static NpgsqlBatchCommand ToBatchCommand(SqlStatement statement)
    {
        var command = new NpgsqlBatchCommand(statement.Text);
        foreach (var parameter in statement.Parameters)
        {
            command.Parameters.Add(new NpgsqlParameter(parameter.Name, parameter.Type) { Value = parameter.Value });
        }

        return command;
    }

    private static NpgsqlCommand ToCommand(SqlStatement statement, NpgsqlConnection connection)
    {
        var command = new NpgsqlCommand(statement.Text, connection);
        foreach (var parameter in statement.Parameters)
        {
            command.Parameters.Add(new NpgsqlParameter(parameter.Name, parameter.Type) { Value = parameter.Value });
        }

        return command;
    }
}
