using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices.Marshalling;
using Emgu.CV;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Shared.Features.ImageStreaming;

namespace Plantmonitor.Server.Features.ImageStitching;

public interface IVirtualImageWorker
{
    void RecalculateTour(long photoTourId);

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);
}

public class VirtualImageWorker(IServiceScopeFactory scopeFactory, IEnvironmentConfiguration configuration,
    ILogger<VirtualImageWorker> logger) : IHostedService, IVirtualImageWorker
{
    private static readonly HashSet<long> s_tripsToSkip = [];
    private static int s_imageCalculationTimeout = 10;
    private const int MaxImageCalculationTimeout = 60;
    private const int MinImageCalculationTimeout = 1;
    public const int VirtualPlantImageCropHeight = 960;
    private Timer? _timer;

    public void RecalculateTour(long photoTourId)
    {
        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var tripsToRecalculate = new HashSet<long>();
        foreach (var trip in dataContext.PhotoTourTrips.Where(ptt => ptt.PhotoTourFk == photoTourId))
        {
            tripsToRecalculate.Add(trip.Id);
            if (Path.Exists(trip.VirtualPicturePath)) File.Delete(trip.VirtualPicturePath);
            trip.VirtualPicturePath = null;
        }
        dataContext.SaveChanges();
        s_tripsToSkip.RemoveWhere(tripsToRecalculate.Contains);
    }

    private void CreateVirtualImage()
    {
        _timer?.Dispose();
        using var scope = scopeFactory.CreateScope();
        using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var stitcher = scope.ServiceProvider.GetRequiredService<IPhotoStitcher>();
        var cropper = scope.ServiceProvider.GetRequiredService<IImageCropper>();
        Action createImages = () => RunImageCreation(dataContext, stitcher, cropper, configuration);
        createImages.Try(ex => ex.LogError());
        _timer = CreateNewTimer();
    }

    private Timer CreateNewTimer()
    {
        return new Timer(_ => CreateVirtualImage(), default, (int)TimeSpan.FromSeconds(s_imageCalculationTimeout).TotalMilliseconds, Timeout.Infinite);
    }

    public void RunImageCreation(IDataContext dataContext, IPhotoStitcher stitcher, IImageCropper cropper, IEnvironmentConfiguration configuration)
    {
        logger.LogInformation("Running virtual image creation");
        var tripsToSkipArray = s_tripsToSkip.ToArray();
        var currentTrip = dataContext.PhotoTourTrips
            .Include(ttp => ttp.PhotoTourFkNavigation)
            .Where(apt => apt.VirtualPicturePath == null && !tripsToSkipArray.Contains(apt.Id))
            .OrderByDescending(apt => apt.PhotoTourFk)
            .FirstOrDefault();
        if (currentTrip == null)
        {
            s_imageCalculationTimeout = Math.Min(s_imageCalculationTimeout * 2, MaxImageCalculationTimeout);
            logger.LogInformation("No trips to process for virtual image creation. Exiting");
            return;
        }
        s_imageCalculationTimeout = MinImageCalculationTimeout;
        logger.LogInformation("Processing Tour {tour}", currentTrip.PhotoTourFkNavigation.Name);
        var extractionTemplates = dataContext.PlantExtractionTemplates
            .Include(pet => pet.PhotoTripFkNavigation)
            .Include(pet => pet.PhotoTourPlantFkNavigation)
            .Where(pet => pet.PhotoTripFkNavigation.PhotoTourFk == currentTrip.PhotoTourFk)
            .OrderByDescending(pet => pet.PhotoTripFkNavigation.Timestamp)
            .ToList();
        if (extractionTemplates.Count == 0)
        {
            logger.LogWarning("No extraction templates defined for tour {tour}. Exiting trip {trip}", currentTrip.PhotoTourFkNavigation.Name, currentTrip.Id);
            foreach (var trip in dataContext.PhotoTourTrips.Where(ptt => ptt.PhotoTourFk == currentTrip.PhotoTourFk)) s_tripsToSkip.Add(trip.Id);
            return;
        }
        var plantsOfTour = dataContext.PhotoTourPlants.Where(ptp => ptp.PhotoTourFk == currentTrip.PhotoTourFk).ToList();
        var virtualImageFolder = configuration.VirtualImagePath(currentTrip.PhotoTourFkNavigation.Name, currentTrip.PhotoTourFk);

        var virtualImageFile = currentTrip.VirtualImageFileName(virtualImageFolder);
        logger.LogInformation("Processing virtual image {image} of tour {tour}", virtualImageFile, currentTrip.PhotoTourFkNavigation.Name);
        var virtualImageList = new List<PhotoStitcher.PhotoStitchData>();
        foreach (var plant in plantsOfTour
            .Select(pot => (Number: pot.Name.ExtractNumbersFromString(out var cleanText), CleanText: cleanText, Plant: pot))
            .OrderBy(pot => pot.CleanText)
            .ThenBy(pot => pot.Number)
            .Select(pot => pot.Plant))
        {
            logger.LogInformation("Adding plant {plant} to virtual image {image}", $"{plant.Name} {plant.Comment}", virtualImageFile);
            var extractionTemplate = extractionTemplates
                .Where(et => et.PhotoTripFkNavigation.Timestamp <= currentTrip.Timestamp && et.PhotoTourPlantFk == plant.Id)
                .MaxBy(et => et.PhotoTripFkNavigation.Timestamp);
            logger.LogInformation("Using extraction from {template} for position {position}",
                extractionTemplate?.PhotoTripFkNavigation.Timestamp.ToString("yyyy.MM.dd HH:mm:ss") ?? "NA", extractionTemplate?.MotorPosition.ToString() ?? "NA");
            virtualImageList.Add(new PhotoStitcher.PhotoStitchData()
            {
                Comment = plant.Comment,
                Name = plant.Name,
            });
            if (extractionTemplate == null || !Path.Exists(currentTrip.VisDataFolder) || !Path.Exists(currentTrip.IrDataFolder)) continue;
            var visImage = CameraStreamFormatter.FindInFolder(currentTrip.VisDataFolder, extractionTemplate.MotorPosition);
            var irImage = CameraStreamFormatter.FindInFolder(currentTrip.IrDataFolder, extractionTemplate.MotorPosition);

            if (visImage.FileName == null || visImage.Formatter == null)
            {
                logger.LogWarning("No vis image found. Moving to next plant.");
                continue;
            }
            logger.LogInformation("Using images vis: {vis} and ir: {ir} for cropping", visImage.FileName, irImage.FileName ?? "NA");
            var matResults = cropper.CropImages(visImage.FileName, irImage.FileName, [.. extractionTemplate.PhotoBoundingBox],
                extractionTemplate.IrBoundingBoxOffset, VirtualPlantImageCropHeight);
            virtualImageList[^1].VisImage = matResults.VisImage;
            virtualImageList[^1].VisImageTime = visImage.Formatter.Timestamp;
            virtualImageList[^1].MotorPosition = visImage.Formatter.Steps;
            if (irImage.Formatter == null || irImage.FileName == null) continue;
            virtualImageList[^1].IrImageTime = irImage.Formatter.Timestamp;
            virtualImageList[^1].IrTemperatureInK = irImage.Formatter.TemperatureInK;
            if (matResults.IrImage?.Cols == 0 || matResults.IrImage?.Rows == 0) continue;
            var colorMat = matResults.IrImage?.Clone();
            if (matResults.IrImage != null) virtualImageList[^1].IrImageRawData = cropper.CreateRawIr(matResults.IrImage);
            if (colorMat != null) cropper.ApplyIrColorMap(colorMat);
            virtualImageList[^1].ColoredIrImage = colorMat;
        }
        logger.LogInformation("Stitching virtual image together");
        var maxHeight = virtualImageList.Select(v => v.VisImage?.Height ?? 10).OrderByDescending(h => h).FirstOrDefault();
        var maxWidth = virtualImageList.Select(v => v.VisImage?.Width ?? 10).OrderByDescending(h => h).FirstOrDefault();
        var virtualImage = stitcher.CreateVirtualImage(virtualImageList, maxWidth, maxHeight, currentTrip.PhotoTourFkNavigation.PixelSizeInMm);
        logger.LogInformation("Fetching additional metadata");
        var fullMetaDataTable = AddAdditionalMetaData(dataContext, currentTrip, currentTrip.PhotoTourFkNavigation, virtualImage.MetaData);

        var fileBaseName = Path.GetFileNameWithoutExtension(virtualImageFile);
        logger.LogInformation("Storing virtual image {image}", virtualImageFile);
        using (var zipStream = new MemoryStream())
        {
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                AddMat(virtualImageFolder + $"/{PhotoTourTrip.IrPrefix}{fileBaseName}.png", virtualImage.IrColorImage, zip);
                AddMat(virtualImageFolder + $"/{PhotoTourTrip.VisPrefix}{fileBaseName}.png", virtualImage.VisImage, zip);
                AddMat(virtualImageFolder + $"/{PhotoTourTrip.RawIrPrefix}{fileBaseName}.png", virtualImage.IrRawData, zip);
                var tsvPath = virtualImageFolder + $"/{PhotoTourTrip.MetaDataPrefix}{fileBaseName}.tsv";
                File.WriteAllText(tsvPath, fullMetaDataTable.ExportAsTsv());
                var fileName = Path.GetFileName(tsvPath);
                zip.CreateEntryFromFile(tsvPath, fileName);
                File.Delete(tsvPath);
            }
            using var resultFile = new FileStream(virtualImageFile, FileMode.Create);
            zipStream.Seek(0, SeekOrigin.Begin);
            zipStream.CopyTo(resultFile);
        }
        currentTrip.VirtualPicturePath = virtualImageFile;
        dataContext.SaveChanges();
        virtualImageList.DisposeItems();
        logger.LogInformation("Finished virtual image creation of trip {trip}", currentTrip.Timestamp.ToString("yyyy.MM.dd HH:mm:ss"));
    }

    private VirtualImageMetaDataModel AddAdditionalMetaData(IDataContext dataContext, PhotoTourTrip tripToProcess,
        DataModel.DataModel.AutomaticPhotoTour photoTour, VirtualImageMetaDataModel metaData)
    {
        var to = tripToProcess.Timestamp;
        var previousTrip = dataContext.PhotoTourTrips
            .Where(ptt => ptt.PhotoTourFk == tripToProcess.PhotoTourFk && ptt.Timestamp < to)
            .OrderByDescending(ptt => ptt.Timestamp)
            .FirstOrDefault();
        var timeToNextTrip = to - previousTrip?.Timestamp;
        var from = to.Add(-timeToNextTrip ?? -TimeSpan.FromMinutes(photoTour.IntervallInMinutes));
        logger.LogInformation("Fetching Temperatures from {from} to {to}", from, to);
        var temperaturesOfTrip = dataContext.TemperatureMeasurementValues
            .Include(tmv => tmv.MeasurementFkNavigation)
            .Where(tm => tm.MeasurementFkNavigation.PhotoTourFk == tripToProcess.PhotoTourFk && tm.Timestamp >= from && tm.Timestamp <= to)
            .OrderBy(tm => tm.Timestamp)
            .Take(10000)
            .ToList();
        metaData.TimeInfos = new(from, to, photoTour.Name, photoTour.Id, tripToProcess.Id);
        logger.LogInformation("Creating temperature table");
        var measurementValues = temperaturesOfTrip
            .Where(tot => !tot.MeasurementFkNavigation.IsThermalCamera())
            .OrderBy(tot => tot.MeasurementFk)
            .ThenBy(tot => tot.Timestamp)
            .Select(tot => new
            {
                tot.MeasurementFkNavigation.Comment,
                tot.MeasurementFkNavigation.SensorId,
                tot.Temperature,
                tot.Timestamp
            });
        foreach (var value in measurementValues) metaData.TemperatureReadings.Add(new(value.SensorId, value.Comment, value.Temperature, value.Timestamp));
        return metaData;
    }

    private static void AddMat(string path, Mat mat, ZipArchive zip)
    {
        CvInvoke.Imwrite(path, mat);
        var fileName = Path.GetFileName(path);
        zip.CreateEntryFromFile(path, fileName);
        File.Delete(path);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = CreateNewTimer();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }
}
