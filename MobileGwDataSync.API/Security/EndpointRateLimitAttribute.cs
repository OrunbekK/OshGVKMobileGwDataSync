using Microsoft.AspNetCore.Mvc.Filters;

namespace MobileGwDataSync.API.Security
{
    public class EndpointRateLimitAttribute : ActionFilterAttribute
    {
        private readonly string _policyName;

        public EndpointRateLimitAttribute(string policyName = "default")
        {
            _policyName = policyName;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Rate limit logic будет в Program.cs через middleware
            base.OnActionExecuting(context);
        }
    }
}