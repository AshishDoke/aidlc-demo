namespace LeaveTracker.Api.Services;

/// <summary>
/// Thrown when input breaks a business rule. <see cref="Errors"/> maps a field name to plain-words
/// messages, in the same shape as <c>Results.ValidationProblem</c> expects.
/// </summary>
public sealed class RuleViolationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public RuleViolationException(IDictionary<string, string[]> errors)
        : base(string.Join(" ", errors.SelectMany(e => e.Value)))
    {
        Errors = new Dictionary<string, string[]>(errors);
    }

    public RuleViolationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
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
