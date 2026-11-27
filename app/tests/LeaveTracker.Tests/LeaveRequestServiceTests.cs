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

    private async Task<(int OtherManagerId, int OtherReportId)> AddOtherTeamAsync()
    {
        await using var db = _testDb.CreateContext();
        var employees = new EmployeeService(db);
        var otherManager = await employees.AddAsync(new NewEmployee("Other Boss", "other.boss@example.com", true, null));
        var otherReport = await employees.AddAsync(new NewEmployee("Other Report", "other.report@example.com", false, otherManager.Id));
        return (otherManager.Id, otherReport.Id);
    }

    private async Task<RuleViolationException> DecisionRefusedAsync(int requestId, int deciderId, bool approve, string? comment)
    {
        await using var db = _testDb.CreateContext();
        return await Assert.ThrowsAsync<RuleViolationException>(() =>
            new LeaveRequestService(db).RecordDecisionAsync(requestId, deciderId, approve, comment));
    }

    private async Task<LeaveStatus> StatusOfAsync(int requestId)
    {
        await using var db = _testDb.CreateContext();
        return (await db.LeaveRequests.SingleAsync(r => r.Id == requestId)).Status;
    }

    // LT-002 AC5: an employee can only ask for leave for themselves.
    [Fact]
    public async Task Asking_for_someone_else_is_forbidden()
    {
        await using var db = _testDb.CreateContext();
        var ex = await Assert.ThrowsAsync<RuleViolationException>(() =>
            new LeaveRequestService(db).AskForSelfAsync(_managerId, new NewLeaveRequest(_reportId, Day, Day, LeaveType.Annual, null)));

        Assert.Equal(RuleKind.Forbidden, ex.Kind);
        Assert.Contains("only ask for leave for yourself", ex.Errors["employeeId"].Single());
        Assert.Equal(0, await db.LeaveRequests.CountAsync());
    }

    [Fact]
    public async Task Asking_for_self_is_pending()
    {
        await using var db = _testDb.CreateContext();
        var created = await new LeaveRequestService(db).AskForSelfAsync(_reportId, new NewLeaveRequest(_reportId, Day, Day, LeaveType.Annual, null));

        Assert.Equal(LeaveStatus.Pending, created.Status);
    }

    // LT-002 AC10: a rejection needs a reason; an approval comment is optional.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejection_without_reason_is_refused(string? reason)
    {
        var request = await AddAsync(new NewLeaveRequest(_reportId, Day, Day, LeaveType.Annual, null));

        var ex = await DecisionRefusedAsync(request.Id, _managerId, approve: false, reason);

        Assert.Equal(RuleKind.Validation, ex.Kind);
        Assert.Contains("rejection needs a reason", ex.Errors["reason"].Single());
        Assert.Equal(LeaveStatus.Pending, await StatusOfAsync(request.Id));
    }

    [Fact]
    public async Task Approval_without_comment_uses_clock_time()
    {
        var request = await AddAsync(new NewLeaveRequest(_reportId, Day, Day, LeaveType.Annual, null));
        var now = new DateTimeOffset(2026, 11, 27, 10, 15, 0, TimeSpan.Zero);

        await using var db = _testDb.CreateContext();
        var decided = await new LeaveRequestService(db, new FixedClock(now)).RecordDecisionAsync(request.Id, _managerId, true, null);

        Assert.Equal(LeaveStatus.Approved, decided.Status);
        Assert.Null(decided.DecisionComment);
        Assert.Equal(now.UtcDateTime, decided.DecidedAtUtc);
    }

    // LT-002 AC11: a manager cannot decide a request from outside their team, including their own request.
    [Fact]
    public async Task Request_outside_team_is_forbidden()
    {
        var (otherManagerId, otherReportId) = await AddOtherTeamAsync();
        var otherTeamRequest = await AddAsync(new NewLeaveRequest(otherReportId, Day, Day, LeaveType.Annual, null));
        var ownRequest = await AddAsync(new NewLeaveRequest(_managerId, Day, Day, LeaveType.Annual, null));

        var ex1 = await DecisionRefusedAsync(otherTeamRequest.Id, _managerId, approve: true, null);
        var ex2 = await DecisionRefusedAsync(ownRequest.Id, _managerId, approve: false, "No");
        var ex3 = await DecisionRefusedAsync(ownRequest.Id, otherManagerId, approve: true, null);

        Assert.All(new[] { ex1, ex2, ex3 }, ex =>
        {
            Assert.Equal(RuleKind.Forbidden, ex.Kind);
            Assert.Contains("not in your team", ex.Errors["requestId"].Single());
        });
        Assert.Equal(LeaveStatus.Pending, await StatusOfAsync(otherTeamRequest.Id));
        Assert.Equal(LeaveStatus.Pending, await StatusOfAsync(ownRequest.Id));
    }

    // LT-002 AC12: a decided request cannot be decided again; the message says how and by whom.
    [Fact]
    public async Task Already_decided_is_a_conflict_naming_the_decision()
    {
        var request = await AddAsync(new NewLeaveRequest(_reportId, Day, Day, LeaveType.Annual, null));
        await using (var db = _testDb.CreateContext())
        {
            await new LeaveRequestService(db).RecordDecisionAsync(request.Id, _managerId, true, "Enjoy");
        }

        var ex = await DecisionRefusedAsync(request.Id, _managerId, approve: false, "Changed my mind");

        Assert.Equal(RuleKind.Conflict, ex.Kind);
        Assert.Contains("already approved by Boss", ex.Errors["status"].Single());
        await using var check = _testDb.CreateContext();
        var stored = await check.LeaveRequests.SingleAsync(r => r.Id == request.Id);
        Assert.Equal(LeaveStatus.Approved, stored.Status);
        Assert.Equal("Enjoy", stored.DecisionComment);
    }

    // LT-002 AC13: deciding a request that does not exist.
    [Fact]
    public async Task Deciding_unknown_request_is_not_found()
    {
        var ex = await DecisionRefusedAsync(999, _managerId, approve: true, null);

        Assert.Equal(RuleKind.NotFound, ex.Kind);
        Assert.Contains("No leave request with id 999", ex.Errors["requestId"].Single());
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // LT-002 AC14 / Q4: calendar days, first and last day included.
    [Theory]
    [InlineData("2026-12-14", "2026-12-14", 1)]
    [InlineData("2026-12-14", "2026-12-16", 3)]
    [InlineData("2026-11-30", "2026-12-01", 2)]
    [InlineData("2026-12-31", "2027-01-01", 2)]
    [InlineData("2028-02-28", "2028-03-01", 3)]
    public void Days_covered_is_inclusive_calendar_days(string first, string last, int expected)
    {
        Assert.Equal(expected, LeaveRequestService.DaysCovered(DateOnly.Parse(first), DateOnly.Parse(last)));
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
