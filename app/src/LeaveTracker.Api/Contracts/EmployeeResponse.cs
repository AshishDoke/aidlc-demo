namespace LeaveTracker.Api.Contracts;

public sealed record EmployeeResponse(
    int Id,
    string Name,
    string Email,
    bool IsManager,
    int? ManagerId,
    string? ManagerName);
