using System.Security.Claims;

namespace SmartEdu.Web.Extensions
{
    public static class UserExtensions
    {
        public static int GetUserId(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst("UserId")?.Value;
            return claim != null ? int.Parse(claim) : 0;
        }
    }
}
