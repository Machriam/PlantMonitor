using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Plantmonitor.Server.Features.AppConfiguration;

namespace Plantmonitor.Server.Features.DeviceConfiguration;
public record struct DeviceConnection(string Ip, bool SshIsOpen);

[ApiController]
[Route("api/[controller]")]
public class DeviceConfigurationController(IDeviceConnectionTester connectionTester)
{
    [HttpGet("devices")]
    public async Task<IEnumerable<string>> GetDevices()
    {
        return await connectionTester.GetSSHDevices();
    }
}