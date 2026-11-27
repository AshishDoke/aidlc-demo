using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LeaveTracker.Tests;

public class AskForLeaveApiTests
{
    private const string Alex = "alex.turner@example.com";
    private const string Sam = "sam.lee@example.com";

    private static async Task<(HttpResponseMessage Response, JsonElement Body)> PostAsync(HttpClient client, object body)
    {
        var response = await client.PostAsJsonAsync("/api/leave-requests", body);
        var text = await response.Content.ReadAsStringAsync();
        return (response, JsonDocument.Parse(text).RootElement.Clone());
    }

    private static int RequestCount(TestAppFactory factory)
    {
        using var db = factory.CreateDbContext();
        return db.LeaveRequests.Count();
    }

    // LT-002 AC2: an employee asks for leave for themselves; it is pending, undecided, and shows 3 days.
    [Fact]
    public async Task Employee_asks_for_leave_and_it_is_pending()
    {
        using var factory = new TestAppFactory();
        var alexId = await factory.EmployeeIdAsync(Alex);
        using var client = factory.CreateClientAs(alexId);

        var (response, body) = await PostAsync(client, new { firstDay = "2026-12-14", lastDay = "2026-12-16", type = "annual", reason = "Trip" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = body.GetProperty("id").GetInt32();
        Assert.Equal($"/api/leave-requests/{id}", response.Headers.Location?.OriginalString);
        Assert.Equal(alexId, body.GetProperty("employeeId").GetInt32());
        Assert.Equal("Alex Turner", body.GetProperty("employeeName").GetString());
        Assert.Equal("2026-12-14", body.GetProperty("firstDay").GetString());
        Assert.Equal("2026-12-16", body.GetProperty("lastDay").GetString());
        Assert.Equal("annual", body.GetProperty("type").GetString());
        Assert.Equal("Trip", body.GetProperty("reason").GetString());
        Assert.Equal("pending", body.GetProperty("status").GetString());
        Assert.Equal(3, body.GetProperty("daysCovered").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("decidedById").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("decidedAtUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("decisionComment").ValueKind);

        // Kept: the single lookup shows the same request.
        using var lookup = JsonDocument.Parse(await factory.CreateClient().GetStringAsync($"/api/leave-requests/{id}"));
        Assert.Equal("pending", lookup.RootElement.GetProperty("status").GetString());
    }

    // LT-002 AC4: last day before first day is refused with a message saying so; nothing is kept.
    [Fact]
    public async Task Last_day_before_first_day_is_refused()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClientAs(await factory.EmployeeIdAsync(Alex));
        var before = RequestCount(factory);

        var (response, body) = await PostAsync(client, new { firstDay = "2026-12-16", lastDay = "2026-12-14", type = "annual" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("is before the first day", body.GetProperty("errors").GetProperty("lastDay")[0].GetString());
        Assert.Equal(before, RequestCount(factory));
    }

    // LT-002 AC4: leave type must be annual, sick or unpaid (including numbers and missing values).
    [Theory]
    [InlineData("holiday")]
    [InlineData("1")]
    [InlineData(null)]
    public async Task Bad_type_is_refused(string? type)
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClientAs(await factory.EmployeeIdAsync(Alex));
        var before = RequestCount(factory);

        var (response, body) = await PostAsync(client, new { firstDay = "2026-12-14", lastDay = "2026-12-14", type });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("annual, sick or unpaid", body.GetProperty("errors").GetProperty("type")[0].GetString());
        Assert.Equal(before, RequestCount(factory));
    }

    [Fact]
    public async Task Type_is_case_insensitive()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClientAs(await factory.EmployeeIdAsync(Alex));

        var (response, body) = await PostAsync(client, new { firstDay = "2026-12-14", lastDay = "2026-12-14", type = "Sick" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("sick", body.GetProperty("type").GetString());
        Assert.Equal(1, body.GetProperty("daysCovered").GetInt32());
    }

    // LT-002 AC4: dates must be real dates in year-month-day form; missing dates are required.
    [Theory]
    [InlineData("14/12/2026", "2026-12-14", "firstDay", "must be a date like")]
    [InlineData("2026-02-30", "2026-03-01", "firstDay", "must be a date like")]
    [InlineData("2026-12-14", "tomorrow", "lastDay", "must be a date like")]
    [InlineData(null, "2026-12-14", "firstDay", "First day is required")]
    public async Task Bad_or_missing_dates_are_refused(string? first, string? last, string field, string message)
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClientAs(await factory.EmployeeIdAsync(Alex));
        var before = RequestCount(factory);

        var (response, body) = await PostAsync(client, new { firstDay = first, lastDay = last, type = "annual" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(message, body.GetProperty("errors").GetProperty(field)[0].GetString());
        Assert.Equal(before, RequestCount(factory));
    }

    // LT-002 AC4: the LT-001 reason limit still applies.
    [Fact]
    public async Task Reason_too_long_is_refused()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClientAs(await factory.EmployeeIdAsync(Alex));
        var before = RequestCount(factory);

        var (response, body) = await PostAsync(client, new { firstDay = "2026-12-14", lastDay = "2026-12-14", type = "annual", reason = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("500 characters", body.GetProperty("errors").GetProperty("reason")[0].GetString());
        Assert.Equal(before, RequestCount(factory));
    }

    // LT-002 AC5: asking on behalf of someone else is refused; nothing is kept.
    [Fact]
    public async Task Asking_for_someone_else_is_refused()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClientAs(await factory.EmployeeIdAsync(Alex));
        var samId = await factory.EmployeeIdAsync(Sam);
        var before = RequestCount(factory);

        var (response, body) = await PostAsync(client, new { firstDay = "2026-12-14", lastDay = "2026-12-14", type = "annual", employeeId = samId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("only ask for leave for yourself", body.GetProperty("detail").GetString());
        Assert.Equal(before, RequestCount(factory));
    }

    [Fact]
    public async Task Naming_yourself_explicitly_is_allowed()
    {
        using var factory = new TestAppFactory();
        var alexId = await factory.EmployeeIdAsync(Alex);
        using var client = factory.CreateClientAs(alexId);

        var (response, _) = await PostAsync(client, new { firstDay = "2026-12-14", lastDay = "2026-12-14", type = "annual", employeeId = alexId });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
