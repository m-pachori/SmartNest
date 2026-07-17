using System.IdentityModel.Tokens.Jwt;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

[assembly: InternalsVisibleTo("SmartNest.Shared.Tests")]

namespace SmartNest.Shared.Security;

/// <summary>
/// Configuration needed to validate Entra ID-issued JWTs against a specific tenant/audience.
/// </summary>
public sealed record JwtValidationOptions
{
    /// <summary>Azure AD (Entra ID) tenant GUID that issued the token.</summary>
    public required string TenantId { get; init; }

    /// <summary>Expected audience claim, e.g. <c>"api://{apiAppClientId}"</c>.</summary>
    public required string Audience { get; init; }
}

/// <summary>
/// Validates a raw bearer token string and returns the resulting claims, throwing
/// <see cref="UnauthorizedException"/> on any validation failure.
/// </summary>
public interface IJwtValidator
{
    Task<ClaimsPrincipal> ValidateAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates Entra ID-issued JWTs (signature, issuer, audience, expiry) against the
/// tenant's live JWKS, fetched from the OIDC discovery document. This replaces reliance
/// on APIM's <c>validate-jwt</c> policy as the sole point of signature verification (see
/// infra/modules/apim.bicep) - useful defence-in-depth, and required while
/// <c>deployApim</c> is disabled (infra/main.bicep) since APIM currently isn't deployed.
/// </summary>
/// <remarks>
/// Safe to register as a singleton: <see cref="ConfigurationManager{T}"/> caches the
/// retrieved signing keys internally (default refresh: 24h, with automatic refresh on a
/// signature-validation failure), so this does not make a network call per request.
/// </remarks>
public sealed class EntraJwtValidator : IJwtValidator
{
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configManager;
    private readonly string _audience;
    private readonly string[] _validIssuers;

    // JwtSecurityTokenHandler maps several short JWT claim types (including "oid" and
    // "roles") to long legacy XML-namespace claim URIs by default. CurrentUser reads the
    // short claim names directly from the validated ClaimsPrincipal, so that mapping must
    // be disabled - otherwise every validated token would appear to be missing its
    // subject/object-id claim.
    private readonly JwtSecurityTokenHandler _handler = new() { MapInboundClaims = false };

    public EntraJwtValidator(JwtValidationOptions options)
        : this(options, CreateConfigurationManager(options.TenantId))
    {
    }

    /// <summary>Test-only seam: allows a fake/static configuration manager to be supplied.</summary>
    internal EntraJwtValidator(JwtValidationOptions options, IConfigurationManager<OpenIdConnectConfiguration> configurationManager)
    {
        ArgumentNullException.ThrowIfNull(options);
        _configManager = configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
        _audience = options.Audience;

        var authority = $"https://login.microsoftonline.com/{options.TenantId}/v2.0";
        // v1 and v2 tokens use different issuer formats - accept both, mirroring APIM's policy
        // (infra/policies/jwt-validation.xml).
        _validIssuers = [authority, $"https://sts.windows.net/{options.TenantId}/"];
    }

    public async Task<ClaimsPrincipal> ValidateAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new UnauthorizedException("Bearer token is empty.");

        OpenIdConnectConfiguration config;
        try
        {
            config = await _configManager.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedException("Unable to retrieve identity provider signing keys.", ex);
        }

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = _validIssuers,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
        };

        try
        {
            return _handler.ValidateToken(token, validationParameters, out _);
        }
        catch (SecurityTokenException ex)
        {
            throw new UnauthorizedException("JWT failed signature/issuer/audience/lifetime validation.", ex);
        }
        catch (ArgumentException ex)
        {
            // Thrown by ValidateToken for malformed token strings.
            throw new UnauthorizedException("Authorization header does not contain a valid JWT.", ex);
        }
    }

    private static IConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(string tenantId)
    {
        var authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        return new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());
    }
}
