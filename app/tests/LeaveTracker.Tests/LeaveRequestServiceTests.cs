using LeaveTracker.Api.Data;
using LeaveTracker.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Tests;

public class LeaveRequestServiceTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 12, 1);

    private readonly TestDb _testDb = new();
    private readonly int _managerId;
    private readonly int _reportId;

    public LeaveRequestServiceTests()
    {
        using var db = _testDb.CreateContext();
        var employees = new EmployeeService(db);
        _managerId = employees.AddAsync(new NewEmployee("Boss", "boss@example.com", true, null)).GetAwaiter().GetResult().Id;
        _reportId = employees.AddAsync(new NewEmployee("Report", "report@example.com", false, _managerId)).GetAwaiter().GetResult().Id;
    }

    public void Dispose() => _testDb.Dispose();

    private async Task<LeaveRequest> AddAsync(NewLeaveRequest input)
    {
        await using var db = _testDb.CreateContext();
        return await new LeaveRequestService(db).AddAsync(input);
    }

    private async Task<RuleViolationException> RefusedAsync(NewLeaveRequest input)
    {
        await using var db = _testDb.CreateContext();
        var ex = await Assert.ThrowsAsync<RuleViolationException>(() => new LeaveRequestService(db).AddAsync(input));
        Assert.Equal(0, await db.LeaveRequests.CountAsync());
        return ex;
    }

    // AC8: a new request is pending and has no decision.
    [Fact]
    public async Task New_request_is_pending_without_decision()
    {
        var created = await AddAsync(new NewLeaveRequest(_reportId, Day, Day.AddDays(2), LeaveType.Annual, "Family trip"));

        await using var db = _testDb.CreateContext();
        var stored = await db.LeaveRequests.SingleAsync(r => r.Id == created.Id);
        Assert.Equal(LeaveStatus.Pending, stored.Status);
        Assert.Equal(_reportId, stored.EmployeeId);
        Assert.Equal(Day, stored.FirstDay);
        Assert.Equal(Day.AddDays(2), stored.LastDay);
        Assert.Equal(LeaveType.Annual, stored.Type);
        Assert.Equal("Family trip", stored.Reason);
        Assert.Null(stored.DecidedById);
        Assert.Null(stored.DecidedAtUtc);
        Assert.Null(stored.DecisionComment);
    }

    // AC9: last day before first day is refused.
    [Fact]
    public async Task Last_day_before_first_day_is_refused()
    {
        var ex = await RefusedAsync(new NewLeaveRequest(_reportId, Day, Day.AddDays(-1), LeaveType.Sick, null));

        Assert.Contains("before the first day", ex.Errors["lastDay"].Single());
    }

    // AC9: a leave type other than annual, sick or unpaid is refused.
    [Fact]
    public async Task Undefined_leave_type_is_refused()
    {
        var ex = await RefusedAsync(new NewLeaveRequest(_reportId, Day, Day, (LeaveType)42, null));

        Assert.Contains("annual, sick or unpaid", ex.Errors["type"].Single());
    }

    // AC9: missing leave type is refused.
    [Fact]
    public async Task Missing_leave_type_is_refused()
    {
        var ex = await RefusedAsync(new NewLeaveRequest(_reportId, Day, Day, null, null));

        Assert.Contains("annual, sick or unpaid", ex.Errors["type"].Single());
    }

    // AC9: a request without a valid requester is refused.
    [Fact]
    public async Task Unknown_requester_is_refused()
    {
        var ex = await RefusedAsync(new NewLeaveRequest(999, Day, Day, LeaveType.Annual, null));

        Assert.Contains("no valid requester", ex.Errors["employeeId"].Single());
    }

    // AC9: missing days are refused.
    [Fact]
    public async Task Missing_days_are_refused()
    {
        var ex = await RefusedAsync(new NewLeaveRequest(_reportId, null, null, LeaveType.Annual, null));

        Assert.Contains("First day is required", ex.Errors["firstDay"].Single());
        Assert.Contains("Last day is required", ex.Errors["lastDay"].Single());
    }

    // Spec Q7: reason up to 500 characters.
    [Fact]
    public async Task Reason_over_500_chars_is_refused()
    {
        var ex = await RefusedAsync(new NewLeaveRequest(_reportId, Day, Day, LeaveType.Annual, new string('x', 501)));

        Assert.Contains("500 characters", ex.Errors["reason"].Single());
    }

    [Fact]
    public async Task Reason_is_optional_and_blank_is_stored_as_none()
    {
        var created = await AddAsync(new NewLeaveRequest(_reportId, Day, Day, LeaveType.Unpaid, "   "));

        Assert.Null(created.Reason);
    }

    // AC10: a single-day request is accepted.
    [Fact]
    public async Task Single_day_request_is_accepted()
    {
        var created = await AddAsync(new NewLeaveRequest(_reportId, Day, Day, LeaveType.Sick, null));

        Assert.Equal(created.FirstDay, created.LastDay);
        Assert.Equal(LeaveStatus.Pending, created.Status);
    }

    // Supports AC7: decisions are recorded with decider, UTC time and comment; only from pending; only by managers.
    [Fact]
    public async Task Decision_only_from_pending_and_by_a_manager()
    {
        var request = await AddAsync(new NewLeaveRequest(_reportId, Day, Day, LeaveType.Annual, null));
        var decidedAt = new DateTime(2026, 11, 20, 9, 30, 0, DateTimeKind.Utc);

        await using (var db = _testDb.CreateContext())
        {
            var byReport = await Assert.ThrowsAsync<RuleViolationException>(() =>
                new LeaveRequestService(db).RecordDecisionAsync(request.Id, _reportId, true, null, decidedAt));
            Assert.Contains("not a manager", byReport.Errors["decidedById"].Single());
        }

        await using (var db = _testDb.CreateContext())
        {
            var decided = await new LeaveRequestService(db).RecordDecisionAsync(request.Id, _managerId, false, " Too busy ", decidedAt);
            Assert.Equal(LeaveStatus.Rejected, decided.Status);
        }

        await using (var db = _testDb.CreateContext())
        {
            var stored = await db.LeaveRequests.SingleAsync(r => r.Id == request.Id);
            Assert.Equal(_managerId, stored.DecidedById);
            Assert.Equal(decidedAt, stored.DecidedAtUtc);
            Assert.Equal("Too busy", stored.DecisionComment);

            var again = await Assert.ThrowsAsync<RuleViolationException>(() =>
                new LeaveRequestService(db).RecordDecisionAsync(request.Id, _managerId, true, null, decidedAt));
            Assert.Contains("only pending requests can be decided", again.Errors["status"].Single());
        }
    }

    [Fact]
    public async Task Decision_comment_over_500_chars_is_refused()
    {
        var request = await AddAsync(new NewLeaveRequest(_reportId, Day, Day, LeaveType.Annual, null));

        await using var db = _testDb.CreateContext();
        var ex = await Assert.ThrowsAsync<RuleViolationException>(() =>
            new LeaveRequestService(db).RecordDecisionAsync(request.Id, _managerId, true, new string('x', 501), DateTime.UtcNow));
        Assert.Contains("500 characters", ex.Errors["comment"].Single());
    }
}
