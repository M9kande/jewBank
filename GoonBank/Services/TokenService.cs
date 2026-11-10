using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GoonBank.Models;
using Microsoft.IdentityModel.Tokens;

namespace GoonBank.Services;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) GenerateToken(Account account);
}

public class TokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAt) GenerateToken(Account account)
    {
        var secret = _configuration["JwtSettings:Secret"] ?? "GoonBankSuperSecretSecureKeyForJwtTokens2026!#*$";
        var issuer = _configuration["JwtSettings:Issuer"] ?? "GoonBank";
        var audience = _configuration["JwtSettings:Audience"] ?? "GoonBankClients";
        var expiryMinutesStr = _configuration["JwtSettings:ExpiryMinutes"];
        var expiryMinutes = int.TryParse(expiryMinutesStr, out var m) ? m : 120;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.AccountId),
            new Claim(ClaimTypes.Email, account.Email),
            new Claim(ClaimTypes.GivenName, account.FirstName),
            new Claim(ClaimTypes.Surname, account.LastName),
            new Claim(ClaimTypes.Role, account.AccountType.ToString()),
            new Claim("account_id", account.AccountId),
            new Claim("account_no", account.AccountNo),
            new Claim("currency", account.Currency)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiresAt);
    }
}
