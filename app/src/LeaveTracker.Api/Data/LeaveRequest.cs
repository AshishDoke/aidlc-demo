namespace LeaveTracker.Api.Data;

/// <summary>One request for leave over a continuous range of calendar days (both days included).</summary>
public class LeaveRequest
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public DateOnly FirstDay { get; set; }

    public DateOnly LastDay { get; set; }

    public LeaveType Type { get; set; }

    public string? Reason { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public int? DecidedById { get; set; }

    public Employee? DecidedBy { get; set; }

    public DateTime? DecidedAtUtc { get; set; }

    public string? DecisionComment { get; set; }
}
