using Microsoft.AspNetCore.Mvc.Filters;

namespace MyMvcApp.Filters
{
    /// <summary>
    /// Action filter that sets aggressive cache-control headers to prevent browser caching.
    /// Apply to all authenticated pages to ensure they can't be accessed via back button after logout.
    /// </summary>
    public class NoCacheAttribute : ActionFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext context)
        {
            var headers = context.HttpContext.Response.Headers;

            // HTTP 1.1 cache control - prevent all caching
            headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private, max-age=0";
            headers["Pragma"] = "no-cache";
            headers["Expires"] = "-1";
            headers["Vary"] = "*";

            // Additional headers to prevent caching in various browsers/proxies
            headers["X-Cache-Control"] = "no-cache, no-store, must-revalidate";
            headers["Surrogate-Control"] = "no-store";

            // Prevent storing in persistent storage (bfcache in Firefox/Safari)
            headers["Cache-Control"] += ", immutable";

            base.OnResultExecuting(context);
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Also set headers early in case of short-circuit
            var headers = context.HttpContext.Response.Headers;
            headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private, max-age=0";
            headers["Pragma"] = "no-cache";
            headers["Expires"] = "-1";

            base.OnActionExecuting(context);
        }
    }
}
