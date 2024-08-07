using System.Globalization;
using System.IO.Compression;
using Emgu.CV;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
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
    private const int VirtualPlantImageCropHeight = 960;
    private Timer? _timer;
    private static readonly object s_lock = new();
    private static bool s_isRunning;

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

    private void CreateVirtualImage()
    {
        lock (s_lock)
        {
            if (s_isRunning) return;
            s_isRunning = true;
        }
        using var scope = scopeFactory.CreateScope();
        using var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var stitcher = scope.ServiceProvider.GetRequiredService<IPhotoStitcher>();
        var cropper = scope.ServiceProvider.GetRequiredService<IImageCropper>();
        Action createImages = () => RunImageCreation(dataContext, stitcher, cropper, configuration);
        createImages.Try(ex => ex.LogError());
        lock (s_lock) s_isRunning = false;
    }

    public void RunImageCreation(IDataContext dataContext, IPhotoStitcher stitcher, IImageCropper cropper, IEnvironmentConfiguration configuration)
    {
        logger.LogInformation("Running virtual image creation");
        var tripToProcess = dataContext.PhotoTourTrips
            .Include(ttp => ttp.PhotoTourFkNavigation)
            .OrderByDescending(apt => apt.PhotoTourFk)
            .Where(apt => apt.VirtualPicturePath == null)
            .FirstOrDefault();
        if (tripToProcess == null)
        {
            logger.LogInformation("No trips to process for virtual image creation. Exiting");
            return;
        }
        logger.LogInformation("Processing Tour {tour}", tripToProcess.PhotoTourFkNavigation.Name);
        var extractionTemplates = dataContext.PlantExtractionTemplates
            .Include(pet => pet.PhotoTripFkNavigation)
            .Include(pet => pet.PhotoTourPlantFkNavigation)
            .Where(pet => pet.PhotoTripFkNavigation.PhotoTourFk == tripToProcess.PhotoTourFk)
            .OrderByDescending(pet => pet.PhotoTripFkNavigation.Timestamp)
            .ToList();
        if (extractionTemplates.Count == 0)
        {
            logger.LogWarning("No extraction templates defined for tour {tour}. Exiting", tripToProcess.PhotoTourFkNavigation.Name);
            return;
        }
        var maxBoundingBoxHeight = (int)float.Ceiling(extractionTemplates.Max(bb => bb.BoundingBoxHeight));
        var maxBoundingBoxWidth = (int)float.Ceiling(extractionTemplates.Max(bb => bb.BoundingBoxWidth));
        var imagesToCreate = dataContext.PhotoTourTrips
            .Where(ptt => ptt.VirtualPicturePath == null && ptt.PhotoTourFk == tripToProcess.PhotoTourFk)
            .ToList();
        var plantsOfTour = dataContext.PhotoTourPlants.Where(ptp => ptp.PhotoTourFk == tripToProcess.PhotoTourFk).ToList();
        var virtualImageFolder = configuration.VirtualImagePath(tripToProcess.PhotoTourFkNavigation.Name, tripToProcess.PhotoTourFk);

        foreach (var image in imagesToCreate)
        {
            var virtualImageFile = $"{virtualImageFolder}/trip_{image.Timestamp:yyyyMMdd_HHmmss_fff}.zip";
            logger.LogInformation("Processing virtual image {image} of tour {tour}", virtualImageFile, tripToProcess.PhotoTourFkNavigation.Name);
            var virtualImageList = new List<PhotoStitcher.PhotoStitchData>();
            foreach (var plant in plantsOfTour
                .Select(pot => (Number: pot.Name.ExtractNumbersFromString(out var cleanText), CleanText: cleanText, Plant: pot))
                .OrderBy(pot => pot.CleanText)
                .ThenBy(pot => pot.Number)
                .Select(pot => pot.Plant))
            {
                logger.LogInformation("Adding plant {plant} to virtual image {image}", $"{plant.Name} {plant.Comment}", virtualImageFile);
                var extractionTemplate = extractionTemplates
                    .Where(et => et.PhotoTripFkNavigation.Timestamp <= image.Timestamp && et.PhotoTourPlantFk == plant.Id)
                    .MaxBy(et => et.PhotoTripFkNavigation.Timestamp);
                logger.LogInformation("Using extraction from {template} for position {position}",
                    extractionTemplate?.PhotoTripFkNavigation.Timestamp.ToString("yyyy.MM.dd HH:mm:ss") ?? "NA", extractionTemplate?.MotorPosition.ToString() ?? "NA");
                virtualImageList.Add(new PhotoStitcher.PhotoStitchData()
                {
                    Comment = plant.Comment,
                    Name = plant.Name,
                });
                if (extractionTemplate == null || !Path.Exists(image.VisDataFolder) || !Path.Exists(image.IrDataFolder)) continue;
                var visImage = Directory.GetFiles(image.VisDataFolder)
                    .Select(vi => new { Success = CameraStreamFormatter.FromFileLazy(vi, out var formatter), File = vi, Formatter = formatter })
                    .FirstOrDefault(f => f.Success && f.Formatter.Steps == extractionTemplate.MotorPosition);
                var irImage = Directory.GetFiles(image.IrDataFolder)
                    .Select(vi => new { Success = CameraStreamFormatter.FromFileLazy(vi, out var formatter), File = vi, Formatter = formatter })
                    .FirstOrDefault(f => f.Success && f.Formatter.Steps == extractionTemplate.MotorPosition);

                if (visImage == null)
                {
                    logger.LogWarning("No vis image found. Moving to next plant.");
                    continue;
                }
                logger.LogInformation("Using images vis: {vis} and ir: {ir} for cropping", visImage.File, irImage?.File ?? "NA");
                var matResults = cropper.CropImages(visImage.File, irImage?.File, [.. extractionTemplate.PhotoBoundingBox],
                    extractionTemplate.IrBoundingBoxOffset, VirtualPlantImageCropHeight);
                virtualImageList[^1].VisImage = matResults.VisImage;
                virtualImageList[^1].VisImageTime = visImage.Formatter.Timestamp;
                if (irImage == null) continue;
                virtualImageList[^1].IrImageTime = irImage.Formatter.Timestamp;
                virtualImageList[^1].IrTemperatureInK = irImage.Formatter.TemperatureInK;
                if (matResults.IrImage?.Cols == 0 || matResults.IrImage?.Rows == 0) continue;
                var colorMat = matResults.IrImage?.Clone();
                if (matResults.IrImage != null) virtualImageList[^1].IrImageRawData = cropper.CreateRawIr(matResults.IrImage);
                if (colorMat != null) cropper.ApplyIrColorMap(colorMat);
                virtualImageList[^1].ColoredIrImage = colorMat;
            }
            logger.LogInformation("Stitching virtual image together");
            var virtualImage = stitcher.CreateVirtualImage(virtualImageList, maxBoundingBoxWidth, maxBoundingBoxHeight);
            logger.LogInformation("Fetching additional metadata");
            var fullMetaDataTable = AddAditionalMetaData(dataContext, tripToProcess, virtualImageList, virtualImage);

            var fileBaseName = Path.GetFileNameWithoutExtension(virtualImageFile);
            logger.LogInformation("Storing virtual image {image}", virtualImageFile);
            using (var zipStream = new MemoryStream())
            {
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    AddMat(virtualImageFolder + $"/ir_{fileBaseName}.png", virtualImage.IrColorImage, zip);
                    AddMat(virtualImageFolder + $"/vis_{fileBaseName}.png", virtualImage.VisImage, zip);
                    AddMat(virtualImageFolder + $"/rawIr_{fileBaseName}.png", virtualImage.IrRawData, zip);
                    var tsvPath = virtualImageFolder + $"/data_{fileBaseName}.tsv";
                    File.WriteAllText(tsvPath, fullMetaDataTable);
                    var fileName = Path.GetFileName(tsvPath);
                    zip.CreateEntryFromFile(tsvPath, fileName);
                    File.Delete(tsvPath);
                }
                using var resultFile = new FileStream(virtualImageFile, FileMode.Create);
                zipStream.Seek(0, SeekOrigin.Begin);
                zipStream.CopyTo(resultFile);
            }
            image.VirtualPicturePath = virtualImageFile;
            dataContext.SaveChanges();
            virtualImageList.DisposeItems();
            logger.LogInformation("Finished virtual image creation of trip {trip}", image.Timestamp.ToString("yyyy.MM.dd HH:mm:ss"));
        }
        logger.LogInformation("All trips of tour {tour} processed", tripToProcess.PhotoTourFkNavigation.Name);
    }

    private string AddAditionalMetaData(IDataContext dataContext, PhotoTourTrip tripToProcess, List<PhotoStitcher.PhotoStitchData> virtualImageList, (Mat VisImage, Mat IrColorImage, Mat IrRawData, string MetaDataTable) virtualImage)
    {
        var from = tripToProcess.Timestamp;
        var to = from.Add(TimeSpan.FromMinutes(tripToProcess.PhotoTourFkNavigation.IntervallInMinutes));
        logger.LogInformation("Fetching Temperatures from {from} to {to}", from, to);
        var temperaturesOfTrip = dataContext.TemperatureMeasurementValues
            .Include(tmv => tmv.MeasurementFkNavigation)
            .Where(tm => tm.MeasurementFkNavigation.PhotoTourFk == tripToProcess.PhotoTourFk && tm.Timestamp >= from && tm.Timestamp <= to)
            .OrderBy(tm => tm.Timestamp)
            .Take(10000)
            .ToList();
        logger.LogInformation("Creating temperature table");
        var timeTable = $"\nStart Time\tEnd Time\n{from:yyyy.MM.dd_HH:mm:ss}\t{to:yyyy.MM.dd_HH:mm:ss}";
        var measurementValues = temperaturesOfTrip
            .Where(tot => !tot.MeasurementFkNavigation.IsThermalCamera())
            .OrderBy(tot => tot.MeasurementFk)
            .ThenBy(tot => tot.Timestamp)
            .Select(tot => $"{tot.MeasurementFkNavigation.Comment}_{tot.MeasurementFkNavigation.SensorId}\t" +
            $"{tot.Temperature.ToString("0.00 °C", CultureInfo.InvariantCulture)}\t{tot.Timestamp:yyyy.MM.dd_HH:mm:ss}")
            .Concat("\n");
        var temperatureTable = $"\nDevice\tTemperature\tTime\n{measurementValues}";
        return $"{virtualImage.MetaDataTable}\n{timeTable}\n{temperatureTable}";
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
        _timer = new Timer(_ => CreateVirtualImage(), default, (int)TimeSpan.FromSeconds(10).TotalMilliseconds, (int)TimeSpan.FromSeconds(10).TotalMilliseconds);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }
}
