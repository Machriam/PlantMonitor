using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Features.DeviceConfiguration;
public record struct DeviceConnection(string Ip, bool SshIsOpen);

[ApiController]
[Route("api/[controller]")]
public class DeviceConfigurationController(IDeviceConnectionEventBus eventBus, IEnvironmentConfiguration configuration, IDeviceApiFactory deviceApiFactory)
{
    public record struct WebSshCredentials(string Protocol, string Port, string Password, string User);
    public record struct CertificateData(string Certificate, string Key);

    [HttpGet("certificates")]
    public CertificateData GetCertificateData()
    {
        return new CertificateData(configuration.Certificate(), configuration.CertificateKey());
    }

    [HttpGet("websshcredentials")]
    public WebSshCredentials GetWebSshCredentials()
    {
        var (protocol, port) = configuration.WebSshUrl();
        return new WebSshCredentials(protocol, port, configuration.DevicePassword(), configuration.DeviceUsername());
    }

    [HttpGet("devicelogs")]
    public async Task<string> GetDeviceLog(string ip)
    {
        return await deviceApiFactory.HealthClient(ip).LogsAsync();
    }

    [HttpGet("alldevicelogs")]
    public async Task<string> GetAllDeviceLog(string ip)
    {
        return await deviceApiFactory.HealthClient(ip).AlllogsAsync();
    }

    [HttpPost("recheckdevice")]
    public async Task<DeviceHealth> RecheckDevice(string ip)
    {
        return await deviceApiFactory.HealthClient(ip).CheckdevicehealthAsync();
    }

    [HttpGet("devices")]
    public IEnumerable<DeviceHealthState> GetDevices()
    {
        return eventBus.GetDeviceHealthInformation();
    }
}
