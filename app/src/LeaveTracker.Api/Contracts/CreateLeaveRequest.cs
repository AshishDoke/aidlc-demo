namespace LeaveTracker.Api.Contracts;

/// <summary>
/// Body of <c>POST /api/leave-requests</c>. Dates (<c>yyyy-MM-dd</c>) and type (<c>annual</c>, <c>sick</c>, <c>unpaid</c>)
/// are strings so bad input gets a plain message instead of a generic binding error.
/// <see cref="EmployeeId"/> is optional. When it is given, it must be the picked person.
/// </summary>
public sealed record CreateLeaveRequest(
    string? FirstDay,
    string? LastDay,
    string? Type,
    string? Reason,
    int? EmployeeId);
