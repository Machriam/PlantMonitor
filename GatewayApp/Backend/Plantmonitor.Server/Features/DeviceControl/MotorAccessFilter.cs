using System.Net;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace Plantmonitor.Server.Features.DeviceControl;

public class MotorAccessFilter : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();
        var localIp = context.HttpContext.Connection.LocalIpAddress?.ToString();
        var header = context.HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedIp);
        Log.Logger.Information("Ip {ip} accessed motor. Local IP: {localip}, Forwarded: {forwarded}", remoteIp, localIp, forwardedIp);
        Log.Logger.Information("Headers: {headers}", context.HttpContext.Request.Headers.Select(h => new { h.Key, h.Value }).AsJson());
        if (!forwardedIp.Any(fi => new[] { "127.0.0.1", "::1", localIp }.Contains(fi)))
            throw new Exception("Motor movement actions are only allowed on the gateway computer");
    }
}
