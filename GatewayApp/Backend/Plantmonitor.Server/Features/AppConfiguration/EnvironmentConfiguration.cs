namespace Plantmonitor.Server.Features.AppConfiguration
{
    public interface IEnvironmentConfiguration
    {
        string IpScanRange_From();
        string IpScanRange_To();
    }

    public class EnvironmentConfiguration(IConfiguration configuration) : IEnvironmentConfiguration
    {
        public string IpScanRange_From() =>
            configuration.GetConnectionString(nameof(IpScanRange_From)) ??
                throw new Exception(nameof(IpScanRange_From) + " must be defined in appsettings.json");
        public string IpScanRange_To() =>
            configuration.GetConnectionString(nameof(IpScanRange_To)) ??
                throw new Exception(nameof(IpScanRange_To) + " must be defined in appsettings.json");
    }
}
