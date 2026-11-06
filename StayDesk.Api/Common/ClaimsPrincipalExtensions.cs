using System.Security.Claims;

namespace StayDesk.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return int.Parse(value ?? throw new InvalidOperationException("El token no contiene el identificador del usuario."));
    }
}
