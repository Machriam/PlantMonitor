using Microsoft.AspNetCore.Mvc;
using PlantMonitorControl.Features.AppsettingsConfiguration;

namespace PlantMonitorControl.Features.ImageTaking;

[ApiController]
[Route("api/[controller]")]
public class CustomTourController(IEnvironmentConfiguration configuration)
{
    public record struct AvailableCustomData(float SizeInMb, string FileName);

    [HttpGet("availabledata")]
    public IEnumerable<AvailableCustomData> AvailableData()
    {
        const float InverseMegaByte = 1f / (1024 * 1024);
        return Directory.EnumerateFiles(configuration.StreamArchivePath)
            .Select(f => new AvailableCustomData(new System.IO.FileInfo(f).Length * InverseMegaByte, Path.GetFileName(f)));
    }

    [HttpGet("downloadfile")]
    public Stream DownloadFile(string fileName)
    {
        var path = Path.Combine(configuration.StreamArchivePath, fileName);
        return new FileStream(path, FileMode.Open);
    }

    [HttpPost("deletefile")]
    public void DeleteFile(string fileName)
    {
        var path = Path.Combine(configuration.StreamArchivePath, fileName);
        File.Delete(path);
    }
}
