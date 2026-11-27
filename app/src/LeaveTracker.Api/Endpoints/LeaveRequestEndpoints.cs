using LeaveTracker.Api.Contracts;
using LeaveTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Api.Endpoints;

/// <summary>Leave request endpoints. Reads are anonymous (LT-001, LT-002 AC13).</summary>
public static class LeaveRequestEndpoints
{
    public static IEndpointRouteBuilder MapLeaveRequestEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leave-requests");

        group.MapGet("/", async (LeaveDbContext db, CancellationToken ct) =>
        {
            var requests = await WithPeople(db)
                .OrderBy(r => r.FirstDay)
                .ThenBy(r => r.Id)
                .ToListAsync(ct);
            return Results.Ok(requests.Select(LeaveRequestResponse.From));
        });

        group.MapGet("/{id:int}", async (int id, LeaveDbContext db, CancellationToken ct) =>
        {
            var request = await WithPeople(db).SingleOrDefaultAsync(r => r.Id == id, ct);
            return request is null
                ? RequestNotFound(id)
                : Results.Ok(LeaveRequestResponse.From(request));
        });

        return app;
    }

    internal static IQueryable<LeaveRequest> WithPeople(LeaveDbContext db) =>
        db.LeaveRequests.AsNoTracking()
            .Include(r => r.Employee)
            .Include(r => r.DecidedBy);

    internal static IResult RequestNotFound(int id) =>
        Results.Problem(
            title: "Leave request not found",
            detail: $"No leave request with id {id}.",
            statusCode: StatusCodes.Status404NotFound);
}
