using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace HybirdRedisCache.Sample.WebAPI
{
    public class LogActionFilter : IActionFilter
    {
        private static long _requestCounter = 0;
        private static long RequestCounter
        {
            get { return _requestCounter; }
            set { Interlocked.Exchange(ref _requestCounter, value); }
        }
        private readonly ILoggerFactory _loggerFactory;

        public LogActionFilter(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            // Log information before every action method is called
            var controllerName = context.RouteData.Values["controller"].ToString();
            //var actionName = context.RouteData.Values["action"].ToString();
            var logger = _loggerFactory.CreateLogger(controllerName);
            var request = context.HttpContext.Request;
            logger.LogInformation($"[{++RequestCounter}] | {request.Protocol} {request.Method} {request.Scheme}://{request.Host}/{request.Path}");
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Do nothing
        }
    }
}
