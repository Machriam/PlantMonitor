using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Server.Features.AppConfiguration;
using System.Net;
using System.Net.NetworkInformation;

namespace Plantmonitor.Server.Features.DeviceConfiguration;
[ApiController]
[Route("[controller]")]
public class DeviceConfigurationController(IEnvironmentConfiguration configuration)
{
    [HttpGet("devices")]
    public async Task<IEnumerable<string>> GetDevices()
    {
        var from = configuration.IpScanRange_From();
        var to = configuration.IpScanRange_To();
        var ping = new Ping();
        var pingTasks = new List<Task<PingReply>>();
        foreach (var ip in from.ToIpRange(to)) pingTasks.Add(ping.SendPingAsync(ip, 500));
        await Task.WhenAll(pingTasks);
        return pingTasks
            .Where(pt => pt.Result.Status == IPStatus.Success)
            .Select(pt => pt.Result.Address.ToString());
    }

}
