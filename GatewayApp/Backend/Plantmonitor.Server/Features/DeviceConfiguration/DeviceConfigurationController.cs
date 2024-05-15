using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Shared.Features.HealthChecking;

namespace Plantmonitor.Server.Features.DeviceConfiguration;
public record struct DeviceConnection(string Ip, bool SshIsOpen);

[ApiController]
[Route("api/[controller]")]
public class DeviceConfigurationController(IDeviceConnectionEventBus eventBus, IEnvironmentConfiguration configuration)
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

    [HttpGet("devices")]
    public IEnumerable<DeviceHealthState> GetDevices()
    {
        var result = eventBus.GetDeviceHealthInformation();
#if DEBUG
        result = result.Append(new DeviceHealthState(new DeviceHealth() { DeviceId = "test-id", DeviceName = "test", State = HealthState.NA }, 0, "localhost:7006"));
#endif
        return result;
    }
}
