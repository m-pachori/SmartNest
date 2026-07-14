namespace SmartNest.Shared.Security;

/// <summary>
/// Thrown when the caller's identity cannot be established (missing/malformed bearer
/// token). Maps to HTTP 401 in Function response mapping.
/// </summary>
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message)
    {
    }

    public UnauthorizedException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
