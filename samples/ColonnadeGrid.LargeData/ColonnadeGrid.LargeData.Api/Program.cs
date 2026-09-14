using System.Globalization;
using ColonnadeGrid.LargeData.Api.Data;
using ColonnadeGrid.LargeData.Shared;
using ColonnadeGrid.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("LargeData")
    ?? throw new InvalidOperationException("Missing connection string 'LargeData'.");

builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IssueRepository>();
builder.Services.ConfigureHttpJsonOptions(options => LargeDataJson.Configure(options.SerializerOptions));
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}

// Serves the Blazor WebAssembly client (including _framework/*, via the
// referenced client project's static web assets), so the whole sample runs
// from one `dotnet run`. Don't add UseBlazorFrameworkFiles(): it branches the
// pipeline for _framework requests before these endpoints can run.
app.MapStaticAssets();

// One page of rows (ungrouped, or paged across groups).
app.MapPost("/api/issues/query", (DataRequest request, HttpRequest http, IssueRepository issues, CancellationToken cancellationToken) =>
    RunQueryAsync(() => issues.QueryAsync(request, ClientNow(http), cancellationToken)));

// A batch of groups with their row counts, for paging each group separately.
app.MapPost("/api/issues/groups", (GroupListRequest request, HttpRequest http, IssueRepository issues, CancellationToken cancellationToken) =>
    RunQueryAsync(() => issues.ListGroupsAsync(request, ClientNow(http), cancellationToken)));

// A page of rows from each of several groups.
app.MapPost("/api/issues/group-pages", (GroupPagesRequest request, HttpRequest http, IssueRepository issues, CancellationToken cancellationToken) =>
    RunQueryAsync(() => issues.GetGroupPagesAsync(request, ClientNow(http), cancellationToken)));

// A column's range, empty count, and (for value lists) distinct values, for its filter editor.
app.MapPost("/api/issues/column-stats", (ColumnStatsRequest request, HttpRequest http, IssueRepository issues, CancellationToken cancellationToken) =>
    RunQueryAsync(() => issues.GetColumnStatsAsync(request, ClientNow(http), cancellationToken)));

app.MapFallbackToFile("index.html");

app.Run();

// "Now" for relative date filters ("within the last 30 days"): the browser's
// local clock, which the client sends, so they match the in-memory provider
// wherever the API runs. Falls back to the API's own clock.
static DateTime ClientNow(HttpRequest request) =>
    DateTime.SpecifyKind(
        DateTime.TryParse(request.Headers["X-Client-Now"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var now)
            ? now
            : DateTime.Now,
        DateTimeKind.Unspecified);

// Turns the failures a sample user is likely to hit into readable problem responses.
static async Task<IResult> RunQueryAsync<T>(Func<Task<T>> query)
{
    try
    {
        return Results.Ok(await query());
    }
    catch (InvalidQueryException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
    }
    catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
    {
        return Results.Problem(
            "The issues table doesn't exist yet. Run `docker compose run --rm seed` from samples/ColonnadeGrid.LargeData.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (NpgsqlException ex) when (ex is not PostgresException)
    {
        return Results.Problem(
            $"Can't reach Postgres ({ex.Message}). Run `docker compose up -d` from samples/ColonnadeGrid.LargeData.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
