using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace Plantmonitor.Server.Features.DeviceControl;

public class MotorAccessFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        var localIp = context.HttpContext.Connection.LocalIpAddress?.ToString();
        Log.Logger.Information("Ip {ip} accessed motor", remoteIp);
        if (!new[] { "127.0.0.1", "::1", localIp }.Contains(remoteIp))
            throw new Exception("Motor movement actions are only allowed on the gateway computer");
    }
}
