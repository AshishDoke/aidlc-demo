using System.Globalization;
using LeaveTracker.Api.Contracts;
using LeaveTracker.Api.Data;
using LeaveTracker.Api.Services;
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

        group.MapPost("/", async (CreateLeaveRequest body, HttpContext http, LeaveDbContext db, LeaveRequestService service, CancellationToken ct) =>
        {
            var (me, whoError) = await CurrentEmployee.ResolveAsync(http, db, ct);
            if (me is null)
            {
                return whoError!;
            }

            var parseErrors = new Dictionary<string, string[]>();
            var firstDay = ParseDay(body.FirstDay, "firstDay", "First day", parseErrors);
            var lastDay = ParseDay(body.LastDay, "lastDay", "Last day", parseErrors);
            var type = ParseType(body.Type, parseErrors);
            if (parseErrors.Count > 0)
            {
                return Results.ValidationProblem(parseErrors, title: "Please correct the request");
            }

            try
            {
                var created = await service.AskForSelfAsync(
                    me.Id, new NewLeaveRequest(body.EmployeeId ?? me.Id, firstDay, lastDay, type, body.Reason), ct);
                var loaded = await WithPeople(db).SingleAsync(r => r.Id == created.Id, ct);
                return Results.Created($"/api/leave-requests/{created.Id}", LeaveRequestResponse.From(loaded));
            }
            catch (RuleViolationException ex)
            {
                return ex.ToResult();
            }
        });

        return app;
    }

    private static DateOnly? ParseDay(string? value, string field, string label, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null; // the service reports "is required"
        }

        if (DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return day;
        }

        errors[field] = [$"{label} must be a date like 2026-12-14 (year-month-day)."];
        return null;
    }

    private static LeaveType? ParseType(string? value, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null; // the service reports the allowed types
        }

        var text = value.Trim();
        if (text.All(char.IsLetter) && Enum.TryParse<LeaveType>(text, ignoreCase: true, out var type))
        {
            return type;
        }

        errors["type"] = ["Leave type must be annual, sick or unpaid."];
        return null;
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
