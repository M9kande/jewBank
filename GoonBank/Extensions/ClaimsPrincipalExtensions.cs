using System.Security.Claims;

namespace GoonBank.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string? GetAccountId(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("account_id")?.Value
            ?? user.FindFirst("sub")?.Value;
    }
}
