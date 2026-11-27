using LeaveTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Api.Services;

/// <summary>Input for recording a new leave request.</summary>
public sealed record NewLeaveRequest(int EmployeeId, DateOnly? FirstDay, DateOnly? LastDay, LeaveType? Type, string? Reason);

/// <summary>The only way to add leave requests and record decisions. Enforces the LT-001 rules.</summary>
public sealed class LeaveRequestService(LeaveDbContext db)
{
    public const int TextMaxLength = 500;

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
    /// Records a manager's decision on a pending request. Used by the demo seeder in LT-001.
    /// LT-002 will expose it to managers.
    /// </summary>
    internal async Task<LeaveRequest> RecordDecisionAsync(
        int requestId, int deciderId, bool approve, string? comment, DateTime decidedAtUtc, CancellationToken ct = default)
    {
        var request = await db.LeaveRequests.SingleOrDefaultAsync(r => r.Id == requestId, ct)
            ?? throw new RuleViolationException("requestId", $"No leave request with id {requestId} exists.");

        var errors = new RuleErrors();

        if (request.Status != LeaveStatus.Pending)
        {
            errors.Add("status", $"This request is already {request.Status.ToString().ToLowerInvariant()}; only pending requests can be decided.");
        }

        var decider = await db.Employees.AsNoTracking()
            .Where(e => e.Id == deciderId)
            .Select(e => new { e.Name, e.IsManager })
            .SingleOrDefaultAsync(ct);
        if (decider is null)
        {
            errors.Add("decidedById", $"No employee with id {deciderId} exists.");
        }
        else if (!decider.IsManager)
        {
            errors.Add("decidedById", $"{decider.Name} is not a manager, so they cannot decide on leave requests.");
        }

        var trimmedComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (trimmedComment is { Length: > TextMaxLength })
        {
            errors.Add("comment", $"Comment must be {TextMaxLength} characters or fewer.");
        }

        errors.ThrowIfAny();

        request.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        request.DecidedById = deciderId;
        request.DecidedAtUtc = decidedAtUtc.Kind == DateTimeKind.Utc
            ? decidedAtUtc
            : DateTime.SpecifyKind(decidedAtUtc.ToUniversalTime(), DateTimeKind.Utc);
        request.DecisionComment = trimmedComment;
        await db.SaveChangesAsync(ct);
        return request;
    }
}
