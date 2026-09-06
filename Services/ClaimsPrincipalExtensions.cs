using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MeetingScheduler.API.Services
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? GetUserId(this ClaimsPrincipal user)
        {
            var raw =
                user.FindFirstValue(ClaimTypes.NameIdentifier) ??
                user.FindFirstValue(JwtRegisteredClaimNames.Sub) ??
                user.FindFirst("sub")?.Value;

            if (int.TryParse(raw, out var id) && id > 0)
            {
                return id;
            }

            return null;
        }

        public static bool IsAdmin(this ClaimsPrincipal user)
        {
            var raw =
                user.FindFirstValue("isAdmin") ??
                user.FindFirst("isAdmin")?.Value;

            return bool.TryParse(raw, out var isAdmin) && isAdmin;
        }
    }
}
