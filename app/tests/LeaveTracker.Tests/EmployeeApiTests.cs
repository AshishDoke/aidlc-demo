using System.Net;
using System.Text.Json;

namespace LeaveTracker.Tests;

public class EmployeeApiTests
{
    // AC2: a fresh start has exactly one manager and three people reporting to them.
    [Fact]
    public async Task Fresh_start_has_one_manager_and_three_reports()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/employees");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var employees = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(4, employees.Count);

        foreach (var e in employees)
        {
            Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("name").GetString()));
            Assert.Contains("@", e.GetProperty("email").GetString());
        }

        var manager = Assert.Single(employees, e => e.GetProperty("isManager").GetBoolean());
        var managerId = manager.GetProperty("id").GetInt32();
        var reports = employees.Where(e => !e.GetProperty("isManager").GetBoolean()).ToList();
        Assert.Equal(3, reports.Count);
        Assert.All(reports, r =>
        {
            Assert.Equal(managerId, r.GetProperty("managerId").GetInt32());
            Assert.Equal(manager.GetProperty("name").GetString(), r.GetProperty("managerName").GetString());
        });
    }

    [Fact]
    public async Task Get_employee_by_id_returns_it()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();
        using var list = JsonDocument.Parse(await client.GetStringAsync("/api/employees"));
        var first = list.RootElement[0];
        var id = first.GetProperty("id").GetInt32();

        var response = await client.GetAsync($"/api/employees/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(first.GetProperty("email").GetString(), json.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Get_unknown_employee_returns_404_with_plain_message()
    {
        using var factory = new TestAppFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/employees/9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("No employee with id 9999.", json.RootElement.GetProperty("detail").GetString());
    }
}
