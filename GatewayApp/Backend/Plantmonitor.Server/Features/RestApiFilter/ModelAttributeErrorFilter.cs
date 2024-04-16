using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Plantmonitor.Server.Features.RestApiFilter
{
    public class ModelAttributeErrorFilter : ResultFilterAttribute
    {
        public override void OnResultExecuting(ResultExecutingContext context)
        {
            if (!context.ModelState.IsValid && context.Result is ObjectResult result && result.Value is HttpValidationProblemDetails details)
            {
                context.Result = new JsonResult(details.Errors
                    .SelectMany(err => err.Value.Select(v => $"{err.Key}: {v}"))
                    .Concat("\n"))
                { StatusCode = result.StatusCode };
            }
            base.OnResultExecuting(context);
        }
    }
}