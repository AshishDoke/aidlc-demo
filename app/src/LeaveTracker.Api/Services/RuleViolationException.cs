namespace LeaveTracker.Api.Services;

/// <summary>What kind of rule was broken. The API maps each kind to a status code.</summary>
public enum RuleKind
{
    /// <summary>The input is missing, malformed or breaks a rule about its content (400).</summary>
    Validation,

    /// <summary>The thing the caller refers to does not exist (404).</summary>
    NotFound,

    /// <summary>The caller is not allowed to do this (403).</summary>
    Forbidden,

    /// <summary>The action is not possible in the current state, for example already decided (409).</summary>
    Conflict,
}

/// <summary>
/// Thrown when input breaks a business rule. <see cref="Errors"/> maps a field name to plain-words
/// messages, in the same shape as <c>Results.ValidationProblem</c> expects.
/// </summary>
public sealed class RuleViolationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public RuleKind Kind { get; }

    public RuleViolationException(IDictionary<string, string[]> errors, RuleKind kind = RuleKind.Validation)
        : base(string.Join(" ", errors.SelectMany(e => e.Value)))
    {
        Errors = new Dictionary<string, string[]>(errors);
        Kind = kind;
    }

    public RuleViolationException(string field, string message, RuleKind kind = RuleKind.Validation)
        : this(new Dictionary<string, string[]> { [field] = [message] }, kind)
    {
    }
}

/// <summary>Collects rule violations per field before throwing them together.</summary>
internal sealed class RuleErrors
{
    private readonly Dictionary<string, List<string>> _errors = new();

    public bool Any => _errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
        {
            list = [];
            _errors[field] = list;
        }

        list.Add(message);
    }

    public void ThrowIfAny()
    {
        if (Any)
        {
            throw new RuleViolationException(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}
