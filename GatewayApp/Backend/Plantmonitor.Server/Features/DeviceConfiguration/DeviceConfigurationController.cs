using Microsoft.AspNetCore.Mvc;
using Plantmonitor.Server.Features.AppConfiguration;

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
        return eventBus.GetDeviceHealthInformation();
    }
}