using LeaveTracker.Api.Services;

namespace LeaveTracker.Api.Endpoints;

/// <summary>Translates a business rule violation into the HTTP result from project.md's API conventions.</summary>
public static class RuleResults
{
    public static IResult ToResult(this RuleViolationException ex) => ex.Kind switch
    {
        RuleKind.NotFound => Results.Problem(
            title: "Not found", detail: ex.Message, statusCode: StatusCodes.Status404NotFound),
        RuleKind.Forbidden => Results.Problem(
            title: "Not allowed", detail: ex.Message, statusCode: StatusCodes.Status403Forbidden),
        RuleKind.Conflict => Results.Problem(
            title: "Not possible right now", detail: ex.Message, statusCode: StatusCodes.Status409Conflict),
        _ => Results.ValidationProblem(
            ex.Errors.ToDictionary(e => e.Key, e => e.Value), detail: ex.Message, title: "Please correct the request"),
    };
}
