using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MyMvcApp.Filters
{
    public class AuthFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var userId = context.HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new RedirectToActionResult("Index", "Home", null);
                return;
            }

            // Prevent browser from caching any protected page
            context.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private";
            context.HttpContext.Response.Headers["Pragma"]        = "no-cache";
            context.HttpContext.Response.Headers["Expires"]       = "0";
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
