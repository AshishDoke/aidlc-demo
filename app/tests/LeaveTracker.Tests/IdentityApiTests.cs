using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LeaveTracker.Tests;

public class IdentityApiTests
{
    // LT-002 AC1: anyone can list employees without having picked who they are.
    [Fact]
    public async Task Employee_list_needs_no_picked_person()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var employees = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(4, employees.Count);
        Assert.All(employees, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("name").GetString()));
            Assert.True(e.TryGetProperty("isManager", out _));
            Assert.True(e.GetProperty("id").GetInt32() > 0);
        });
    }

    public static TheoryData<string?, string, string> MissingOrUnknownIdentity()
    {
        var data = new TheoryData<string?, string, string>();
        foreach (var who in new string?[] { null, "abc", "9999" })
        {
            data.Add(who, "POST", "/api/leave-requests");
            data.Add(who, "POST", "/api/leave-requests/1/approve");
            data.Add(who, "POST", "/api/leave-requests/1/reject");
            data.Add(who, "GET", "/api/me/leave-requests");
            data.Add(who, "GET", "/api/team/leave-requests/pending");
        }

        return data;
    }

    // LT-002 AC3: actions that depend on who you are are refused without a (known) picked person.
    [Theory]
    [MemberData(nameof(MissingOrUnknownIdentity))]
    public async Task Actions_without_picked_person_are_refused(string? who, string method, string path)
    {
        using var factory = new TestAppFactory();
        using var client = who is null ? factory.CreateClient() : factory.CreateClientAs(who);

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "POST"
                ? JsonContent.Create(new { firstDay = "2026-12-14", lastDay = "2026-12-14", type = "annual", reason = "x", comment = "x" })
                : null,
        };
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("Pick who you are", json.RootElement.GetProperty("detail").GetString());

        await using var db = factory.CreateDbContext();
        Assert.Equal(4, db.LeaveRequests.Count()); // nothing new kept
    }
}
