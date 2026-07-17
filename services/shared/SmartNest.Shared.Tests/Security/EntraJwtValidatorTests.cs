using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using SmartNest.Shared.Security;
using Xunit;

namespace SmartNest.Shared.Tests.Security;

/// <summary>
/// Exercises <see cref="EntraJwtValidator"/> against real RSA-signed tokens, using
/// <see cref="StaticConfigurationManager{T}"/> to supply a fixed JWKS instead of making a
/// live call to Entra ID's discovery endpoint.
/// </summary>
public class EntraJwtValidatorTests
{
    private const string TenantId = "test-tenant";
    private const string Audience = "api://test-client";
    private const string Authority = $"https://login.microsoftonline.com/{TenantId}/v2.0";

    [Fact]
    public async Task ValidateAsync_ReturnsPrincipal_WhenTokenIsValid()
    {
        using var rsa = RSA.Create(2048);
        var validator = CreateValidator(rsa);
        var token = CreateToken(rsa, oid: "user-oid-1", roles: ["SmartNest.Owner"], audience: Audience, issuer: Authority);

        var principal = await validator.ValidateAsync(token);

        principal.FindFirst("oid")!.Value.Should().Be("user-oid-1");
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenSignedWithUntrustedKey()
    {
        using var trustedKey = RSA.Create(2048);
        using var attackerKey = RSA.Create(2048);
        var validator = CreateValidator(trustedKey);
        var token = CreateToken(attackerKey, oid: "user-oid-1", roles: ["SmartNest.Owner"], audience: Audience, issuer: Authority);

        var act = async () => await validator.ValidateAsync(token);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenAudienceDoesNotMatch()
    {
        using var rsa = RSA.Create(2048);
        var validator = CreateValidator(rsa);
        var token = CreateToken(rsa, oid: "user-oid-1", roles: ["SmartNest.Owner"], audience: "api://wrong-client", issuer: Authority);

        var act = async () => await validator.ValidateAsync(token);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenIssuerDoesNotMatch()
    {
        using var rsa = RSA.Create(2048);
        var validator = CreateValidator(rsa);
        var token = CreateToken(rsa, oid: "user-oid-1", roles: ["SmartNest.Owner"], audience: Audience, issuer: "https://attacker.example.com/");

        var act = async () => await validator.ValidateAsync(token);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ValidateAsync_Throws_WhenTokenIsExpired()
    {
        using var rsa = RSA.Create(2048);
        var validator = CreateValidator(rsa);
        var token = CreateToken(
            rsa, oid: "user-oid-1", roles: ["SmartNest.Owner"], audience: Audience, issuer: Authority,
            expires: DateTime.UtcNow.AddHours(-1));

        var act = async () => await validator.ValidateAsync(token);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_Throws_WhenTokenIsEmpty(string? token)
    {
        using var rsa = RSA.Create(2048);
        var validator = CreateValidator(rsa);

        var act = async () => await validator.ValidateAsync(token!);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    private static EntraJwtValidator CreateValidator(RSA trustedSigningKey)
    {
        var config = new OpenIdConnectConfiguration();
        config.SigningKeys.Add(new RsaSecurityKey(trustedSigningKey) { KeyId = "test-key" });

        IConfigurationManager<OpenIdConnectConfiguration> configManager =
            new StaticConfigurationManager<OpenIdConnectConfiguration>(config);

        var options = new JwtValidationOptions { TenantId = TenantId, Audience = Audience };
        return new EntraJwtValidator(options, configManager);
    }

    private static string CreateToken(
        RSA signingKey, string? oid, IEnumerable<string> roles, string audience, string issuer, DateTime? expires = null)
    {
        var claims = new List<Claim>();
        if (oid is not null)
            claims.Add(new Claim("oid", oid));
        claims.AddRange(roles.Select(r => new Claim("roles", r)));

        var credentials = new SigningCredentials(new RsaSecurityKey(signingKey) { KeyId = "test-key" }, SecurityAlgorithms.RsaSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
