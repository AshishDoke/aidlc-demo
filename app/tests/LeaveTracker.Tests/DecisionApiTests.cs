using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeaveTracker.Api.Data;
using LeaveTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Tests;

public class DecisionApiTests
{
    private const string Maya = "maya.manager@example.com";
    private const string Alex = "alex.turner@example.com";

    private static async Task<int> AskAsync(TestAppFactory factory, int employeeId)
    {
        using var client = factory.CreateClientAs(employeeId);
        var response = await client.PostAsJsonAsync("/api/leave-requests", new { firstDay = "2026-12-14", lastDay = "2026-12-15", type = "annual" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<(HttpResponseMessage Response, JsonElement Body)> DecideAsync(
        TestAppFactory factory, int deciderId, int requestId, string action, object? body)
    {
        using var client = factory.CreateClientAs(deciderId);
        var response = body is null
            ? await client.PostAsync($"/api/leave-requests/{requestId}/{action}", null)
            : await client.PostAsJsonAsync($"/api/leave-requests/{requestId}/{action}", body);
        var text = await response.Content.ReadAsStringAsync();
        return (response, JsonDocument.Parse(text).RootElement.Clone());
    }

    private static async Task<LeaveRequest> StoredAsync(TestAppFactory factory, int requestId)
    {
        await using var db = factory.CreateDbContext();
        return await db.LeaveRequests.AsNoTracking().SingleAsync(r => r.Id == requestId);
    }

    private static async Task<(int ManagerId, int ReportId)> AddOtherTeamAsync(TestAppFactory factory)
    {
        await using var db = factory.CreateDbContext();
        var employees = new EmployeeService(db);
        var manager = await employees.AddAsync(new NewEmployee("Omar Other", "omar.other@example.com", true, null));
        var report = await employees.AddAsync(new NewEmployee("Olga Other", "olga.other@example.com", false, manager.Id));
        return (manager.Id, report.Id);
    }

    // LT-002 AC9: a manager approves a pending request from their team, with or without a comment.
    [Theory]
    [InlineData("Have fun")]
    [InlineData(null)]
    public async Task Manager_approves_with_and_without_comment(string? comment)
    {
        using var factory = new TestAppFactory();
        var mayaId = await factory.EmployeeIdAsync(Maya);
        var requestId = await AskAsync(factory, await factory.EmployeeIdAsync(Alex));

        var (response, body) = await DecideAsync(factory, mayaId, requestId, "approve", comment is null ? null : new { comment });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("approved", body.GetProperty("status").GetString());
        Assert.Equal(mayaId, body.GetProperty("decidedById").GetInt32());
        Assert.Equal("Maya Manager", body.GetProperty("decidedByName").GetString());
        var decidedAt = body.GetProperty("decidedAtUtc").GetString()!;
        Assert.EndsWith("Z", decidedAt);
        Assert.True(DateTime.UtcNow - DateTime.Parse(decidedAt).ToUniversalTime() < TimeSpan.FromMinutes(5));
        if (comment is null)
        {
            Assert.Equal(JsonValueKind.Null, body.GetProperty("decisionComment").ValueKind);
        }
        else
        {
            Assert.Equal(comment, body.GetProperty("decisionComment").GetString());
        }

        Assert.Equal(2, body.GetProperty("daysCovered").GetInt32());
        Assert.Equal(LeaveStatus.Approved, (await StoredAsync(factory, requestId)).Status);
    }

    // LT-002 AC10: a rejection with a reason is kept.
    [Fact]
    public async Task Manager_rejects_with_reason()
    {
        using var factory = new TestAppFactory();
        var mayaId = await factory.EmployeeIdAsync(Maya);
        var requestId = await AskAsync(factory, await factory.EmployeeIdAsync(Alex));

        var (response, body) = await DecideAsync(factory, mayaId, requestId, "reject", new { reason = "Release week" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("rejected", body.GetProperty("status").GetString());
        Assert.Equal(mayaId, body.GetProperty("decidedById").GetInt32());
        Assert.EndsWith("Z", body.GetProperty("decidedAtUtc").GetString());
        Assert.Equal("Release week", body.GetProperty("decisionComment").GetString());
    }

    // LT-002 AC10: a rejection without a reason is refused and the request stays pending.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reject_without_reason_is_refused(string? reason)
    {
        using var factory = new TestAppFactory();
        var mayaId = await factory.EmployeeIdAsync(Maya);
        var requestId = await AskAsync(factory, await factory.EmployeeIdAsync(Alex));

        var (response, body) = await DecideAsync(factory, mayaId, requestId, "reject", reason is null ? null : new { reason });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("rejection needs a reason", body.GetProperty("errors").GetProperty("reason")[0].GetString());
        Assert.Equal(LeaveStatus.Pending, (await StoredAsync(factory, requestId)).Status);
    }

    // LT-002 AC8: a person who is not a manager cannot decide.
    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Non_manager_cannot_decide(string action)
    {
        using var factory = new TestAppFactory();
        var alexId = await factory.EmployeeIdAsync(Alex);
        var requestId = await AskAsync(factory, await factory.EmployeeIdAsync("priya.shah@example.com"));

        var (response, body) = await DecideAsync(factory, alexId, requestId, action, new { reason = "No", comment = "Ok" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("not a manager", body.GetProperty("detail").GetString());
        Assert.Equal(LeaveStatus.Pending, (await StoredAsync(factory, requestId)).Status);
    }

    // LT-002 AC11: a manager cannot decide a request from outside their team, including their own.
    [Fact]
    public async Task Cannot_decide_request_outside_team()
    {
        using var factory = new TestAppFactory();
        var mayaId = await factory.EmployeeIdAsync(Maya);
        var (omarId, olgaId) = await AddOtherTeamAsync(factory);
        var olgaRequest = await AskAsync(factory, olgaId);
        var mayaOwnRequest = await AskAsync(factory, mayaId);

        var cases = new[]
        {
            (Decider: mayaId, Request: olgaRequest, Action: "approve"),
            (Decider: mayaId, Request: olgaRequest, Action: "reject"),
            (Decider: mayaId, Request: mayaOwnRequest, Action: "approve"),
            (Decider: omarId, Request: mayaOwnRequest, Action: "reject"),
        };
        foreach (var c in cases)
        {
            var (response, body) = await DecideAsync(factory, c.Decider, c.Request, c.Action, new { reason = "No", comment = "Ok" });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Contains("not in your team", body.GetProperty("detail").GetString());
        }

        Assert.Equal(LeaveStatus.Pending, (await StoredAsync(factory, olgaRequest)).Status);
        Assert.Equal(LeaveStatus.Pending, (await StoredAsync(factory, mayaOwnRequest)).Status);
    }

    // LT-002 AC12: an already decided request cannot be decided again; the original decision is unchanged.
    [Fact]
    public async Task Already_decided_request_cannot_be_decided_again()
    {
        using var factory = new TestAppFactory();
        var mayaId = await factory.EmployeeIdAsync(Maya);
        var requestId = await AskAsync(factory, await factory.EmployeeIdAsync(Alex));
        var (first, _) = await DecideAsync(factory, mayaId, requestId, "approve", new { comment = "Enjoy" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var original = await StoredAsync(factory, requestId);

        foreach (var (action, body) in new[] { ("approve", (object)new { comment = "Again" }), ("reject", new { reason = "Changed my mind" }) })
        {
            var (response, problem) = await DecideAsync(factory, mayaId, requestId, action, body);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("already approved by Maya Manager", problem.GetProperty("detail").GetString());
        }

        var after = await StoredAsync(factory, requestId);
        Assert.Equal(LeaveStatus.Approved, after.Status);
        Assert.Equal("Enjoy", after.DecisionComment);
        Assert.Equal(original.DecidedAtUtc, after.DecidedAtUtc);
    }

    // LT-002 AC12: also for a seeded rejected request.
    [Fact]
    public async Task Seeded_rejected_request_cannot_be_approved()
    {
        using var factory = new TestAppFactory();
        var mayaId = await factory.EmployeeIdAsync(Maya);
        await using var db = factory.CreateDbContext();
        var rejected = await db.LeaveRequests.FirstAsync(r => r.Status == LeaveStatus.Rejected);

        var (response, problem) = await DecideAsync(factory, mayaId, rejected.Id, "approve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("already rejected by Maya Manager", problem.GetProperty("detail").GetString());
    }

    // LT-002 AC13: deciding a request that does not exist.
    [Theory]
    [InlineData("approve")]
    [InlineData("reject")]
    public async Task Deciding_unknown_request_is_404(string action)
    {
        using var factory = new TestAppFactory();
        var mayaId = await factory.EmployeeIdAsync(Maya);

        var (response, problem) = await DecideAsync(factory, mayaId, 9999, action, new { reason = "No" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("No leave request with id 9999.", problem.GetProperty("detail").GetString());
    }
}
