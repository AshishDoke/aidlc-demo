using LeaveTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Tests;

public class EmployeeServiceTests : IDisposable
{
    private readonly TestDb _testDb = new();

    public void Dispose() => _testDb.Dispose();

    private async Task<int> AddManagerAsync(string email = "boss@example.com")
    {
        await using var db = _testDb.CreateContext();
        var manager = await new EmployeeService(db).AddAsync(new NewEmployee("Boss", email, true, null));
        return manager.Id;
    }

    // AC4: duplicate email is refused, ignoring case and surrounding spaces.
    [Fact]
    public async Task Duplicate_email_is_refused_ignoring_case()
    {
        var managerId = await AddManagerAsync();
        await using (var db = _testDb.CreateContext())
        {
            await new EmployeeService(db).AddAsync(new NewEmployee("Alex", "alex.turner@example.com", false, managerId));
        }

        await using (var db = _testDb.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<RuleViolationException>(() =>
                new EmployeeService(db).AddAsync(new NewEmployee("Other Alex", "  ALEX.TURNER@example.com ", false, managerId)));

            Assert.Contains("already in use", ex.Errors["email"].Single());
        }

        await using (var db = _testDb.CreateContext())
        {
            Assert.Equal(2, await db.Employees.CountAsync());
            Assert.Equal("Alex", (await db.Employees.SingleAsync(e => e.NormalizedEmail == "alex.turner@example.com")).Name);
        }
    }

    // AC5: a non-manager without a manager is refused.
    [Fact]
    public async Task Non_manager_without_manager_is_refused()
    {
        await using var db = _testDb.CreateContext();

        var ex = await Assert.ThrowsAsync<RuleViolationException>(() =>
            new EmployeeService(db).AddAsync(new NewEmployee("Lonely", "lonely@example.com", false, null)));

        Assert.Contains("must report to exactly one manager", ex.Errors["managerId"].Single());
        Assert.Equal(0, await db.Employees.CountAsync());
    }

    // AC5: a non-manager whose "manager" is not marked as a manager is refused.
    [Fact]
    public async Task Non_manager_whose_manager_is_not_a_manager_is_refused()
    {
        var managerId = await AddManagerAsync();
        int reportId;
        await using (var db = _testDb.CreateContext())
        {
            reportId = (await new EmployeeService(db).AddAsync(new NewEmployee("Report", "report@example.com", false, managerId))).Id;
        }

        await using (var db = _testDb.CreateContext())
        {
            var ex = await Assert.ThrowsAsync<RuleViolationException>(() =>
                new EmployeeService(db).AddAsync(new NewEmployee("Second", "second@example.com", false, reportId)));

            var message = ex.Errors["managerId"].Single();
            Assert.Contains("must report to exactly one manager", message);
            Assert.Contains("Report is not a manager", message);
            Assert.Equal(2, await db.Employees.CountAsync());
        }
    }

    // AC5: a manager id that does not exist is refused.
    [Fact]
    public async Task Non_manager_with_unknown_manager_is_refused()
    {
        await using var db = _testDb.CreateContext();

        var ex = await Assert.ThrowsAsync<RuleViolationException>(() =>
            new EmployeeService(db).AddAsync(new NewEmployee("Ghost", "ghost@example.com", false, 999)));

        Assert.Contains("must report to exactly one manager", ex.Errors["managerId"].Single());
    }

    // Spec Q2: a manager does not need a manager.
    [Fact]
    public async Task Manager_without_manager_is_accepted()
    {
        var id = await AddManagerAsync();

        await using var db = _testDb.CreateContext();
        var stored = await db.Employees.SingleAsync(e => e.Id == id);
        Assert.True(stored.IsManager);
        Assert.Null(stored.ManagerId);
    }

    // Spec Q8: name and email are required and the email must look like an email.
    [Theory]
    [InlineData("", "a@example.com", "name")]
    [InlineData("   ", "a@example.com", "name")]
    [InlineData("Ann", "", "email")]
    [InlineData("Ann", "not-an-email", "email")]
    [InlineData("Ann", "a@@example.com", "email")]
    [InlineData("Ann", "@example.com", "email")]
    [InlineData("Ann", "ann@", "email")]
    [InlineData("Ann", "ann smith@example.com", "email")]
    public async Task Missing_or_malformed_name_or_email_is_refused(string name, string email, string field)
    {
        await using var db = _testDb.CreateContext();

        var ex = await Assert.ThrowsAsync<RuleViolationException>(() =>
            new EmployeeService(db).AddAsync(new NewEmployee(name, email, true, null)));

        Assert.True(ex.Errors.ContainsKey(field));
        Assert.Equal(0, await db.Employees.CountAsync());
    }

    [Fact]
    public async Task Email_is_stored_trimmed_with_normalized_copy()
    {
        await using var db = _testDb.CreateContext();

        var e = await new EmployeeService(db).AddAsync(new NewEmployee("  Ann  ", "  Ann@Example.com ", true, null));

        Assert.Equal("Ann", e.Name);
        Assert.Equal("Ann@Example.com", e.Email);
        Assert.Equal("ann@example.com", e.NormalizedEmail);
    }
}
