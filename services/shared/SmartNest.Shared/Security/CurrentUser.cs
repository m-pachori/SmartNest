using System.IdentityModel.Tokens.Jwt;

namespace SmartNest.Shared.Security;

/// <summary>
/// The authenticated caller's identity and authorization-relevant claims, extracted
/// from the JWT bearer token forwarded by API Management.
/// </summary>
/// <remarks>
/// API Management already validates the token's signature, audience, issuer, and
/// expiration (see infra/modules/apim.bicep's global <c>validate-jwt</c> policy) before
/// the request reaches this Function App. Reading the token here is for authorization
/// decisions only (role / homeId matching) — it intentionally does not re-validate the
/// signature. Direct-to-Function access (bypassing APIM) is prevented by the
/// Function-level auth key configured on the APIM backend (see ADR-009).
/// </remarks>
public sealed class CurrentUser
{
    public required string UserId { get; init; }

    public required IReadOnlyCollection<string> Roles { get; init; }

    public string? HomeId { get; init; }

    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a <see cref="CurrentUser"/> from the raw <c>Authorization</c> header value
    /// (e.g. <c>"Bearer eyJ..."</c>).
    /// </summary>
    /// <exception cref="UnauthorizedException">
    /// Thrown when the header is missing, empty, or not a well-formed JWT, or the token
    /// is missing a subject/object-id claim.
    /// </exception>
    public static CurrentUser FromAuthorizationHeader(string? authorizationHeaderValue)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeaderValue))
            throw new UnauthorizedException("Authorization header is missing.");

        const string bearerPrefix = "Bearer ";
        var token = authorizationHeaderValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorizationHeaderValue[bearerPrefix.Length..].Trim()
            : authorizationHeaderValue.Trim();

        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedException("Bearer token is empty.");

        JwtSecurityToken jwt;
        try
        {
            jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedException("Authorization header does not contain a valid JWT.", ex);
        }

        var userId = jwt.Claims.FirstOrDefault(c => c.Type == "oid")?.Value
            ?? jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value
            ?? throw new UnauthorizedException("Token is missing a subject/object identifier claim.");

        var roles = jwt.Claims
            .Where(c => c.Type is "roles" or "role")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var homeId = jwt.Claims.FirstOrDefault(c => c.Type == "homeId")?.Value;

        return new CurrentUser
        {
            UserId = userId,
            Roles = roles,
            HomeId = homeId,
        };
    }
}
