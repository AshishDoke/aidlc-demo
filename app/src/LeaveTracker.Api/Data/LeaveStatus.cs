namespace LeaveTracker.Api.Data;

/// <summary>Where a leave request is in its life: waiting for a decision, or decided.</summary>
public enum LeaveStatus
{
    Pending,
    Approved,
    Rejected,
}
