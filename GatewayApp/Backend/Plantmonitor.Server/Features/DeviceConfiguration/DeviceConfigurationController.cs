using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Server.Features.AppConfiguration;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Plantmonitor.Server.Features.DeviceConfiguration;
public record struct DeviceConnection(string Ip, bool SshIsOpen);

[ApiController]
[Route("api/[controller]")]
public class DeviceConfigurationController(IEnvironmentConfiguration configuration)
{
    [HttpGet("devices")]
    public async Task<IEnumerable<string>> GetDevices()
    {
        var from = configuration.IpScanRange_From();
        var to = configuration.IpScanRange_To();
        var pingTasks = new List<Task<DeviceConnection>>();
        foreach (var ip in from.ToIpRange(to)) pingTasks.Add(PingIp(ip));
        await Task.WhenAll(pingTasks);
        return pingTasks
            .Where(pt => pt.Result.SshIsOpen && !pt.Result.Ip.IsEmpty())
            .Select(pt => pt.Result.Ip);
    }

    private static async Task<DeviceConnection> PingIp(string ip)
    {
        DeviceConnection deviceConnection = new();
        for (var i = 0; i < 3; i++)
        {
            var ping = new Ping();
            var pingResult = await ping.SendPingAsync(ip, 100);
            if (pingResult.Status == IPStatus.Success)
            {
                var (success, _) = await TestSSH(ip).TryAsyncTask();
                return new DeviceConnection(ip, success);
            }
            ping.Dispose();
            await Task.Delay(10);
        }
        return deviceConnection;
    }

    private static async Task<bool> TestSSH(string ip)
    {
        await Task.Yield();
        using var tcpClient = new TcpClient();
        var result = tcpClient.BeginConnect(ip, 22, null, null);
        var success = result.AsyncWaitHandle.WaitOne(100);
        tcpClient.EndConnect(result);
        return success;
    }
}