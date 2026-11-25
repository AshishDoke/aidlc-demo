using LeaveTracker.Api.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LeaveTracker.Api.Services;

/// <summary>Input for adding an employee.</summary>
public sealed record NewEmployee(string? Name, string? Email, bool IsManager, int? ManagerId);

/// <summary>The only way to add employees. Enforces the employee rules from LT-001.</summary>
public sealed class EmployeeService(LeaveDbContext db)
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;

    public const string MustReportToOneManager =
        "An employee who is not a manager must report to exactly one manager.";

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public async Task<Employee> AddAsync(NewEmployee input, CancellationToken ct = default)
    {
        var errors = new RuleErrors();

        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            errors.Add("name", "Name is required.");
        }
        else if (name.Length > NameMaxLength)
        {
            errors.Add("name", $"Name must be {NameMaxLength} characters or fewer.");
        }

        var email = input.Email?.Trim() ?? string.Empty;
        var normalizedEmail = NormalizeEmail(email);
        if (email.Length == 0)
        {
            errors.Add("email", "Email is required.");
        }
        else if (email.Length > EmailMaxLength)
        {
            errors.Add("email", $"Email must be {EmailMaxLength} characters or fewer.");
        }
        else if (!LooksLikeEmail(email))
        {
            errors.Add("email", "Email must look like name@example.com.");
        }
        else if (await db.Employees.AnyAsync(e => e.NormalizedEmail == normalizedEmail, ct))
        {
            errors.Add("email", $"The email address '{email}' is already in use by another employee.");
        }

        await CheckManagerAsync(input, errors, ct);

        errors.ThrowIfAny();

        var employee = new Employee
        {
            Name = name,
            Email = email,
            NormalizedEmail = normalizedEmail,
            IsManager = input.IsManager,
            ManagerId = input.ManagerId,
        };
        db.Employees.Add(employee);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            // Unique index on NormalizedEmail: another add with the same email won the race.
            db.Entry(employee).State = EntityState.Detached;
            throw new RuleViolationException(
                "email", $"The email address '{email}' is already in use by another employee.");
        }

        return employee;
    }

    private async Task CheckManagerAsync(NewEmployee input, RuleErrors errors, CancellationToken ct)
    {
        if (input.ManagerId is null)
        {
            if (!input.IsManager)
            {
                errors.Add("managerId", MustReportToOneManager);
            }

            return;
        }

        var manager = await db.Employees.AsNoTracking()
            .Where(e => e.Id == input.ManagerId)
            .Select(e => new { e.Name, e.IsManager })
            .SingleOrDefaultAsync(ct);

        if (manager is null)
        {
            errors.Add("managerId", $"{MustReportToOneManager} No employee with id {input.ManagerId} exists.");
        }
        else if (!manager.IsManager)
        {
            errors.Add("managerId", $"{MustReportToOneManager} {manager.Name} is not a manager.");
        }
    }

    private static bool LooksLikeEmail(string email)
    {
        var at = email.IndexOf('@');
        return at > 0
            && at == email.LastIndexOf('@')
            && at < email.Length - 1
            && !email.Any(char.IsWhiteSpace);
    }
}
