using CoverLetter.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace CoverLetter.Api.Extensions;

/// <summary>
/// Extension members for converting Result&lt;T&gt; to HTTP IResult responses.
/// Uses C# 14 extension members syntax.
/// </summary>
public static class ResultExtensions
{
  extension<T>(Result<T> result)
  {
    /// <summary>
    /// Converts this Result to an appropriate HTTP IResult response.
    /// </summary>
    public IResult ToHttpResult()
    {
      if (result.IsSuccess)
        return Results.Ok(result.Value);

      return result.Type switch
      {
        ResultType.NotFound => Results.NotFound(CreateProblemDetails(result.Errors, 404, "Not Found")),
        ResultType.ValidationError => Results.BadRequest(CreateProblemDetails(result.Errors, 400, "Validation Failed")),
        ResultType.Unauthorized => Results.Unauthorized(),
        ResultType.Forbidden => Results.Problem(CreateProblemDetails(result.Errors, 403, "Forbidden")),
        ResultType.Conflict => Results.Conflict(CreateProblemDetails(result.Errors, 409, "Conflict")),
        _ => Results.Problem(CreateProblemDetails(result.Errors, 500, "Internal Server Error"))
      };
    }

    /// <summary>
    /// Converts this Result to an HTTP IResult with a custom success status code.
    /// </summary>
    public IResult ToHttpResult(int successStatusCode)
    {
      if (result.IsFailure)
        return result.ToHttpResult();

      return successStatusCode switch
      {
        201 => Results.Created((string?)null, result.Value),
        202 => Results.Accepted(value: result.Value),
        204 => Results.NoContent(),
        _ => Results.Ok(result.Value)
      };
    }
  }

  /// <summary>
  /// Creates a ProblemDetails response with the error list.
  /// </summary>
  private static ProblemDetails CreateProblemDetails(IReadOnlyList<string> errors, int statusCode, string title) => new()
  {
    Status = statusCode,
    Title = title,
    Detail = errors.Count == 1 ? errors[0] : $"{errors.Count} errors occurred.",
    Extensions = { ["errors"] = errors }
  };
}
