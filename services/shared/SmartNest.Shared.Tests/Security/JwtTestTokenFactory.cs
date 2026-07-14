using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SmartNest.Shared.Tests.Security;

/// <summary>
/// Builds unsigned-signature-not-required JWTs for unit tests. <see cref="SmartNest.Shared.Security.CurrentUser"/>
/// only reads claims (signature validation is APIM's responsibility), so a valid
/// signature isn't required — just a well-formed JWT with the claims under test.
/// </summary>
internal static class JwtTestTokenFactory
{
    public static string Create(string? oid, IEnumerable<string> roles, string? homeId)
    {
        var claims = new List<Claim>();
        if (oid is not null)
            claims.Add(new Claim("oid", oid));
        claims.AddRange(roles.Select(r => new Claim("roles", r)));
        if (homeId is not null)
            claims.Add(new Claim("homeId", homeId));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-signing-key-not-used-for-validation-123456"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "https://login.microsoftonline.com/test-tenant/v2.0",
            audience: "api://test-client",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
