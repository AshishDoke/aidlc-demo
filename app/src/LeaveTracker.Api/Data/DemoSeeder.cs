using LeaveTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Api.Data;

/// <summary>
/// Creates the fictional demo team the first time the app starts. Does nothing when any
/// employee already exists, so restarting never duplicates it.
/// </summary>
public static class DemoSeeder
{
    public static async Task SeedAsync(LeaveDbContext db, DateTime? nowUtc = null, CancellationToken ct = default)
    {
        if (await db.Employees.AnyAsync(ct))
        {
            return;
        }

        var now = nowUtc ?? DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var employees = new EmployeeService(db);
        var maya = await employees.AddAsync(new NewEmployee("Maya Manager", "maya.manager@example.com", true, null), ct);
        var alex = await employees.AddAsync(new NewEmployee("Alex Turner", "alex.turner@example.com", false, maya.Id), ct);
        var priya = await employees.AddAsync(new NewEmployee("Priya Shah", "priya.shah@example.com", false, maya.Id), ct);
        var sam = await employees.AddAsync(new NewEmployee("Sam Lee", "sam.lee@example.com", false, maya.Id), ct);

        var requests = new LeaveRequestService(db);

        await requests.AddAsync(
            new NewLeaveRequest(alex.Id, today.AddDays(14), today.AddDays(18), LeaveType.Annual, "Family holiday"), ct);

        var sick = await requests.AddAsync(
            new NewLeaveRequest(priya.Id, today.AddDays(-7), today.AddDays(-6), LeaveType.Sick, "Flu"), ct);
        await requests.RecordDecisionAsync(sick.Id, maya.Id, approve: true, "Get well soon.", now.AddDays(-8), ct);

        var unpaid = await requests.AddAsync(
            new NewLeaveRequest(sam.Id, today.AddDays(30), today.AddDays(30), LeaveType.Unpaid, "Moving house"), ct);
        await requests.RecordDecisionAsync(
            unpaid.Id, maya.Id, approve: false, "Busy release week, please pick another day.", now.AddDays(-1), ct);

        var annual = await requests.AddAsync(
            new NewLeaveRequest(sam.Id, today.AddDays(45), today.AddDays(49), LeaveType.Annual, null), ct);
        await requests.RecordDecisionAsync(annual.Id, maya.Id, approve: true, null, now.AddDays(-1), ct);

        await transaction.CommitAsync(ct);
    }
}
