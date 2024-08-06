using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Plantmonitor.Server.Features.AppConfiguration
{
    public class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            switch (context.Exception)
            {
                case DbUpdateException:
                    if (context.Exception.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        context.Result = new JsonResult("The unique identifier of this entry is already Taken") { StatusCode = StatusCodes.Status500InternalServerError };
                        break;
                    }
                    if (context.Exception.InnerException?.Message.Contains("foreign key", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        context.Result = new JsonResult("Other database entries reference this entry. Those links must be deleted first") { StatusCode = StatusCodes.Status500InternalServerError };
                        break;
                    }
                    context.Result = new JsonResult(context.Exception.Message) { StatusCode = StatusCodes.Status500InternalServerError };
                    break;

                default:
                    context.Result = new JsonResult(context.Exception.Message) { StatusCode = StatusCodes.Status500InternalServerError };
                    break;
            }
            logger.Log(LogLevel.Error, "Message: {message}", context.Exception.Message);
            logger.Log(LogLevel.Error, "InnerMessage: {message}", context.Exception.InnerException?.Message);
            logger.Log(LogLevel.Error, "Stacktrace: {trace}", context.Exception.StackTrace);
            base.OnException(context);
        }
    }
}
