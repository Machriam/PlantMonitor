using System.Net;
using Plantmonitor.Server.Features.AppConfiguration;

namespace Plantmonitor.Server.Features.DeviceControl;

public interface IStaticFilesClient
{
    Task<string> DownloadToStaticFiles(string file, Func<float, Task> onProgressChanged);
}

public interface IDeviceApiFactory
{
    IVisImageTakingClient VisImageTakingClient(string ip);

    IIrImageTakingClient IrImageTakingClient(string ip);

    IMotorMovementClient MovementClient(string ip);

    ITemperatureClient TemperatureClient(string ip);

    IHealthClient HealthClient(string ip);

    ISwitchOutletsClient SwitchOutletsClient(string ip);

    IStaticFilesClient StaticFilesClient(string ip);
}

public class DeviceApiFactory(IWebHostEnvironment webHost) : IDeviceApiFactory
{
    public ITemperatureClient TemperatureClient(string ip)
    {
        return new TemperatureClient($"https://{ip}");
    }

    public IHealthClient HealthClient(string ip)
    {
        return new HealthClient($"https://{ip}");
    }

    public IIrImageTakingClient IrImageTakingClient(string ip)
    {
        return new IrImageTakingClient($"https://{ip}");
    }

    public IVisImageTakingClient VisImageTakingClient(string ip)
    {
        return new VisImageTakingClient($"https://{ip}");
    }

    public IMotorMovementClient MovementClient(string ip)
    {
        return new MotorMovementClient($"https://{ip}");
    }

    public ISwitchOutletsClient SwitchOutletsClient(string ip)
    {
        return new SwitchOutletsClient($"https://{ip}");
    }

    public IStaticFilesClient StaticFilesClient(string ip)
    {
        return new StaticFilesClient(ip, webHost);
    }
}

public class StaticFilesClient(string ip, IWebHostEnvironment webHost) : IStaticFilesClient
{
    public async Task<string> DownloadToStaticFiles(string file, Func<float, Task> onProgressChanged)
    {
        var client = new HttpClient();
        var downloadAddress = new Uri($"https://{ip}/{file}").ToString();
        var downloadPath = webHost.DownloadFolderPath();
        Directory.CreateDirectory(downloadPath);
        var filePath = Path.Combine(downloadPath, Path.GetFileName(file));
        await using var resultStream = new FileStream(filePath, FileMode.CreateNew);
        var progress = new Progress<float>();
        progress.ProgressChanged += (_, f) => onProgressChanged(f);
        await client.DownloadAsync(downloadAddress, resultStream, progress);
        return filePath;
    }
}
