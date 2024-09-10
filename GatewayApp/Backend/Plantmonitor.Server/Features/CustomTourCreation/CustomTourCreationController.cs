using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Shared.Features.ImageStreaming;

namespace Plantmonitor.Server.Features.CustomTourCreation;

[ApiController]
[Route("api/[controller]")]
public class CustomTourCreationController(IEnvironmentConfiguration configuration, IDataContext context)
{
    private static Dictionary<string, UploadProgress> s_uploadProgress = new();
    private record struct NewFolderEntry(string IrPath, string VisPath);
    public record class UploadProgress(string Status, int ExtractedImages, int CreatedTrips)
    {
        public string Status { get; set; } = Status;
        public int ExtractedImages { get; set; } = ExtractedImages;
        public int CreatedTrips { get; set; } = CreatedTrips;
    }

    public record class AddNewCustomTour([MinLength(3)] string Name, [MinLength(3)] string Comment,
        string PixelSizeInMm, IFormFile File, string ProgressGuid);

    [HttpPost("uploadcustomtour")]
    [RequestSizeLimit(1024L * 1024L * 1024L * 1024L)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1024L * 1024L * 1024L * 1024L)]
    public void UploadFile([FromForm] AddNewCustomTour tourData)
    {
        using var transaction = context.Database.BeginTransaction();
        s_uploadProgress.Remove(tourData.ProgressGuid);
        s_uploadProgress.Add(tourData.ProgressGuid, new("Starting Extraction", 0, 0));
        var newTour = new DataModel.DataModel.AutomaticPhotoTour()
        {
            Comment = tourData.Comment,
            DeviceId = Guid.NewGuid(),
            Finished = true,
            IntervallInMinutes = 0.1f,
            Name = tourData.Name.SanitizeFileName(),
            PixelSizeInMm = float.TryParse(tourData.PixelSizeInMm, CultureInfo.InvariantCulture, out var pixelSize) ? pixelSize :
            throw new Exception("Invalid Pixelsize"),
        };
        context.AutomaticPhotoTours.Add(newTour);
        context.SaveChanges();
        context.PhotoTourEvents.Add(new PhotoTourEvent()
        {
            Message = "Custom Tour created",
            PhotoTourFk = newTour.Id,
            Timestamp = DateTime.UtcNow,
            Type = PhotoTourEventType.Information,
        });
        context.SaveChanges();
        var imagePath = configuration.PicturePath(newTour.DeviceId.ToString());
        using var fileStream = tourData.File.OpenReadStream();
        using var archive = new ZipArchive(fileStream);
        s_uploadProgress[tourData.ProgressGuid].Status = "Extracting Images";
        foreach (var entry in archive.Entries)
        {
            s_uploadProgress[tourData.ProgressGuid].ExtractedImages++;
            entry.ExtractToFile(Path.Combine(imagePath, entry.Name.SanitizeFileName()), true);
        }
        var newFolderEntry = new NewFolderEntry();
        s_uploadProgress[tourData.ProgressGuid].Status = "Creating Phototrips";
        foreach (var fileData in Directory.GetFiles(imagePath)
            .Select(p => new { Path = p, Success = CameraStreamFormatter.FromFileLazy(p, out var result), Info = result })
            .OrderBy(f => f.Info.Timestamp))
        {
            if (!fileData.Success)
            {
                File.Delete(fileData.Path);
                continue;
            }
            if (fileData.Info.GetCameraType() == CameraType.Vis)
            {
                if (!newFolderEntry.VisPath.IsEmpty()) File.Delete(newFolderEntry.VisPath);
                newFolderEntry = new("", fileData.Path);
            }
            else if (fileData.Info.GetCameraType() == CameraType.IR && !newFolderEntry.VisPath.IsEmpty())
            {
                if (fileData.Path.GetBytesFromIrFilePath(out _).Bytes.Length != ImageConstants.IrPixelCount)
                {
                    File.Delete(fileData.Path);
                    continue;
                }
                s_uploadProgress[tourData.ProgressGuid].CreatedTrips++;
                newFolderEntry.IrPath = fileData.Path;
                var visFolder = MoveToImageFolder(imagePath, newFolderEntry.VisPath, fileData.Info.Timestamp, CameraType.Vis);
                var irFolder = MoveToImageFolder(imagePath, newFolderEntry.IrPath, fileData.Info.Timestamp.AddMilliseconds(10), CameraType.IR);
                newFolderEntry = new();
                context.PhotoTourTrips.Add(new PhotoTourTrip()
                {
                    IrDataFolder = irFolder,
                    VisDataFolder = visFolder,
                    PhotoTourFk = newTour.Id,
                    Timestamp = fileData.Info.Timestamp,
                });
            }
            else
            {
                File.Delete(fileData.Path);
            }
        }
        context.SaveChanges();
        transaction.Commit();
        s_uploadProgress[tourData.ProgressGuid].Status = "Phototour successfully added";
    }

    [HttpGet("uploadprogress")]
    public UploadProgress? GetUploadProgress(string progressGuid)
    {
        return s_uploadProgress.TryGetValue(progressGuid, out var progress) ? progress : null;
    }

    private static string MoveToImageFolder(string imagePath, string fileToMove, DateTime timestamp, CameraType type)
    {
        var sequenceId = timestamp.ToString(CameraStreamFormatter.PictureDateFormat);
        var newFolder = Path.Combine(imagePath, sequenceId);
        Directory.CreateDirectory(newFolder);
        var bytes = type == CameraType.IR ? fileToMove.GetBytesFromIrFilePath(out _).Bytes : File.ReadAllBytes(fileToMove);
        File.WriteAllBytes(Path.Combine(newFolder, Path.GetFileName(fileToMove)), bytes);
        File.Delete(fileToMove);
        return newFolder;
    }
}
