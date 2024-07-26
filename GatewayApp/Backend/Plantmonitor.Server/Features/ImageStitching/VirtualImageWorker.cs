using Emgu.CV;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Shared.Features.ImageStreaming;

namespace Plantmonitor.Server.Features.ImageStitching;

public class VirtualImageWorker(IServiceScopeFactory scopeFactory, IPhotoStitcher stitcher, IImageCropper cropper, IEnvironmentConfiguration configuration) : IHostedService
{
    private Timer? _timer;

    public void RecalculateTour(long photoTourId)
    {
        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        foreach (var trip in dataContext.PhotoTourTrips.Where(ptt => ptt.PhotoTourFk == photoTourId))
        {
            if (Path.Exists(trip.VirtualPicturePath)) File.Delete(trip.VirtualPicturePath);
            trip.VirtualPicturePath = null;
        }
        dataContext.SaveChanges();
    }

    public void CreateVirtualImage()
    {
        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var tripToProcess = dataContext.PhotoTourTrips
            .Include(ttp => ttp.PhotoTourFkNavigation)
            .OrderByDescending(apt => apt.PhotoTourFk)
            .Where(apt => apt.VirtualPicturePath == null)
            .FirstOrDefault();
        if (tripToProcess == null) return;
        var existingTrip = dataContext.PhotoTourTrips
            .Where(ptt => ptt.PhotoTourFk == tripToProcess.PhotoTourFk)
            .OrderByDescending(ptt => ptt.Timestamp)
            .FirstOrDefault(ptt => ptt.VirtualPicturePath != null);
        var extractionTemplates = dataContext.PlantExtractionTemplates
            .Include(pet => pet.PhotoTripFkNavigation)
            .Include(pet => pet.PhotoTourPlantFkNavigation)
            .Where(pet => pet.PhotoTripFkNavigation.PhotoTourFk == tripToProcess.PhotoTourFk &&
                          (existingTrip == null || pet.PhotoTripFkNavigation.Timestamp <= existingTrip.Timestamp))
            .OrderByDescending(pet => pet.PhotoTripFkNavigation.Timestamp)
            .ToList();
        if (extractionTemplates.Count == 0) return;
        var maxBoundingBoxHeight = extractionTemplates.Max(bb => bb.BoundingBoxHeight);
        var maxBoundingBoxWidth = extractionTemplates.Max(bb => bb.BoundingBoxWidth);
        var imagesToCreate = dataContext.PhotoTourTrips
            .Where(ptt => (existingTrip == null || ptt.Timestamp <= existingTrip.Timestamp) && ptt.VirtualPicturePath == null)
            .ToList();
        var plantsOfTour = dataContext.PhotoTourPlants.Where(ptp => ptp.PhotoTourFk == tripToProcess.PhotoTourFk).ToList();
        var virtualImageFolder = configuration.VirtualImagePath(tripToProcess.PhotoTourFkNavigation.Name, tripToProcess.PhotoTourFk);
        foreach (var image in imagesToCreate)
        {
            var virtualImageFile = $"{virtualImageFolder}/trip_{image.Timestamp:yyyyMMdd_HHmmss_fff}.tiff";
            var virtualImageList = new List<PhotoStitcher.PhotoStitchData>();
            foreach (var plant in plantsOfTour.OrderBy(pot => pot.Name).ThenBy(pot => pot.Id))
            {
                var extractionTemplate = extractionTemplates
                    .Where(et => et.PhotoTripFkNavigation.Timestamp <= image.Timestamp && et.PhotoTourPlantFk == plant.Id)
                    .MaxBy(et => et.PhotoTripFkNavigation.Timestamp);
                virtualImageList.Add(new PhotoStitcher.PhotoStitchData()
                {
                    Comment = plant.Comment,
                    Name = plant.Name,
                });
                if (extractionTemplate == null) continue;
                var visImage = Directory.GetFiles(image.VisDataFolder)
                    .Select(vi => new { Success = CameraStreamFormatter.FromFileLazy(vi, out var formatter), File = vi, Formatter = formatter })
                    .FirstOrDefault(f => f.Success && f.Formatter.Steps == extractionTemplate.MotorPosition);
                var irImage = Directory.GetFiles(image.IrDataFolder)
                    .Select(vi => new { Success = CameraStreamFormatter.FromFileLazy(vi, out var formatter), File = vi, Formatter = formatter })
                    .FirstOrDefault(f => f.Success && f.Formatter.Steps == extractionTemplate.MotorPosition);

                if (visImage == null) continue;
                var matResults = cropper.CropImages(visImage.File, irImage?.File, [.. extractionTemplate.PhotoBoundingBox], extractionTemplate.IrBoundingBoxOffset);
                virtualImageList[^1].VisImage = matResults.VisImage;
                virtualImageList[^1].IrImage = matResults.IrImage;
            }
            var virtualImage = stitcher.CreateVirtualImage(virtualImageList, maxBoundingBoxWidth, maxBoundingBoxHeight, 50f);
            CvInvoke.Imwrite(virtualImageFile, virtualImage);
            image.VirtualPicturePath = virtualImageFile;
            dataContext.SaveChanges();
            virtualImageList.DisposeItems();
            virtualImage?.Dispose();
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(_ => CreateVirtualImage(), default, (int)TimeSpan.FromSeconds(10).TotalMilliseconds, (int)TimeSpan.FromSeconds(10).TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }
}
