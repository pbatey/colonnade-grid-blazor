using ColonnadeGrid.LargeData.Shared;

namespace ColonnadeGrid.LargeData.Api.Data;

public enum ColumnKind
{
    Integer,
    Double,
    Text,
    Enum,
    Timestamp,
    Date,
    Interval
}

/// <summary>Maps one <see cref="Issue"/> property to its column in the <c>issues</c> table.</summary>
/// <param name="PropertyName">The C# property name the grid sends in sort/filter/group descriptors.</param>
/// <param name="Sql">The column name. Only ever taken from <see cref="IssueColumns"/>, never from the request.</param>
/// <param name="PgEnumType">For <see cref="ColumnKind.Enum"/> columns, the Postgres enum type to cast filter values to.</param>
/// <param name="ClrEnumType">For <see cref="ColumnKind.Enum"/> columns, the C# enum used to validate filter values.</param>
public sealed record IssueColumn(
    string PropertyName,
    string Sql,
    ColumnKind Kind,
    string? PgEnumType = null,
    Type? ClrEnumType = null);

/// <summary>
/// The allow-list of queryable columns. Property names from a request are
/// resolved here, so user input never reaches the SQL text as an identifier.
/// </summary>
public static class IssueColumns
{
    public static IssueColumn Id { get; } = new(nameof(Issue.Id), "id", ColumnKind.Integer);

    private static readonly Dictionary<string, IssueColumn> ByProperty = new IssueColumn[]
    {
        Id,
        new(nameof(Issue.Title), "title", ColumnKind.Text),
        new(nameof(Issue.Status), "status", ColumnKind.Enum, "issue_status", typeof(IssueStatus)),
        new(nameof(Issue.Priority), "priority", ColumnKind.Enum, "issue_priority", typeof(IssuePriority)),
        new(nameof(Issue.Project), "project", ColumnKind.Text),
        new(nameof(Issue.Assignee), "assignee", ColumnKind.Text),
        new(nameof(Issue.StoryPoints), "story_points", ColumnKind.Integer),
        new(nameof(Issue.CreatedAt), "created_at", ColumnKind.Timestamp),
        new(nameof(Issue.DueDate), "due_date", ColumnKind.Date),
        new(nameof(Issue.EstimateHours), "estimate_hours", ColumnKind.Double),
        new(nameof(Issue.TimeSpent), "time_spent", ColumnKind.Interval),
        new(nameof(Issue.CycleTime), "cycle_time", ColumnKind.Interval),
    }.ToDictionary(c => c.PropertyName, StringComparer.Ordinal);

    public static IssueColumn Resolve(string propertyName) =>
        ByProperty.TryGetValue(propertyName, out var column)
            ? column
            : throw new InvalidQueryException($"'{propertyName}' is not a queryable issue property.");
}

/// <summary>A request the API can't turn into a query (unknown property, missing filter value, ...). Reported as HTTP 400.</summary>
public sealed class InvalidQueryException(string message) : Exception(message);
