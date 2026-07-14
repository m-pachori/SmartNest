namespace SmartNest.Shared.Security;

/// <summary>
/// Thrown when an authenticated caller does not have permission to perform the
/// requested operation (maps to HTTP 403 in Function response mapping).
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
