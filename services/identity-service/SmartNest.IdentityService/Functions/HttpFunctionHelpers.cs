using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartNest.Shared.Security;

namespace SmartNest.IdentityService.Functions;

/// <summary>
/// Shared helpers for thin HTTP Function triggers: current-user extraction, JSON body
/// reading, and exception-to-HTTP-status mapping. Keeps Functions themselves limited to
/// route binding + delegating to a Handler (mirrors Home/Device Service - ADR-010).
/// </summary>
internal static class HttpFunctionHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Extracts and cryptographically validates the caller's identity from the
    /// <c>Authorization</c> header (signature/issuer/audience/expiry - see
    /// <see cref="IJwtValidator"/>).
    /// </summary>
    public static Task<CurrentUser> GetCurrentUserAsync(HttpRequest request, IJwtValidator validator, CancellationToken cancellationToken = default)
    {
        var header = request.Headers["Authorization"].FirstOrDefault();
        return CurrentUser.FromAuthorizationHeaderAsync(header, validator, cancellationToken);
    }

    public static async Task<T> ReadRequiredJsonAsync<T>(HttpRequest request, CancellationToken cancellationToken = default)
    {
        var body = await JsonSerializer.DeserializeAsync<T>(request.Body, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        return body ?? throw new ArgumentException("Request body is required.");
    }

    /// <summary>
    /// Runs <paramref name="action"/> and maps known exceptions from the shared
    /// authorization/domain layers to the corresponding HTTP status code.
    /// </summary>
    public static async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (UnauthorizedException ex)
        {
            return Problem(StatusCodes.Status401Unauthorized, ex.Message);
        }
        catch (ForbiddenException ex)
        {
            return Problem(StatusCodes.Status403Forbidden, ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(StatusCodes.Status404NotFound, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Problem(StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private static IActionResult Problem(int statusCode, string message) =>
        new ObjectResult(new { error = message }) { StatusCode = statusCode };
}
