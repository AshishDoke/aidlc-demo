using LeaveTracker.Api.Data;
using LeaveTracker.Api.Services;

namespace LeaveTracker.Api.Contracts;

public sealed record LeaveRequestResponse(
    int Id,
    int EmployeeId,
    string EmployeeName,
    DateOnly FirstDay,
    DateOnly LastDay,
    int DaysCovered,
    LeaveType Type,
    string? Reason,
    LeaveStatus Status,
    int? DecidedById,
    string? DecidedByName,
    DateTime? DecidedAtUtc,
    string? DecisionComment)
{
    /// <summary>Maps a request loaded with its <c>Employee</c> and <c>DecidedBy</c> navigations.</summary>
    public static LeaveRequestResponse From(LeaveRequest r) => new(
        r.Id,
        r.EmployeeId,
        r.Employee?.Name ?? string.Empty,
        r.FirstDay,
        r.LastDay,
        LeaveRequestService.DaysCovered(r.FirstDay, r.LastDay),
        r.Type,
        r.Reason,
        r.Status,
        r.DecidedById,
        r.DecidedBy?.Name,
        r.DecidedAtUtc,
        r.DecisionComment);
}
