namespace Candidate.API.Services;

public enum ServiceErrorKind
{
    NotFound,
    Conflict,
    Validation
}

/// <summary>An expected business failure. Controllers translate it to an HTTP problem response.</summary>
/// <param name="FieldErrors">Keyed by the JSON property name, so the UI can show each message beside its field.</param>
public sealed record ServiceError(
    ServiceErrorKind Kind,
    string Message,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null)
{
    public static ServiceError NotFound(string message) => new(ServiceErrorKind.NotFound, message);

    public static ServiceError Conflict(string message) => new(ServiceErrorKind.Conflict, message);

    public static ServiceError Validation(string field, string message) =>
        new(ServiceErrorKind.Validation, message, new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>Either a value or a <see cref="ServiceError"/>. Keeps expected failures out of exception handling.</summary>
public readonly record struct ServiceResult<T>(T? Value, ServiceError? Error)
{
    public bool IsSuccess => Error is null;

    public static implicit operator ServiceResult<T>(T value) => new(value, null);

    public static implicit operator ServiceResult<T>(ServiceError error) => new(default, error);
}
