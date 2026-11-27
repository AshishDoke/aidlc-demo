using LeaveTracker.Api.Contracts;
using LeaveTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Api.Endpoints;

/// <summary>Read-only leave request endpoints (LT-001). Asking for and deciding on leave comes in LT-002.</summary>
public static class LeaveRequestEndpoints
{
    public static IEndpointRouteBuilder MapLeaveRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leave-requests");

        group.MapGet("/", async (LeaveDbContext db, CancellationToken ct) =>
        {
            var requests = await Project(db.LeaveRequests.AsNoTracking()
                    .OrderBy(r => r.FirstDay)
                    .ThenBy(r => r.Id))
                .ToListAsync(ct);
            return Results.Ok(requests);
        });

        group.MapGet("/{id:int}", async (int id, LeaveDbContext db, CancellationToken ct) =>
        {
            var request = await Project(db.LeaveRequests.AsNoTracking().Where(r => r.Id == id))
                .SingleOrDefaultAsync(ct);
            return request is null
                ? Results.Problem(
                    title: "Leave request not found",
                    detail: $"No leave request with id {id}.",
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(request);
        });

        return app;
    }

    private static IQueryable<LeaveRequestResponse> Project(IQueryable<LeaveRequest> query) =>
        query.Select(r => new LeaveRequestResponse(
            r.Id,
            r.EmployeeId,
            r.Employee!.Name,
            r.FirstDay,
            r.LastDay,
            r.Type,
            r.Reason,
            r.Status,
            r.DecidedById,
            r.DecidedBy == null ? null : r.DecidedBy.Name,
            r.DecidedAtUtc,
            r.DecisionComment));
}
