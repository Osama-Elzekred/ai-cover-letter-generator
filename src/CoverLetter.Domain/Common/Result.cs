namespace CoverLetter.Domain.Common;

/// <summary>
/// Represents the type/category of a result for proper HTTP mapping.
/// </summary>
public enum ResultType
{
  Success,
  Error,
  NotFound,
  ValidationError,
  Unauthorized,
  Forbidden,
  Conflict
}

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// Supports multiple error messages for validation scenarios.
/// </summary>
public class Result
{
  public bool IsSuccess { get; }
  public bool IsFailure => !IsSuccess;
  public ResultType Type { get; }
  public IReadOnlyList<string> Errors { get; }

  /// <summary>
  /// Gets the first error message, or empty string if no errors.
  /// </summary>
  public string Error => Errors.FirstOrDefault() ?? string.Empty;

  protected Result(bool isSuccess, ResultType type, IEnumerable<string>? errors = null)
  {
    IsSuccess = isSuccess;
    Type = type;
    Errors = errors?.ToList().AsReadOnly() ?? [];
  }

  public static Result Success() => new(true, ResultType.Success);
  public static Result Failure(string error) => new(false, ResultType.Error, [error]);
  public static Result Failure(IEnumerable<string> errors) => new(false, ResultType.Error, errors);

  public static Result<T> Success<T>(T value) => new(value, true, ResultType.Success);
  public static Result<T> Failure<T>(string error, ResultType type = ResultType.Error)
      => new(default!, false, type, [error]);
  public static Result<T> Failure<T>(IEnumerable<string> errors, ResultType type = ResultType.Error)
      => new(default!, false, type, errors);
}

/// <summary>
/// Represents the result of an operation that returns a value.
/// </summary>
public class Result<T> : Result
{
  public T Value { get; }

  protected internal Result(T value, bool isSuccess, ResultType type, IEnumerable<string>? errors = null)
      : base(isSuccess, type, errors)
  {
    Value = value;
  }

  // Convenience factory methods
  public static Result<T> NotFound(string error = "Resource not found")
      => new(default!, false, ResultType.NotFound, [error]);

  public static Result<T> ValidationErrors(IEnumerable<string> errors)
      => new(default!, false, ResultType.ValidationError, errors);

  public static Result<T> ValidationError(string error)
      => new(default!, false, ResultType.ValidationError, [error]);

  public static Result<T> Unauthorized(string error = "Unauthorized access")
      => new(default!, false, ResultType.Unauthorized, [error]);

  public static Result<T> Forbidden(string error = "Access forbidden")
      => new(default!, false, ResultType.Forbidden, [error]);

  public static Result<T> Conflict(string error)
      => new(default!, false, ResultType.Conflict, [error]);
}
