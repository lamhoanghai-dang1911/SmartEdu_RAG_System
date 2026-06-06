using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmartEdu.Business.Interfaces;
using System.Security.Claims;

namespace SmartEdu.Web.Filters
{
    public class RequirePasswordChangeFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                base.OnActionExecuting(context);
                return;
            }

            var routeData = context.RouteData;
            var action = routeData.Values["action"]?.ToString() ?? string.Empty;
            var controller = routeData.Values["controller"]?.ToString() ?? string.Empty;

            // Allow ChangePassword and Logout to proceed
            if (string.Equals(action, "ChangePassword", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(action, "Logout", System.StringComparison.OrdinalIgnoreCase))
            {
                base.OnActionExecuting(context);
                return;
            }

            var userIdClaim = user.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdClaim, out var userId))
            {
                base.OnActionExecuting(context);
                return;
            }

            // resolve unit of work from services
            var uow = context.HttpContext.RequestServices.GetService(typeof(IUnitOfWork)) as IUnitOfWork;
            if (uow == null)
            {
                base.OnActionExecuting(context);
                return;
            }

            var userEntityTask = uow.Users.GetByIdAsync(userId);
            userEntityTask.Wait();
            var userEntity = userEntityTask.Result;
            if (userEntity != null && userEntity.RequirePasswordChange)
            {
                context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
