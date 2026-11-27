using LeaveTracker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Api.Endpoints;

/// <summary>
/// Resolves "who you are" from the <c>X-Employee-Id</c> header.
/// DEMO LIMITATION: there is no login. The header is trusted as-is, so anyone can act as anyone (see project.md).
/// </summary>
public static class CurrentEmployee
{
    public const string HeaderName = "X-Employee-Id";

    public const string PickYourselfMessage =
        "Pick who you are from the employee list (send your employee id in the X-Employee-Id header).";

    /// <summary>Returns the picked employee, or a 400 problem result saying why nobody could be identified.</summary>
    public static async Task<(Employee? Employee, IResult? Error)> ResolveAsync(
        HttpContext http, LeaveDbContext db, CancellationToken ct)
    {
        var raw = http.Request.Headers[HeaderName].ToString().Trim();
        if (raw.Length == 0)
        {
            return (null, Problem(PickYourselfMessage));
        }

        if (!int.TryParse(raw, out var id))
        {
            return (null, Problem($"'{raw}' is not an employee id. {PickYourselfMessage}"));
        }

        var employee = await db.Employees.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id, ct);
        return employee is null
            ? (null, Problem($"There is no employee with id {id}. {PickYourselfMessage}"))
            : (employee, null);
    }

    private static IResult Problem(string detail) =>
        Results.Problem(title: "Who are you?", detail: detail, statusCode: StatusCodes.Status400BadRequest);
}
