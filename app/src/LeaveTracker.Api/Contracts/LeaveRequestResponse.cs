using LeaveTracker.Api.Data;

namespace LeaveTracker.Api.Contracts;

public sealed record LeaveRequestResponse(
    int Id,
    int EmployeeId,
    string EmployeeName,
    DateOnly FirstDay,
    DateOnly LastDay,
    LeaveType Type,
    string? Reason,
    LeaveStatus Status,
    int? DecidedById,
    string? DecidedByName,
    DateTime? DecidedAtUtc,
    string? DecisionComment);
