using ColonnadeGrid.Abstractions;
using ColonnadeGrid.Models;
using ColonnadeGrid.Providers;

namespace ColonnadeGrid.Tests.TestSupport;

public enum TicketType
{
    Bug,
    Feature,
    Chore
}

/// <summary>A row with one column of each filter kind.</summary>
public class Ticket
{
    public string Name { get; set; } = "";
    public TicketType? Type { get; set; }
    public double? Estimate { get; set; }
    public DateTime? Opened { get; set; }
    public TimeSpan? Spent { get; set; }
    public string Team { get; set; } = "";
}

public static class SampleTickets
{
    /// <summary>
    /// Five tickets. Opened dates are relative to today (3, 45, 400, and 800 days
    /// ago), so date presets have something to show. Estimates ≤ 1.5 pick out A and
    /// B, whose time spent is under a day; the rest reach 45 days.
    /// </summary>
    public static List<Ticket> Create() =>
    [
        new() { Name = "A", Type = TicketType.Bug, Estimate = 1.5, Opened = DateTime.Today.AddDays(-3), Spent = TimeSpan.FromMinutes(30), Team = "Core" },
        new() { Name = "B", Type = TicketType.Feature, Estimate = 0.5, Opened = DateTime.Today.AddDays(-45), Spent = new TimeSpan(2, 15, 0), Team = "Web" },
        new() { Name = "C", Type = null, Estimate = null, Opened = null, Spent = null, Team = "Core" },
        new() { Name = "D", Type = TicketType.Chore, Estimate = 8, Opened = DateTime.Today.AddDays(-400), Spent = TimeSpan.FromDays(3), Team = "Data" },
        new() { Name = "E", Type = TicketType.Bug, Estimate = 40.25, Opened = DateTime.Today.AddDays(-800), Spent = TimeSpan.FromDays(45), Team = "Web" },
    ];
}

/// <summary>An <see cref="IColumnStatsProvider{TItem}"/> that records its stats requests, backed by <see cref="InMemoryDataProvider{TItem}"/>.</summary>
public sealed class RecordingStatsProvider<TItem>(IEnumerable<TItem> items) : IColumnStatsProvider<TItem>
{
    private readonly InMemoryDataProvider<TItem> _inner = new(items);

    public List<ColumnStatsRequest> StatsRequests { get; } = [];

    public Task<DataResponse<TItem>> GetDataAsync(DataRequest request, CancellationToken cancellationToken = default) =>
        _inner.GetDataAsync(request, cancellationToken);

    public Task<ColumnStats> GetColumnStatsAsync(ColumnStatsRequest request, CancellationToken cancellationToken = default)
    {
        StatsRequests.Add(request);
        return _inner.GetColumnStatsAsync(request, cancellationToken);
    }
}
