namespace Plantmonitor.Server.Features.AppConfiguration
{
    public static class IWebHostEnvironmentExtensions
    {
        public const string DownloadFolder = "/download/";

        public static string DownloadFolderPath(this IWebHostEnvironment webHost) => webHost.WebRootPath + DownloadFolder;
    }
}
