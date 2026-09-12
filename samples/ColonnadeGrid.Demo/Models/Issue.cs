namespace ColonnadeGrid.Demo.Models;

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

public class Issue
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
    public string? Assignee { get; set; }
    public DateTime? DueDate { get; set; }
}

public static class IssueData
{
    private static readonly string[] Assignees = ["alice", "bob", "carol", "dave"];

    private static readonly string[] Titles =
    [
        "Fix login redirect loop",
        "Add dark mode toggle",
        "Write API documentation",
        "Refactor authentication middleware",
        "Upgrade dependency versions",
        "Investigate flaky checkout test",
        "Add pagination to search results",
        "Improve error messages on signup",
        "Set up CI caching",
        "Design empty-state illustrations",
        "Add keyboard shortcuts",
        "Fix memory leak in background worker",
        "Support CSV export",
        "Localize date formatting",
        "Add rate limiting to public API",
        "Clean up unused CSS",
        "Add integration tests for billing",
        "Improve accessibility of modal dialogs",
        "Cache expensive dashboard queries",
        "Support drag-and-drop file upload",
        "Fix timezone bug in scheduler",
        "Add audit log for admin actions",
        "Reduce bundle size",
        "Add health check endpoint",
        "Support bulk row selection",
        "Fix column resize snapping to zero",
        "Add dependency graph visualization",
        "Improve onboarding checklist",
        "Add webhook retry with backoff",
        "Migrate to new logging provider",
    ];

    public static List<Issue> CreateSampleIssues()
    {
        var random = new Random(42); // fixed seed for a stable, reproducible demo
        var issues = new List<Issue>();

        for (var i = 0; i < Titles.Length; i++)
        {
            var hasAssignee = random.NextDouble() > 0.15;
            var hasDueDate = random.NextDouble() > 0.3;

            issues.Add(new Issue
            {
                Id = i + 1,
                Title = Titles[i],
                Status = (IssueStatus)random.Next(0, 3),
                Priority = (IssuePriority)random.Next(0, 4),
                Assignee = hasAssignee ? Assignees[random.Next(Assignees.Length)] : null,
                DueDate = hasDueDate ? DateTime.Today.AddDays(random.Next(-10, 30)) : null
            });
        }

        return issues;
    }
}
