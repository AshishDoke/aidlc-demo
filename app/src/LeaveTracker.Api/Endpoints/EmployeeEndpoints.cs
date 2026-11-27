using LeaveTracker.Api.Contracts;
using LeaveTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Api.Endpoints;

/// <summary>Read-only employee endpoints (LT-001).</summary>
public static class EmployeeEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/employees");

        group.MapGet("/", async (LeaveDbContext db, CancellationToken ct) =>
        {
            var employees = await Project(db.Employees.AsNoTracking()
                    .OrderBy(e => e.Name)
                    .ThenBy(e => e.Id))
                .ToListAsync(ct);
            return Results.Ok(employees);
        });

        group.MapGet("/{id:int}", async (int id, LeaveDbContext db, CancellationToken ct) =>
        {
            var employee = await Project(db.Employees.AsNoTracking().Where(e => e.Id == id))
                .SingleOrDefaultAsync(ct);
            return employee is null
                ? Results.Problem(
                    title: "Employee not found",
                    detail: $"No employee with id {id}.",
                    statusCode: StatusCodes.Status404NotFound)
                : Results.Ok(employee);
        });

        return app;
    }

    private static IQueryable<EmployeeResponse> Project(IQueryable<Employee> query) =>
        query.Select(e => new EmployeeResponse(
            e.Id,
            e.Name,
            e.Email,
            e.IsManager,
            e.ManagerId,
            e.Manager == null ? null : e.Manager.Name));
}
