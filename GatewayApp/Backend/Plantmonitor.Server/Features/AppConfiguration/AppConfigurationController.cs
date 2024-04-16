using Microsoft.AspNetCore.Mvc;

namespace Plantmonitor.Server.Features.AppConfiguration
{
    [ApiController]
    [Route("api/[controller]")]
    public class AppConfigurationController(IConfigurationStorage storage)
    {
        [HttpPost("updatedevicesettings")]
        public void UpdateDeviceSettings(string password, string user)
        {
            var configuration = storage.GetConfiguration();
            configuration.DeviceData = new(password, user);
            storage.UpdateConfiguration(configuration);
        }

        [HttpPost("updateipranges")]
        public void UpdateIpRanges(string ipFrom, string ipTo)
        {
            var configuration = storage.GetConfiguration();
            configuration.DeviceScanRange = new(ipFrom, ipTo);
            storage.UpdateConfiguration(configuration);
        }
    }
}