namespace LeaveTracker.Api.Data;

/// <summary>A person known to the system. Managers are employees with <see cref="IsManager"/> set.</summary>
public class Employee
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Email as entered (trimmed).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Trimmed, lower-cased email. Unique, so emails are unique ignoring case.</summary>
    public string NormalizedEmail { get; set; } = string.Empty;

    public bool IsManager { get; set; }

    /// <summary>The manager this employee reports to. Required for non-managers.</summary>
    public int? ManagerId { get; set; }

    public Employee? Manager { get; set; }
}
