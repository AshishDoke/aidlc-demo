using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LeaveTracker.Api.Services;

namespace LeaveTracker.Tests;

public class MyAndTeamRequestsApiTests
{
    private const string Maya = "maya.manager@example.com";
    private const string Alex = "alex.turner@example.com";
    private const string Sam = "sam.lee@example.com";

    private static async Task<int> AskAsync(TestAppFactory factory, int employeeId, string first, string last)
    {
        using var client = factory.CreateClientAs(employeeId);
        var response = await client.PostAsJsonAsync("/api/leave-requests", new { firstDay = first, lastDay = last, type = "annual" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<List<JsonElement>> GetListAsync(TestAppFactory factory, int asEmployeeId, string path)
    {
        using var client = factory.CreateClientAs(asEmployeeId);
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    // LT-002 AC6: own requests in every state, most recently made first, only your own, with days covered.
    [Fact]
    public async Task Own_requests_are_all_states_newest_first()
    {
        using var factory = new TestAppFactory();
        var samId = await factory.EmployeeIdAsync(Sam);
        // Sam has two seeded requests (one rejected, one approved). Make a third one now, dated earlier than both.
        var newest = await AskAsync(factory, samId, "2026-12-02", "2026-12-04");

        var mine = await GetListAsync(factory, samId, "/api/me/leave-requests");

        Assert.Equal(3, mine.Count);
        Assert.Equal(newest, mine[0].GetProperty("id").GetInt32());
        var ids = mine.Select(r => r.GetProperty("id").GetInt32()).ToList();
        Assert.Equal(ids.OrderByDescending(i => i), ids);
        Assert.All(mine, r => Assert.Equal(samId, r.GetProperty("employeeId").GetInt32()));
        Assert.Equal(
            new[] { "approved", "pending", "rejected" },
            mine.Select(r => r.GetProperty("status").GetString()).Order().ToArray());
        Assert.Equal(3, mine[0].GetProperty("daysCovered").GetInt32());
        Assert.All(mine, r => Assert.True(r.GetProperty("daysCovered").GetInt32() >= 1));
    }

    [Fact]
    public async Task Employee_with_no_requests_gets_an_empty_list()
    {
        using var factory = new TestAppFactory();

        var mine = await GetListAsync(factory, await factory.EmployeeIdAsync(Maya), "/api/me/leave-requests");

        Assert.Empty(mine);
    }

    // LT-002 AC7: only pending requests from the manager's own direct reports.
    [Fact]
    public async Task Team_pending_shows_only_own_direct_reports()
    {
        using var factory = new TestAppFactory();
        var mayaId = await factory.EmployeeIdAsync(Maya);
        var alexId = await factory.EmployeeIdAsync(Alex);

        int omarId, olgaId;
        await using (var db = factory.CreateDbContext())
        {
            var employees = new EmployeeService(db);
            omarId = (await employees.AddAsync(new NewEmployee("Omar Other", "omar.other@example.com", true, null))).Id;
            olgaId = (await employees.AddAsync(new NewEmployee("Olga Other", "olga.other@example.com", false, omarId))).Id;
        }

        var olgaRequest = await AskAsync(factory, olgaId, "2026-12-01", "2026-12-01");
        var mayaOwnRequest = await AskAsync(factory, mayaId, "2026-12-01", "2026-12-01");
        var alexLater = await AskAsync(factory, alexId, "2027-03-01", "2027-03-02");

        var team = await GetListAsync(factory, mayaId, "/api/team/leave-requests/pending");

        var ids = team.Select(r => r.GetProperty("id").GetInt32()).ToList();
        Assert.Contains(alexLater, ids);
        Assert.DoesNotContain(olgaRequest, ids);
        Assert.DoesNotContain(mayaOwnRequest, ids);
        Assert.All(team, r =>
        {
            Assert.Equal("pending", r.GetProperty("status").GetString());
            Assert.False(string.IsNullOrWhiteSpace(r.GetProperty("employeeName").GetString()));
            Assert.True(r.GetProperty("daysCovered").GetInt32() >= 1);
        });
        // Seeded: Alex has one pending request; decided seeded requests are not shown.
        Assert.Equal(2, team.Count);
        // Q6: earliest first day first.
        var firstDays = team.Select(r => r.GetProperty("firstDay").GetString()).ToList();
        Assert.Equal(firstDays.Order().ToList(), firstDays);

        var omarTeam = await GetListAsync(factory, omarId, "/api/team/leave-requests/pending");
        Assert.Equal(new[] { olgaRequest }, omarTeam.Select(r => r.GetProperty("id").GetInt32()));
    }

    // LT-002 AC8: a person who is not a manager cannot see team requests.
    [Fact]
    public async Task Non_manager_cannot_see_team_requests()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClientAs(await factory.EmployeeIdAsync(Alex));

        var response = await client.GetAsync("/api/team/leave-requests/pending");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("Only managers can see team requests", json.RootElement.GetProperty("detail").GetString());
    }
}
