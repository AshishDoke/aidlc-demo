namespace LeaveTracker.Api.Contracts;

/// <summary>Body of <c>POST /api/leave-requests/{id}/approve</c>. The body itself and the comment are optional.</summary>
public sealed record ApproveRequest(string? Comment);

/// <summary>Body of <c>POST /api/leave-requests/{id}/reject</c>. A non-blank reason is required.</summary>
public sealed record RejectRequest(string? Reason);
