using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Shared.Features.HealthChecking;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Plantmonitor.Server.Features.DeviceConfiguration;

public interface IDeviceConnectionTester
{
    Task<DeviceHealth> CheckHealth(string ip);

    Task<IEnumerable<string>> GetSSHDevices(HashSet<string> ipsToExclude);

    Task<DeviceConnection> PingIp(string ip, int retryTimes = 30);
}

public class DeviceConnectionTester(IEnvironmentConfiguration configuration, ILogger<DeviceConnectionTester> logger) : IDeviceConnectionTester
{
    private static readonly HttpClient _client = new();

    public async Task<DeviceHealth> CheckHealth(string ip)
    {
        return await _client.GetFromJsonAsync<DeviceHealth>($"https://{ip}/api/health") ?? new();
    }

    public async Task<IEnumerable<string>> GetSSHDevices(HashSet<string> ipsToExclude)
    {
        var from = configuration.IpScanRange_From();
        var to = configuration.IpScanRange_To();
        var pingTasks = new List<Task<DeviceConnection>>();
        logger.LogInformation("Pinging IPs from {from} to {to}", from, to);
        foreach (var ip in from.ToIpRange(to).Where(ip => !ipsToExclude.Contains(ip))) pingTasks.Add(PingIp(ip));
        await Task.WhenAll(pingTasks);
        return pingTasks
            .Where(pt => pt.Result.SshIsOpen && !pt.Result.Ip.IsEmpty())
            .Select(pt => pt.Result.Ip);
    }

    public async Task<DeviceConnection> PingIp(string ip, int retryTimes = 30)
    {
        DeviceConnection deviceConnection = new();
        for (var i = 0; i < retryTimes; i++)
        {
            var ping = new Ping();
            var pingResult = await ping.SendPingAsync(ip, 100);
            if (pingResult.Status == IPStatus.Success)
            {
                var (success, _) = await TestSSH(ip).TryAsyncTask();
                logger.LogInformation("Ping Error {ip}: {success}", ip, success);
                return new DeviceConnection(ip, success);
            }
            ping.Dispose();
            await Task.Delay(100);
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