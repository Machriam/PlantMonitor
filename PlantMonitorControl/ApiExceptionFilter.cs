using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Plantmonitor.Server.Features.AppConfiguration
{
    public class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            context.Result = context.Exception switch
            {
                _ => new JsonResult(context.Exception.Message) { StatusCode = StatusCodes.Status500InternalServerError },
            };
            logger.Log(LogLevel.Error, "Message: {message}", context.Exception.Message);
            logger.Log(LogLevel.Error, "InnerMessage: {message}", context.Exception.InnerException?.Message);
            logger.Log(LogLevel.Error, "Stacktrace: {trace}", context.Exception.StackTrace);
            base.OnException(context);
        }
    }
}
