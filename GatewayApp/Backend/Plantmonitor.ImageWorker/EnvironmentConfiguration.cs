using System.Text.Encodings.Web;
using Plantmonitor.Shared.Extensions;

namespace Plantmonitor.ImageWorker
{
    internal interface IEnvironmentConfiguration
    {
        string PicturePath(string device);

        string DatabaseConnection();

        IEnumerable<string> PictureFolders();

        string VirtualImagePath(string name, long id);

        IEnumerable<string> VirtualImageFolders();

        string CustomTourDataPath();
    }

    internal class EnvironmentConfiguration(IConfiguration configuration) : IEnvironmentConfiguration
    {
        private const string CertificateFolder = nameof(CertificateFolder);
        private const string VirtualImageFolderPrefix = "VirtualImages_";
        private const string CustomTourDataPrefix = "CustomTourData_";
        private const string ImageFolderPrefix = "Images_";
        private const string DataFolder = nameof(DataFolder);
        private const string AppConfiguration = "AppConfiguration.json";

        public IEnumerable<string> PictureFolders()
        {
            var imageFolder = Path.GetDirectoryName(AppConfigurationPath()) ??
                throw new Exception("No App configuration path found");
            return Directory.GetDirectories(imageFolder, $"{ImageFolderPrefix}*");
        }

        public string AppConfigurationPath()
        {
            return Path.Combine(configuration.GetConnectionString(DataFolder) ?? throw new Exception($"Appsettings must define {DataFolder}"), AppConfiguration);
        }

        public string PicturePath(string device)
        {
            var imageFolder = Path.Combine(Path.GetDirectoryName(AppConfigurationPath()) ??
                throw new Exception("No App configuration path found"), ImageFolderPrefix + device);
            Directory.CreateDirectory(imageFolder);
            return imageFolder;
        }

        public IEnumerable<string> VirtualImageFolders()
        {
            var directory = Path.GetDirectoryName(AppConfigurationPath()) ?? throw new Exception("No App configuration path found");
            return Directory.EnumerateDirectories(directory, VirtualImageFolderPrefix + "*");
        }

        public string CustomTourDataPath()
        {
            var directory = Path.GetDirectoryName(AppConfigurationPath()) ?? throw new Exception("No App configuration path found");
            return Directory.EnumerateDirectories(directory, CustomTourDataPrefix + "*").First();
        }

        public string VirtualImagePath(string name, long id)
        {
            var directory = Path.GetDirectoryName(AppConfigurationPath()) ?? throw new Exception("No App configuration path found");
            var imageFolder = Path.Combine(directory, $"{VirtualImageFolderPrefix}{id}_{name.SanitizeFileName()}");
            Directory.CreateDirectory(imageFolder);
            return imageFolder;
        }

        public string DatabaseConnection()
        {
            var connection = configuration.GetConnectionString(nameof(DatabaseConnection));
            var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
            return connection?.Replace("{POSTGRES_PASSWORD}", password) ?? throw new Exception("DatabaseConnection not found in appsettings");
        }
    }
}
