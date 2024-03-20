namespace Plantmonitor.Server.Features.AppConfiguration
{
    public interface IEnvironmentConfiguration
    {
        string IpScanRange_From();

        string IpScanRange_To();

        string WebSshUrl();

        string DeviceUsername();

        string DevicePassword();
    }

    public class EnvironmentConfiguration(IConfiguration configuration) : IEnvironmentConfiguration
    {
        public string IpScanRange_From() =>
            configuration.GetConnectionString(nameof(IpScanRange_From)) ??
                throw new Exception(nameof(IpScanRange_From) + " must be defined in appsettings.json");

        public string IpScanRange_To() =>
            configuration.GetConnectionString(nameof(IpScanRange_To)) ??
                throw new Exception(nameof(IpScanRange_To) + " must be defined in appsettings.json");

        public string WebSshUrl() =>
            configuration.GetConnectionString(nameof(WebSshUrl)) ??
                throw new Exception(nameof(WebSshUrl) + " must be defined in appsettings.json");

        public string DeviceUsername() =>
            configuration.GetConnectionString(nameof(DeviceUsername)) ??
                throw new Exception(nameof(DeviceUsername) + " must be defined in appsettings.json");

        public string DevicePassword() =>
            configuration.GetConnectionString(nameof(DevicePassword)) ??
                throw new Exception(nameof(DevicePassword) + " must be defined in appsettings.json");
    }
}