namespace PlantMonitorControl.Features.AppsettingsConfiguration;

public interface IEnvironmentConfiguration
{
    MotorPinout MotorPinout { get; }
    IRPrograms IRPrograms { get; }
    Temp2ClickPrograms Temp2ClickPrograms { get; }
    PowerSwitchPinout PowerSwitchPinout { get; }
    PowerSwitchPrograms PowerSwitchPrograms { get; }
    string StreamArchivePath { get; }
    string GetDownloadfolder { get; }
    public const string DownloadFolderName = "/download/";
    public const string LinuxStaticFilesFolder = "/srv/dist/wwwroot";

    void ClearDownloadfolder();
}

public class EnvironmentConfiguration(ConfigurationOptions options, IWebHostEnvironment webHost) : IEnvironmentConfiguration
{
    private static readonly string s_streamArchive = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "streamArchive");
    public MotorPinout MotorPinout => options.MotorPinout;
    public IRPrograms IRPrograms => options.IRPrograms;
    public Temp2ClickPrograms Temp2ClickPrograms => options.Temp2ClickPrograms;
    public PowerSwitchPinout PowerSwitchPinout => options.PowerSwitchPinout;
    public PowerSwitchPrograms PowerSwitchPrograms => options.PowerSwitchPrograms;

    public string StreamArchivePath
    {
        get
        {
            if (!Path.Exists(s_streamArchive)) Directory.CreateDirectory(s_streamArchive);
            return s_streamArchive;
        }
    }

    public string GetDownloadfolder
    {
        get
        {
            var downloadPath = webHost.WebRootPath + IEnvironmentConfiguration.DownloadFolderName;
            Directory.CreateDirectory(downloadPath);
            return downloadPath;
        }
    }

    public void ClearDownloadfolder()
    {
        var downloadfolder = GetDownloadfolder;
        Directory.Delete(downloadfolder, true);
    }
}
