namespace ColonnadeGrid.Tests.TestSupport;

public enum IssueStatus
{
    Todo,
    InProgress,
    Done
}

public class Issue
{
    public string Title { get; set; } = "";
    public int Priority { get; set; }
    public IssueStatus Status { get; set; }
    public string? Assignee { get; set; }
}

public static class SampleIssues
{
    public static List<Issue> Create() =>
    [
        new() { Title = "Fix login bug", Priority = 1, Status = IssueStatus.Todo, Assignee = "alice" },
        new() { Title = "Add dark mode", Priority = 3, Status = IssueStatus.InProgress, Assignee = "bob" },
        new() { Title = "Write docs", Priority = 2, Status = IssueStatus.Todo, Assignee = null },
        new() { Title = "Refactor auth", Priority = 1, Status = IssueStatus.Done, Assignee = "alice" },
    ];

    /// <summary>
    /// "Issue 01".."Issue {count}", with Status cycling InProgress, Done, Todo —
    /// for 23 issues, InProgress and Done have 8 each and Todo has 7.
    /// </summary>
    public static List<Issue> CreateMany(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new Issue { Title = $"Issue {i:00}", Priority = i % 3, Status = (IssueStatus)(i % 3) })
            .ToList();
}
