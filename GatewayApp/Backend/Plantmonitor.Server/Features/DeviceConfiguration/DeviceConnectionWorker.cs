using Plantmonitor.Server.Features.DeviceControl;
using System.Collections.Concurrent;

namespace Plantmonitor.Server.Features.DeviceConfiguration;

public record struct DeviceHealthState(DeviceHealth Health, int RetryTimes, string Ip);

public class DeviceConnectionWorker(IDeviceConnectionTester tester, IDeviceConnectionEventBus eventBus, ILogger<DeviceConnectionWorker> logger) : IHostedService
{
    private Timer? _sshPingTimer;
    private Timer? _healthPingTimer;
    private bool _currentlySshPinging;
    private bool _currentlyDevicePinging;
    private readonly object _httpPingLock = new();
    private readonly object _sshPingLock = new();
    private readonly ConcurrentDictionary<string, DeviceHealthState> _deviceList = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sshPingTimer = new Timer(async _ => await SSHPingDevices(), default, 0, (int)TimeSpan.FromSeconds(30).TotalMilliseconds);
        _healthPingTimer = new Timer(async _ => await HttpPingDevices(), default, 10, (int)TimeSpan.FromSeconds(20).TotalMilliseconds);
        return Task.CompletedTask;
    }

    private async Task HttpPingDevices()
    {
        const string errorTemplate = "{IP} could not be pinged, {Error}";
        lock (_httpPingLock)
        {
            if (_currentlyDevicePinging) return;
            _currentlyDevicePinging = true;
        }
        foreach (var (ip, healthState) in _deviceList)
        {
            _deviceList[ip] = new(healthState.Health, healthState.RetryTimes + 1, ip);
            var (result, error) = await tester.CheckHealth(ip).Try();
            if (result != null && error.IsEmpty()) _deviceList[ip] = new(result, 0, ip);
            else logger.Log(LogLevel.Information, errorTemplate, ip, error);
            if (_deviceList[ip].RetryTimes >= 5)
            {
                var (_, success) = await tester.PingIp(ip, 5);
                if (!success) _deviceList.Remove(ip, out _);
            }
        }
        lock (_httpPingLock) _currentlyDevicePinging = false;
        eventBus.UpdateDeviceHealths(_deviceList.Values);
    }

    private async Task SSHPingDevices()
    {
        lock (_sshPingLock)
        {
            if (_currentlySshPinging) return;
            _currentlySshPinging = true;
        }
        var devices = await tester.GetSSHDevices([]);
        foreach (var device in devices) if (!_deviceList.ContainsKey(device)) _deviceList.TryAdd(device, new DeviceHealthState() { Ip = device });
        lock (_sshPingLock) _currentlySshPinging = false;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sshPingTimer?.Dispose();
        _healthPingTimer?.Dispose();
        return Task.CompletedTask;
    }
}
