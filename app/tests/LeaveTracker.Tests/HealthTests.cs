using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Tests;

public class HealthTests
{
    // AC1: the app reports healthy and the data store exists without manual setup.
    [Fact]
    public async Task Health_returns_200_and_database_is_created()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("healthy", body!["status"]);

        await using var db = factory.CreateDbContext();
        // Querying both tables proves the schema was created at startup.
        _ = await db.Employees.CountAsync();
        _ = await db.LeaveRequests.CountAsync();
    }
}
