namespace UpgradePlanner.Api.Services;

/// <summary>How an operation ended, in terms the domain understands.</summary>
/// <remarks>
/// Deliberately not <c>ActionResult</c>. The service layer decides *what
/// happened*; the controller decides *which status code says so*. Keeping those
/// apart is what lets the rules be tested without spinning up HTTP.
/// </remarks>
public enum ServiceStatus
{
    Ok,

    /// <summary>The addressed upgrade does not exist. Maps to 404.</summary>
    NotFound,

    /// <summary>A business rule rejected the request. Maps to 400 with <see cref="ServiceResult{T}.Message"/>.</summary>
    Invalid,
}

public sealed record ServiceResult<T>(ServiceStatus Status, T? Value, string? Message)
{
    public static ServiceResult<T> Ok(T value) => new(ServiceStatus.Ok, value, null);

    public static ServiceResult<T> NotFound() => new(ServiceStatus.NotFound, default, null);

    public static ServiceResult<T> Invalid(string message) => new(ServiceStatus.Invalid, default, message);
}
