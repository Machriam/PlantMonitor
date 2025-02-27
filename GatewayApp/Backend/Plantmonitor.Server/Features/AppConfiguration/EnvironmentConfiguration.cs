﻿using Plantmonitor.DataModel.DataModel;
using Plantmonitor.ImageWorker;

namespace Plantmonitor.Server.Features.AppConfiguration
{
    public interface IEnvironmentConfiguration : IImageWorkerConfiguration
    {
        string IpScanRange_From();

        string IpScanRange_To();

        string DeviceUsername();

        string DevicePassword();

        string Certificate();

        string CertificateKey();

        (string Protocol, string Port) WebSshUrl();

        string RepoRootPath();
    }

    public class EnvironmentConfiguration(IConfiguration configuration, IConfigurationStorage configurationStorage) : IEnvironmentConfiguration
    {
        private const string CertificateFolder = nameof(CertificateFolder);

        public string RepoRootPath()
        {
#if DEBUG || NOSWAG
            return Path.GetFullPath("../../../");
#else
            return Path.GetFullPath("/PlantMonitor");
#endif
        }

        public IEnumerable<string> PictureFolders()
        {
            var imageFolder = Path.GetDirectoryName(configurationStorage.AppConfigurationPath()) ??
                throw new Exception("No App configuration path found");
            return Directory.GetDirectories(imageFolder, $"{PhotoTourTrip.ImageFolderPrefix}*");
        }

        public string PicturePath(string device)
        {
            var imageFolder = Path.Combine(Path.GetDirectoryName(configurationStorage.AppConfigurationPath()) ??
                throw new Exception("No App configuration path found"), PhotoTourTrip.ImageFolderPrefix + device);
            Directory.CreateDirectory(imageFolder);
            return imageFolder;
        }

        public IEnumerable<string> VirtualImageFolders()
        {
            var directory = Path.GetDirectoryName(configurationStorage.AppConfigurationPath()) ?? throw new Exception("No App configuration path found");
            return Directory.EnumerateDirectories(directory, PhotoTourTrip.VirtualImageFolderPrefix + "*");
        }

        public string CustomTourDataPath()
        {
            var directory = Path.GetDirectoryName(configurationStorage.AppConfigurationPath()) ?? throw new Exception("No App configuration path found");
            return Directory.EnumerateDirectories(directory, PhotoTourTrip.CustomTourDataPrefix + "*").First();
        }

        public string VirtualImagePath(string name, long id)
        {
            var directory = Path.GetDirectoryName(configurationStorage.AppConfigurationPath()) ?? throw new Exception("No App configuration path found");
            var imageFolder = Path.Combine(directory, $"{PhotoTourTrip.VirtualImageFolderPrefix}{id}_{name.SanitizeFileName()}");
            Directory.CreateDirectory(imageFolder);
            return imageFolder;
        }

        public string Certificate()
        {
            var path = Path.Combine(configuration.GetConnectionString(CertificateFolder) ?? throw new Exception($"Appsettings must define {CertificateFolder}"), "plantmonitor.crt");
            return File.ReadAllText(path ?? throw new Exception($"A certificate could not be found under {path}"));
        }

        public string CertificateKey()
        {
            var path = Path.Combine(configuration.GetConnectionString(CertificateFolder) ?? throw new Exception($"Appsettings must define {CertificateFolder}"), "plantmonitor.key");
            return File.ReadAllText(path ?? throw new Exception($"A certificate could not be found under {path}"));
        }

        public string IpScanRange_From() => configurationStorage.GetConfiguration().DeviceScanRange.IpFrom;

        public string IpScanRange_To() => configurationStorage.GetConfiguration().DeviceScanRange.IpTo;

        public (string Protocol, string Port) WebSshUrl()
        {
            var result = configuration.GetConnectionString(nameof(WebSshUrl)) ??
                throw new Exception(nameof(WebSshUrl) + " must be defined in appsettings.json");
            var split = result.Split(",");
            return (split[0], split[1]);
        }

        public string DatabaseConnection()
        {
            var connection = configuration.GetConnectionString(nameof(DatabaseConnection));
            var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
            return connection?.Replace("{POSTGRES_PASSWORD}", password) ?? throw new Exception("DatabaseConnection not found in appsettings");
        }

        public string DeviceUsername() => configurationStorage.GetConfiguration().DeviceData.DeviceUser;

        public string DevicePassword() => configurationStorage.GetConfiguration().DeviceData.DevicePassword;
    }
}
