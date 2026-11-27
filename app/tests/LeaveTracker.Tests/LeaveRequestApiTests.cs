using System.Net;
using System.Text.Json;

namespace LeaveTracker.Tests;

public class LeaveRequestApiTests
{
    private static async Task<(List<JsonElement> Requests, List<JsonElement> Employees)> LoadAsync(HttpClient client)
    {
        var requests = JsonDocument.Parse(await client.GetStringAsync("/api/leave-requests")).RootElement.EnumerateArray().ToList();
        var employees = JsonDocument.Parse(await client.GetStringAsync("/api/employees")).RootElement.EnumerateArray().ToList();
        return (requests, employees);
    }

    // AC6: a fresh start has requests in every state, each with requester, days, type and reason.
    [Fact]
    public async Task Fresh_start_has_requests_in_every_state()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/leave-requests");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var (requests, employees) = await LoadAsync(client);
        var employeeIds = employees.Select(e => e.GetProperty("id").GetInt32()).ToHashSet();
        var statuses = requests.Select(r => r.GetProperty("status").GetString()).ToHashSet();
        Assert.Contains("pending", statuses);
        Assert.Contains("approved", statuses);
        Assert.Contains("rejected", statuses);

        foreach (var r in requests)
        {
            Assert.Contains(r.GetProperty("employeeId").GetInt32(), employeeIds);
            Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("employeeName").GetString()));
            var first = DateOnly.ParseExact(r.GetProperty("firstDay").GetString()!, "yyyy-MM-dd");
            var last = DateOnly.ParseExact(r.GetProperty("lastDay").GetString()!, "yyyy-MM-dd");
            Assert.True(last >= first);
            // LT-002 AC14: every request shows the calendar days it covers, both ends included.
            Assert.Equal(last.DayNumber - first.DayNumber + 1, r.GetProperty("daysCovered").GetInt32());
            Assert.Contains(r.GetProperty("type").GetString(), new[] { "annual", "sick", "unpaid" });
            Assert.True(r.TryGetProperty("reason", out var reason));
            Assert.True(reason.ValueKind is JsonValueKind.String or JsonValueKind.Null);
        }
    }

    // AC7: decided requests show who decided, when, and the comment; pending ones have none of these.
    [Fact]
    public async Task Decided_requests_show_decider_time_and_comment()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var (requests, employees) = await LoadAsync(client);
        var managerIds = employees
            .Where(e => e.GetProperty("isManager").GetBoolean())
            .Select(e => e.GetProperty("id").GetInt32())
            .ToHashSet();

        var decided = requests.Where(r => r.GetProperty("status").GetString() != "pending").ToList();
        Assert.NotEmpty(decided);
        foreach (var r in decided)
        {
            Assert.Contains(r.GetProperty("decidedById").GetInt32(), managerIds);
            Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("decidedByName").GetString()));
            Assert.Equal(JsonValueKind.String, r.GetProperty("decidedAtUtc").ValueKind);
            // LT-002 Q10 / LT-001 review item 1: decision times are marked as UTC.
            Assert.EndsWith("Z", r.GetProperty("decidedAtUtc").GetString());
            Assert.True(r.TryGetProperty("decisionComment", out _));
        }

        Assert.Contains(decided, r => r.GetProperty("decisionComment").ValueKind == JsonValueKind.String);

        var pending = requests.Where(r => r.GetProperty("status").GetString() == "pending").ToList();
        Assert.NotEmpty(pending);
        foreach (var r in pending)
        {
            Assert.Equal(JsonValueKind.Null, r.GetProperty("decidedById").ValueKind);
            Assert.Equal(JsonValueKind.Null, r.GetProperty("decidedAtUtc").ValueKind);
            Assert.Equal(JsonValueKind.Null, r.GetProperty("decisionComment").ValueKind);
        }
    }

    [Fact]
    public async Task Get_request_by_id_returns_it()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();
        var (requests, _) = await LoadAsync(client);
        var id = requests[0].GetProperty("id").GetInt32();

        var response = await client.GetAsync($"/api/leave-requests/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, json.RootElement.GetProperty("id").GetInt32());
        // LT-002 AC13/AC14: a single lookup needs no picked person and shows all details, including days covered.
        Assert.True(json.RootElement.GetProperty("daysCovered").GetInt32() >= 1);
        Assert.True(json.RootElement.TryGetProperty("status", out _));
        Assert.True(json.RootElement.TryGetProperty("decidedById", out _));
    }

    [Fact]
    public async Task Get_unknown_request_returns_404_with_plain_message()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/leave-requests/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("No leave request with id 9999.", json.RootElement.GetProperty("detail").GetString());
    }
}
