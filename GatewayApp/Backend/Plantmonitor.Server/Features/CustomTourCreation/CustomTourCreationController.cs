using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO.Compression;
using Microsoft.AspNetCore.Mvc;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Server.Features.ImageStitching;
using Plantmonitor.Shared.Features.ImageStreaming;

namespace Plantmonitor.Server.Features.CustomTourCreation;

[ApiController]
[Route("api/[controller]")]
public class CustomTourCreationController(IEnvironmentConfiguration configuration, IDataContext context)
{
    private record struct NewFolderEntry(string IrPath, string VisPath);
    public record class AddNewCustomTour([MinLength(3)] string Name, [MinLength(3)] string Comment,
        string PixelSizeInMm, IFormFile File);

    [HttpPost("uploadcustomtour")]
    [RequestSizeLimit(1024L * 1024L * 1024L * 1024L)]
    [RequestFormLimits(MultipartBodyLengthLimit = 1024L * 1024L * 1024L * 1024L)]
    public void UploadFile([FromForm] AddNewCustomTour tourData)
    {
        using var transaction = context.Database.BeginTransaction();
        var newTour = new DataModel.DataModel.AutomaticPhotoTour()
        {
            Comment = tourData.Comment,
            DeviceId = Guid.NewGuid(),
            Finished = true,
            IntervallInMinutes = float.MaxValue,
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
        foreach (var entry in archive.Entries)
        {
            entry.ExtractToFile(Path.Combine(imagePath, entry.Name.SanitizeFileName()), true);
        }
        var newFolderEntry = new NewFolderEntry();
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
                newFolderEntry.IrPath = fileData.Path;
                var visFolder = MoveToImageFolder(imagePath, newFolderEntry.VisPath, fileData.Info.Timestamp);
                var irFolder = MoveToImageFolder(imagePath, newFolderEntry.IrPath, fileData.Info.Timestamp.AddMilliseconds(10));
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
    }

    private static string MoveToImageFolder(string imagePath, string fileToMove, DateTime timestamp)
    {
        var sequenceId = timestamp.ToString(CameraStreamFormatter.PictureDateFormat);
        var newFolder = Path.Combine(imagePath, sequenceId);
        Directory.CreateDirectory(newFolder);
        File.WriteAllBytes(Path.Combine(newFolder, Path.GetFileName(fileToMove)), File.ReadAllBytes(fileToMove));
        File.Delete(fileToMove);
        return newFolder;
    }
}
