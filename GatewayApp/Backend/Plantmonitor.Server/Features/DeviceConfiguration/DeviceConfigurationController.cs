using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Server.Features.AppConfiguration;
using System.Net.NetworkInformation;

namespace Plantmonitor.Server.Features.DeviceConfiguration;

[ApiController]
[Route("api/[controller]")]
public class DeviceConfigurationController(IEnvironmentConfiguration configuration)
{
    [HttpGet("devices")]
    public async Task<IEnumerable<string>> GetDevices()
    {
        var from = configuration.IpScanRange_From();
        var to = configuration.IpScanRange_To();
        var pingTasks = new List<Task<PingReply>>();
        foreach (var ip in from.ToIpRange(to)) pingTasks.Add(PingIp(ip));
        await Task.WhenAll(pingTasks);
        return pingTasks
            .Where(pt => pt.Result.Status == IPStatus.Success)
            .Select(pt => pt.Result.Address.ToString());
    }

    private static async Task<PingReply> PingIp(string ip)
    {
        using var ping = new Ping();
        PingReply pingResult = default!;
        for (var i = 0; i < 3; i++)
        {
            pingResult = await ping.SendPingAsync(ip, 200);
            if (pingResult.Status == IPStatus.Success) return pingResult;
            await Task.Delay(10);
        }
        return pingResult;
    }
}