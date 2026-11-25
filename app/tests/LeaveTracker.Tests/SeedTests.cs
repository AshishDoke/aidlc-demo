using LeaveTracker.Api.Data;
using LeaveTracker.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Tests;

public class SeedTests
{
    // AC3: seeding twice on the same database does not duplicate the team or its requests.
    [Fact]
    public async Task Seeding_twice_does_not_duplicate()
    {
        using var testDb = new TestDb();

        await using (var db = testDb.CreateContext())
        {
            await DemoSeeder.SeedAsync(db);
        }

        await using (var db = testDb.CreateContext())
        {
            await DemoSeeder.SeedAsync(db);
        }

        await using (var db = testDb.CreateContext())
        {
            Assert.Equal(4, await db.Employees.CountAsync());
            Assert.Equal(4, await db.LeaveRequests.CountAsync());
        }
    }

    // AC3: restarting the app (a second host on the same database) keeps the same counts.
    [Fact]
    public async Task Restarting_the_app_does_not_duplicate()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        int employeesAfterFirstStart;
        int requestsAfterFirstStart;
        using (var first = new TestAppFactory(connection))
        {
            using var _ = first.CreateClient(); // starts the host: EnsureCreated + seed
            await using var db = first.CreateDbContext();
            employeesAfterFirstStart = await db.Employees.CountAsync();
            requestsAfterFirstStart = await db.LeaveRequests.CountAsync();
        }

        for (var restart = 0; restart < 2; restart++)
        {
            using var again = new TestAppFactory(connection);
            using var _ = again.CreateClient();
            await using var db = again.CreateDbContext();
            Assert.Equal(employeesAfterFirstStart, await db.Employees.CountAsync());
            Assert.Equal(requestsAfterFirstStart, await db.LeaveRequests.CountAsync());
        }

        Assert.Equal(4, employeesAfterFirstStart);
        Assert.Equal(4, requestsAfterFirstStart);
    }

    // Spec Q9: seeding only happens when there are no employees at all.
    [Fact]
    public async Task Seeding_is_skipped_when_any_employee_exists()
    {
        using var testDb = new TestDb();
        await using (var db = testDb.CreateContext())
        {
            await new EmployeeService(db).AddAsync(new NewEmployee("Existing", "existing@example.com", true, null));
        }

        await using (var db = testDb.CreateContext())
        {
            await DemoSeeder.SeedAsync(db);
        }

        await using (var db = testDb.CreateContext())
        {
            Assert.Equal(1, await db.Employees.CountAsync());
            Assert.Equal(0, await db.LeaveRequests.CountAsync());
        }
    }

    // Spec Q10 and design seed table: fictional people on example.com, one manager, three reports,
    // requests in every state with decisions recorded.
    [Fact]
    public async Task Seed_creates_the_designed_demo_team()
    {
        using var testDb = new TestDb();
        var now = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        await using (var db = testDb.CreateContext())
        {
            await DemoSeeder.SeedAsync(db, now);
        }

        await using var check = testDb.CreateContext();
        var employees = await check.Employees.ToListAsync();
        Assert.All(employees, e => Assert.EndsWith("@example.com", e.Email));
        var manager = Assert.Single(employees, e => e.IsManager);
        Assert.Null(manager.ManagerId);
        Assert.All(employees.Where(e => !e.IsManager), e => Assert.Equal(manager.Id, e.ManagerId));
        Assert.Equal(3, employees.Count(e => !e.IsManager));

        var requests = await check.LeaveRequests.ToListAsync();
        Assert.Contains(requests, r => r.Status == LeaveStatus.Pending);
        Assert.Contains(requests, r => r.Status == LeaveStatus.Approved);
        Assert.Contains(requests, r => r.Status == LeaveStatus.Rejected);
        Assert.All(requests, r => Assert.True(r.LastDay >= r.FirstDay));
        Assert.All(requests.Where(r => r.Status != LeaveStatus.Pending), r =>
        {
            Assert.Equal(manager.Id, r.DecidedById);
            Assert.NotNull(r.DecidedAtUtc);
            Assert.True(r.DecidedAtUtc <= now);
        });
    }
}
