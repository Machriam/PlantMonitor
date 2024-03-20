using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Plantmonitor.Server.Features.AppConfiguration;

namespace Plantmonitor.Server.Features.DeviceConfiguration;
public record struct DeviceConnection(string Ip, bool SshIsOpen);

[ApiController]
[Route("api/[controller]")]
public class DeviceConfigurationController(IDeviceConnectionTester connectionTester, IEnvironmentConfiguration configuration)
{
    public record struct WebSshCredentials(string Url, string Password, string User);

    [HttpGet("websshcredentials")]
    public WebSshCredentials GetWebSshCredentials()
    {
        return new WebSshCredentials(configuration.WebSshUrl(), configuration.DevicePassword(), configuration.DeviceUsername());
    }

    [HttpGet("devices")]
    public async Task<IEnumerable<string>> GetDevices()
    {
        return await connectionTester.GetSSHDevices();
    }
}