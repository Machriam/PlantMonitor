namespace Plantmonitor.Server.Features.AppConfiguration
{
    public record struct DeviceData(string DevicePassword, string DeviceUser);
    public record struct DeviceScanRange(string IpFrom, string IpTo);
    public record struct ConfigurationData(DeviceData DeviceData, DeviceScanRange DeviceScanRange);

    public interface IConfigurationStorage
    {
        ConfigurationData GetConfiguration();

        void UpdateConfiguration(ConfigurationData newData);

        void InitializeConfiguration();

        string AppConfigurationPath();
    }

    public class ConfigurationStorage(IConfiguration configuration) : IConfigurationStorage
    {
        private const string DataFolder = nameof(DataFolder);
        private const string AppConfiguration = "AppConfiguration.json";

        public string AppConfigurationPath()
        {
            return Path.Combine(configuration.GetConnectionString(DataFolder) ?? throw new Exception($"Appsettings must define {DataFolder}"), AppConfiguration);
        }

        public ConfigurationData GetConfiguration()
        {
            var path = AppConfigurationPath();
            var data = File.ReadAllText(path);
            return data.FromJson<ConfigurationData>();
        }

        public void InitializeConfiguration()
        {
            var path = AppConfigurationPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path)) return;
            var defaultConfiguration = new ConfigurationData
            {
                DeviceScanRange = new("192.168.1.100", "192.168.1.150"),
                DeviceData = new("", "")
            };
            UpdateConfiguration(defaultConfiguration);
        }

        public void UpdateConfiguration(ConfigurationData newData)
        {
            var path = AppConfigurationPath();
            File.WriteAllText(path, newData.AsJson(writeIndented: true));
        }
    }
}