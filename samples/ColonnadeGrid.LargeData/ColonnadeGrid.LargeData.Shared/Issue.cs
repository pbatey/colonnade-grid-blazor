namespace ColonnadeGrid.LargeData.Shared;

// Member names and order must match the issue_status/issue_priority Postgres
// enums in db/schema.sql — the API parses the database's enum labels into
// these, and relies on both sorting in the same order.
public enum IssueStatus
{
    Todo,
    InProgress,
    Done
}

public enum IssuePriority
{
    Low,
    Medium,
    High,
    Urgent
}

public sealed class Issue
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
    public string Project { get; set; } = "";
    public string? Assignee { get; set; }
    public int? StoryPoints { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public double? EstimateHours { get; set; }

    /// <summary>Time logged: minutes to under a day.</summary>
    public TimeSpan? TimeSpent { get; set; }

    /// <summary>Creation to completion, for Done issues: days to months.</summary>
    public TimeSpan? CycleTime { get; set; }
}
