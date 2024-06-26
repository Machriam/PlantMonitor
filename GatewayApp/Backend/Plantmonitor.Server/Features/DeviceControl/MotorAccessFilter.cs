﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace Plantmonitor.Server.Features.DeviceControl;

public class MotorAccessFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var hostIp = context.HttpContext.Request.Headers.Host;
        Log.Logger.Information("Ip {ip} accessed motor", hostIp.Select(h => h).AsJson());
        if (hostIp.Count != 1 || hostIp[0]?.Split(":")[0] != "localhost")
        {
            context.Result = new JsonResult("Motor movement actions are only allowed on the gateway computer")
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
        }
        base.OnActionExecuting(context);
    }
}
