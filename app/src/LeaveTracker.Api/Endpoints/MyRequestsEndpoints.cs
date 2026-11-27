using LeaveTracker.Api.Contracts;
using LeaveTracker.Api.Data;
using LeaveTracker.Api.Services;

namespace LeaveTracker.Api.Endpoints;

/// <summary>Lists that depend on who you are (LT-002): your own requests, and a manager's team pending requests.</summary>
public static class MyRequestsEndpoints
{
    public static IEndpointRouteBuilder MapMyRequestsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me/leave-requests", async (HttpContext http, LeaveDbContext db, LeaveRequestService service, CancellationToken ct) =>
        {
            var (me, whoError) = await CurrentEmployee.ResolveAsync(http, db, ct);
            if (me is null)
            {
                return whoError!;
            }

            var requests = await service.GetForEmployeeAsync(me.Id, ct);
            return Results.Ok(requests.Select(LeaveRequestResponse.From));
        });

        app.MapGet("/api/team/leave-requests/pending", async (HttpContext http, LeaveDbContext db, LeaveRequestService service, CancellationToken ct) =>
        {
            var (me, whoError) = await CurrentEmployee.ResolveAsync(http, db, ct);
            if (me is null)
            {
                return whoError!;
            }

            try
            {
                var requests = await service.GetPendingForTeamAsync(me.Id, ct);
                return Results.Ok(requests.Select(LeaveRequestResponse.From));
            }
            catch (RuleViolationException ex)
            {
                return ex.ToResult();
            }
        });

        return app;
    }
}
