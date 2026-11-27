using LeaveTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Api.Services;

/// <summary>Input for recording a new leave request.</summary>
public sealed record NewLeaveRequest(int EmployeeId, DateOnly? FirstDay, DateOnly? LastDay, LeaveType? Type, string? Reason);

/// <summary>
/// The only way to add leave requests and record decisions. Enforces the LT-001 rules for keeping requests
/// and the LT-002 rules for asking (self only) and deciding (own team, pending only, rejection needs a reason).
/// </summary>
public sealed class LeaveRequestService(LeaveDbContext db, TimeProvider? clock = null)
{
    public const int TextMaxLength = 500;

    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Calendar days from first to last day, both included (a single day is 1).</summary>
    public static int DaysCovered(DateOnly firstDay, DateOnly lastDay) => lastDay.DayNumber - firstDay.DayNumber + 1;

    /// <summary>
    /// An employee asks for leave for themselves (LT-002). Refused when the request names someone else.
    /// </summary>
    public Task<LeaveRequest> AskForSelfAsync(int currentEmployeeId, NewLeaveRequest input, CancellationToken ct = default)
    {
        if (input.EmployeeId != currentEmployeeId)
        {
            throw new RuleViolationException(
                "employeeId", "You can only ask for leave for yourself.", RuleKind.Forbidden);
        }

        return AddAsync(input, ct);
    }

    /// <summary>Records a new request. It always starts as Pending with no decision.</summary>
    public async Task<LeaveRequest> AddAsync(NewLeaveRequest input, CancellationToken ct = default)
    {
        var errors = new RuleErrors();

        if (!await db.Employees.AnyAsync(e => e.Id == input.EmployeeId, ct))
        {
            errors.Add("employeeId", $"No employee with id {input.EmployeeId} exists, so the request has no valid requester.");
        }

        if (input.FirstDay is null)
        {
            errors.Add("firstDay", "First day is required.");
        }

        if (input.LastDay is null)
        {
            errors.Add("lastDay", "Last day is required.");
        }

        if (input.FirstDay is { } first && input.LastDay is { } last && last < first)
        {
            errors.Add("lastDay", $"The last day ({last:yyyy-MM-dd}) is before the first day ({first:yyyy-MM-dd}).");
        }

        if (input.Type is null || !Enum.IsDefined(input.Type.Value))
        {
            errors.Add("type", "Leave type must be annual, sick or unpaid.");
        }

        var reason = string.IsNullOrWhiteSpace(input.Reason) ? null : input.Reason.Trim();
        if (reason is { Length: > TextMaxLength })
        {
            errors.Add("reason", $"Reason must be {TextMaxLength} characters or fewer.");
        }

        errors.ThrowIfAny();

        var request = new LeaveRequest
        {
            EmployeeId = input.EmployeeId,
            FirstDay = input.FirstDay!.Value,
            LastDay = input.LastDay!.Value,
            Type = input.Type!.Value,
            Reason = reason,
            Status = LeaveStatus.Pending,
        };
        db.LeaveRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return request;
    }

    /// <summary>
    /// A manager approves or rejects a pending request from their own team. Rules are checked in this order,
    /// and the first failure wins: request exists (NotFound), decider is a manager (Forbidden), requester reports to the
    /// decider (Forbidden), request is pending (Conflict), a rejection has a reason and text is at most 500 (Validation).
    /// </summary>
    /// <param name="decidedAtUtc">The decision time. Defaults to now. The demo seeder passes past times.</param>
    public async Task<LeaveRequest> RecordDecisionAsync(
        int requestId, int deciderId, bool approve, string? comment, DateTime? decidedAtUtc = null, CancellationToken ct = default)
    {
        var request = await db.LeaveRequests
            .Include(r => r.Employee)
            .Include(r => r.DecidedBy)
            .SingleOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new RuleViolationException("requestId", $"No leave request with id {requestId}.", RuleKind.NotFound);

        var decider = await db.Employees.AsNoTracking()
            .Where(e => e.Id == deciderId)
            .Select(e => new { e.Name, e.IsManager })
            .SingleOrDefaultAsync(ct);
        if (decider is null)
        {
            throw new RuleViolationException("decidedById", $"No employee with id {deciderId} exists.");
        }

        if (!decider.IsManager)
        {
            throw new RuleViolationException(
                "decidedById", $"{decider.Name} is not a manager, so they cannot decide on leave requests.", RuleKind.Forbidden);
        }

        if (request.Employee?.ManagerId != deciderId)
        {
            throw new RuleViolationException(
                "requestId",
                $"This request is from {request.Employee?.Name}, who is not in your team. You can only decide on requests from your own team.",
                RuleKind.Forbidden);
        }

        if (request.Status != LeaveStatus.Pending)
        {
            var how = request.Status.ToString().ToLowerInvariant();
            var by = request.DecidedBy is null ? string.Empty : $" by {request.DecidedBy.Name}";
            throw new RuleViolationException(
                "status", $"This request was already {how}{by}; only pending requests can be decided.", RuleKind.Conflict);
        }

        var errors = new RuleErrors();
        var trimmedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (!approve && trimmedComment is null)
        {
            errors.Add("reason", "A rejection needs a reason. Please say why the request is rejected.");
        }

        if (trimmedComment is { Length: > TextMaxLength })
        {
            errors.Add(approve ? "comment" : "reason", $"{(approve ? "Comment" : "Reason")} must be {TextMaxLength} characters or fewer.");
        }

        errors.ThrowIfAny();

        var at = decidedAtUtc ?? _clock.GetUtcNow().UtcDateTime;
        request.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        request.DecidedById = deciderId;
        request.DecidedAtUtc = at.Kind == DateTimeKind.Utc ? at : DateTime.SpecifyKind(at.ToUniversalTime(), DateTimeKind.Utc);
        request.DecisionComment = trimmedComment;
        await db.SaveChangesAsync(ct);
        return request;
    }
}
