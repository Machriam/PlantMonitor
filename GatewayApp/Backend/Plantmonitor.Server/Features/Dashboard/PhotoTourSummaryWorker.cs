using System.Collections.Concurrent;
using System.Drawing;
using System.IO.Compression;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.AutomaticPhotoTour;

namespace Plantmonitor.Server.Features.Dashboard;

public interface IPhotoTourSummaryWorker
{
    IManagedMat GetPlantMask(IManagedMat visMat, SegmentationTemplate parameter);

    void RecalculateSummaries(long photoTourId);

    PhotoSummaryResult ProcessImage(string image, SegmentationTemplate segmentationTemplate);

    List<IManagedMat> SplitInSubImages(string image, HashSet<string> desiredPlants);
}

public class PhotoTourSummaryWorker(IEnvironmentConfiguration configuration,
    IServiceScopeFactory scopeFactory, ILogger<PhotoTourSummaryWorker> logger) : IHostedService, IPhotoTourSummaryWorker
{
    private Timer? _processImageTimer;
    private Timer? _processFindImagesToProcessTimer;
    private static readonly ConcurrentDictionary<string, DateTime> s_imagesToProcess = new();
    private static readonly object s_processingLock = new();
    private static readonly object s_imageFindingLock = new();
    private static bool s_isProcessing;
    private const string FileLookupDate = "yyyy-MM-dd_HH-mm-ss";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _processImageTimer = new Timer(_ => FindNextImageToProcess(), default, (int)TimeSpan.FromSeconds(10).TotalMilliseconds, (int)TimeSpan.FromSeconds(1).TotalMilliseconds);
        _processFindImagesToProcessTimer = new Timer(_ => FindImagesToProcess(), default,
            (int)TimeSpan.FromSeconds(20).TotalMilliseconds, (int)TimeSpan.FromMinutes(2).TotalMilliseconds);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processImageTimer == null || _processFindImagesToProcessTimer == null) return;
        await _processImageTimer.DisposeAsync();
        await _processFindImagesToProcessTimer.DisposeAsync();
    }

    public void RecalculateSummaries(long photoTourId)
    {
        using var scope = scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var summariesToRemove = dataContext.VirtualImageSummaryByPhotoTourIds
            .Where(vis => vis.PhotoTourId == null || vis.PhotoTourId <= 0 || vis.PhotoTourId == photoTourId)
            .Select(vis => vis.Id)
            .ToHashSet();
        dataContext.VirtualImageSummaries.RemoveRange(dataContext.VirtualImageSummaries.Where(vis => summariesToRemove.Contains(vis.Id)));
        dataContext.SaveChanges();
        FindImagesToProcess();
    }

    public void FindImagesToProcess()
    {
        logger.LogInformation("Search virtual images to process");
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var existingResults = context.VirtualImageSummaries
            .Select(vis => new { vis.VirtualImageCreationDate, vis.VirtualImagePath })
            .ToList()
            .Select(x => (x.VirtualImageCreationDate.ToString(FileLookupDate), x.VirtualImagePath))
            .ToHashSet();
        lock (s_imageFindingLock) s_imagesToProcess.Clear();
        foreach (var folder in configuration.VirtualImageFolders().OrderBy(f => f))
        {
            foreach (var file in Directory.EnumerateFiles(folder).OrderBy(f => f).Select(f => new FileInfo(f)))
            {
                var creationDateText = file.CreationTimeUtc.ToString(FileLookupDate);
                if ((DateTime.UtcNow - file.CreationTimeUtc).TotalMinutes < 1) continue;
                if (existingResults.Contains((creationDateText, file.FullName))) continue;
                s_imagesToProcess.TryAdd(file.FullName, file.CreationTimeUtc);
            }
        }
        logger.LogInformation("Finished searching for virtual images. Found {images} virtual images to process", s_imagesToProcess.Count);
    }

    public void FindNextImageToProcess()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        KeyValuePair<string, DateTime> nextImage;
        lock (s_processingLock)
        {
            if (s_isProcessing) return;
            lock (s_imageFindingLock)
            {
                if (s_imagesToProcess.IsEmpty) return;
                nextImage = s_imagesToProcess.OrderByDescending(itp => itp.Value).FirstOrDefault();
                s_isProcessing = true;
            }
        }
        Action action = () =>
        {
            logger.LogInformation("Processing virtual image {image}", nextImage.Key);
            var templatedTrips = context.PhotoTourTrips
                .Where(ptt => ptt.SegmentationTemplateJson != null)
                .OrderBy(ptt => ptt.VirtualPicturePath)
                .ToList();
            var tripTime = PhotoTourTrip.DateFromVirtualImage(nextImage.Key);
            var segmentationTemplate = templatedTrips
                .LastOrDefault(tt => tt.Timestamp <= tripTime)?
                .SegmentationTemplateJson ?? SegmentationTemplate.GetDefault();
            logger.LogInformation("Using template {image}", segmentationTemplate.AsJson());
            var pixelSummary = ProcessImage(nextImage.Key, segmentationTemplate);
            logger.LogInformation("Calculating Image Descriptors");
            var imageResults = pixelSummary.GetResults();
            RemoveExistingSummary(logger, context, nextImage);
            context.VirtualImageSummaries.Add(new VirtualImageSummary()
            {
                VirtualImageCreationDate = nextImage.Value,
                ImageDescriptors = new PhotoTourDescriptor()
                {
                    PlantDescriptors = imageResults.ConvertAll(ir => ir.GetDataModel()),
                    TripStart = pixelSummary.GetPhotoTripData.TripStart,
                    PhotoTourId = pixelSummary.GetPhotoTripData.PhotoTourId,
                    PhotoTripId = pixelSummary.GetPhotoTripData.PhotoTripId,
                    TourName = pixelSummary.GetPhotoTripData.TourName,
                    TripEnd = pixelSummary.GetPhotoTripData.TripEnd,
                    DeviceTemperatures = pixelSummary.DeviceTemperatures
                    .Where(dt => dt.AverageTemperature > 0)
                    .Select(dt => new DeviceTemperature()
                    {
                        AverageTemperature = dt.AverageTemperature,
                        CountOfMeasurements = dt.CountOfMeasurements,
                        MaxTemperature = dt.MaxTemperature,
                        MedianTemperature = dt.MedianTemperature,
                        MinTemperature = dt.MinTemperature,
                        Name = dt.Name,
                        TemperatureDeviation = dt.TemperatureDeviation
                    })
                },
                VirtualImagePath = nextImage.Key
            });
            context.SaveChanges();
            logger.LogInformation("Summary for {image} was added successfully", nextImage.Key);
        };
        action.Try(ex =>
        {
            RemoveExistingSummary(logger, context, nextImage);
            var newSummary = new VirtualImageSummary
            {
                VirtualImageCreationDate = nextImage.Value,
                VirtualImagePath = nextImage.Key,
                ImageDescriptors = new()
            };
            context.VirtualImageSummaries.Add(newSummary);
            context.SaveChanges();
            logger.LogInformation("Virtual image {zip} threw an error", nextImage.Key);
            ex.LogError();
        });
        lock (s_processingLock)
        {
            s_imagesToProcess.TryRemove(nextImage.Key, out _);
            s_isProcessing = false;
        }
    }

    private static void RemoveExistingSummary(ILogger<PhotoTourSummaryWorker> logger, IDataContext context, KeyValuePair<string, DateTime> nextImage)
    {
        var existingSummary = context.VirtualImageSummaries.FirstOrDefault(vim => vim.VirtualImagePath == nextImage.Key);
        if (existingSummary != default)
        {
            logger.LogInformation("Removing existing summary for {image}", nextImage.Key);
            context.VirtualImageSummaries.Remove(existingSummary);
            context.SaveChanges();
        }
    }

    public List<IManagedMat> SplitInSubImages(string image, HashSet<string> desiredPlants)
    {
        var (visMat, irMat, metaData) = GetDataFromZip(image);
        var plantIndexByName = metaData.ImageMetaData.OrderBy(im => im.ImageIndex)
            .ToDictionary(im => im.ImageName, im => im.ImageIndex);
        var width = metaData.Dimensions.Width;
        var height = metaData.Dimensions.Height;
        var result = new List<IManagedMat>();
        foreach (var plant in desiredPlants)
        {
            if (!plantIndexByName.TryGetValue(plant, out var index)) continue;
            var startX = width * (index % metaData.Dimensions.ImagesPerRow);
            var startY = height * (index / metaData.Dimensions.ImagesPerRow);
            var roi = new Rectangle(startX, startY, width, height);
            if (roi.Width <= 0 || roi.Height <= 0) continue;
            result.Add(visMat.Execute(x => new Mat(x, roi)).AsManaged());
        }
        visMat.Dispose();
        irMat.Dispose();
        return result;
    }

    public PhotoSummaryResult ProcessImage(string image, SegmentationTemplate segmentationTemplate)
    {
        var (visMat, rawIrMat, metaData) = GetDataFromZip(image);
        var mask = GetPlantMask(visMat, segmentationTemplate);
        var borderMask = SubImageBorderMask(visMat);
        var borderMaskData = borderMask.Execute(x => x.GetData(true));
        var maskData = mask.Execute(x => x.GetData(true));
        var irData = rawIrMat.Execute(x => x.GetData(true));
        var getImage = metaData.BuildCoordinateToImageFunction();
        var visData = visMat.Execute(x => x.GetData(true));
        var resultData = new PhotoSummaryResult(metaData.Dimensions.SizeOfPixelInMm);
        var deviceTemperatureInfo = metaData.TemperatureReadings
            .Where(tr => tr.TemperatureInC > 0f)
            .GroupBy(tr => tr.Comment + " " + tr.SensorId)
            .Select(g =>
            {
                var average = g.Average(tr => tr.TemperatureInC);
                return new PhotoSummaryResult.DeviceTemperatureInfo()
                {
                    Name = g.Key,
                    AverageTemperature = g.Average(tr => tr.TemperatureInC),
                    MaxTemperature = g.Max(tr => tr.TemperatureInC),
                    MedianTemperature = g.OrderBy(tr => tr.TemperatureInC).Median(tr => tr.TemperatureInC),
                    MinTemperature = g.Min(tr => tr.TemperatureInC),
                    TemperatureDeviation = g.Deviation(average, tr => tr.TemperatureInC),
                    CountOfMeasurements = g.Count()
                };
            }).ToList();
        var irTemperatures = metaData.ImageMetaData
            .DistinctBy(im => im.MotorPosition)
            .Select(im => im.IrTempInC)
            .Where(x => x > 0f)
            .ToList();
        deviceTemperatureInfo.PushIf(() =>
        {
            var irAverageTemperature = irTemperatures.Average();
            return new PhotoSummaryResult.DeviceTemperatureInfo()
            {
                Name = TemperatureMeasurement.FlirLeptonSensorId,
                AverageTemperature = irAverageTemperature,
                CountOfMeasurements = irTemperatures.Count,
                MaxTemperature = irTemperatures.Max(),
                MedianTemperature = irTemperatures.OrderBy(t => t).Median(t => t),
                MinTemperature = irTemperatures.Min(),
                TemperatureDeviation = irTemperatures.Deviation(irAverageTemperature, t => t),
            };
        }, () => irTemperatures.Count > 0);
        resultData.AddDeviceTemperatures(deviceTemperatureInfo);
        resultData.AddPhotoTripData(metaData.TimeInfos.TripName, metaData.TimeInfos.StartTime, metaData.TimeInfos.EndTime, metaData.TimeInfos.PhotoTourId, metaData.TimeInfos.PhotoTripId);
        logger.LogInformation("Read temperature values from Image");
        var rowCount = mask.Execute(x => x.Rows);
        var colCount = mask.Execute(x => x.Cols);
        for (var row = 0; row < rowCount; row++)
        {
            for (var col = 0; col < colCount; col++)
            {
                var value = (byte)maskData.GetValue(row, col)!;
                if (value == 0) continue;
                var leafOutOfRange = false;
                if ((byte?)borderMaskData.GetValue(row, col) == 255) leafOutOfRange = true;
                var imageData = getImage((col, row));
                if (imageData == null) continue;
                var temperatureInteger = (byte)irData.GetValue(row, col, 0)!;
                var temperatureFraction = (byte)irData.GetValue(row, col, 1)!;
                var rValue = (byte)visData.GetValue(row, col, 2)!;
                var gValue = (byte)visData.GetValue(row, col, 1)!;
                var bValue = (byte)visData.GetValue(row, col, 0)!;
                resultData.AddPixelInfo(imageData, col, row, temperatureInteger + (temperatureFraction / 100f), [rValue, gValue, bValue], leafOutOfRange);
            }
        }
        mask.Execute(x => x.Dispose());
        visMat.Execute(x => x.Dispose());
        rawIrMat.Execute(x => x.Dispose());
        borderMask.Execute(x => x.Dispose());
        logger.LogInformation("Finished reading photo summary results");
        return resultData;
    }

    public (IManagedMat VisImage, IManagedMat RawIrImage, VirtualImageMetaDataModel MetaData) GetDataFromZip(string image)
    {
        var tempFolder = Directory.CreateTempSubdirectory().FullName;
        var zip = new ZipArchive(File.OpenRead(image));
        var files = new HashSet<string>();
        foreach (var entry in zip.Entries)
        {
            var path = Path.Combine(tempFolder, entry.Name);
            File.WriteAllBytes(path, entry.Open().ConvertToArray());
            files.Add(path);
        }
        logger.LogInformation("Extracted {files} from {zip} to locations:\n{locations}", files.Count, image,
            files.Select(f => f + " exists: " + Path.Exists(f)).Concat("\n"));
        zip.Dispose();
        var visMat = CvInvoke.Imread(files.First(f => Path.GetFileName(f).StartsWith(PhotoTourTrip.VisPrefix)), ImreadModes.Color);
        var rawIrMat = CvInvoke.Imread(files.First(f => Path.GetFileName(f).StartsWith(PhotoTourTrip.RawIrPrefix)));
        var metaData = VirtualImageMetaDataModel.FromTsvFile(File.ReadAllText(files.First(f => Path.GetFileName(f).StartsWith(PhotoTourTrip.MetaDataPrefix))));
        logger.LogInformation("All images were read. ImageInfo vis {vis}, ir {ir}", visMat.Width + "x" + visMat.Height, rawIrMat.Width + "x" + rawIrMat.Height);
        return (visMat.AsManaged(), rawIrMat.AsManaged(), metaData);
    }

    public IManagedMat SubImageBorderMask(IManagedMat visMat)
    {
        logger.LogInformation("Create Border Mask");
        var mask = new Mat().AsManaged();
        visMat.Execute(mask, (x, y) => x.CopyTo(y));
        mask.Execute(x => CvInvoke.CvtColor(x, x, ColorConversion.Rgb2Gray));
        var whiteMask = new Mat().AsManaged();
        mask.Execute(whiteMask, (x, y) => CvInvoke.InRange(x, new ScalarArray(new MCvScalar(255)), new ScalarArray(new MCvScalar(255)), y));
        mask.Execute(whiteMask, (x, y) => x.SetTo(new MCvScalar(0), y));
        mask.Execute(x => CvInvoke.Threshold(x, x, 0d, 255d, ThresholdType.Binary));
        mask.Execute(x => CvInvoke.Canny(x, x, 100, 300));
        whiteMask.Execute(x => x.Dispose());
        logger.LogInformation("Finished Border Mask Creation");
        return mask;
    }

    public IManagedMat GetPlantMask(IManagedMat visMat, SegmentationTemplate parameter)
    {
        logger.LogInformation("Get Plant Mask");
        var hsvMat = new Mat().AsManaged();
        var mask = new Mat().AsManaged();
        visMat.Execute(hsvMat, (x, y) => CvInvoke.CvtColor(x, y, ColorConversion.Bgr2Hsv));
        logger.LogInformation("Segment HSV Color");
        SegmentHsvColorSpace(hsvMat, mask, parameter);
        logger.LogInformation("Apply first Opening with {parameter}", parameter.AsJson());
        MorphologicalOpening(mask, parameter);
        if (parameter.UseOtsu)
        {
            logger.LogInformation("Apply Otsu Thresholding");
            OtsuTresholdingOnSaturationChannel(hsvMat, mask);
        }
        logger.LogInformation("Apply second Opening with {parameter}", parameter.AsJson());
        MorphologicalOpening(mask, parameter);
        hsvMat.Execute(x => x.Dispose());
        logger.LogInformation("Plant Mask creation finished");
        return mask;
    }

    private static void OtsuTresholdingOnSaturationChannel(IManagedMat hsvMat, IManagedMat mask)
    {
        var colorMaskedImage = new Mat().AsManaged();
        hsvMat.Execute(colorMaskedImage, mask, (x, y, z) => CvInvoke.BitwiseAnd(x, x, y, z));
        var hsvChannels = colorMaskedImage.Execute(x => x.Split().Select(m => m.AsManaged())).ToArray();
        hsvChannels[1].Execute(mask, (x, y) => CvInvoke.Threshold(x, y, 65d, 255d, ThresholdType.Otsu));
        colorMaskedImage.Execute(x => x.Dispose());
        foreach (var channel in hsvChannels) channel.Execute(x => x.Dispose());
    }

    private static void MorphologicalOpening(IManagedMat mask, SegmentationTemplate parameter)
    {
        var element = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1)).AsManaged();
        mask.Execute(element, (x, y) => CvInvoke.Erode(x, x, y, anchor: new Point(-1, -1), parameter.OpeningIterations, BorderType.Constant, new MCvScalar(0)));
        mask.Execute(element, (x, y) => CvInvoke.Dilate(x, x, y, anchor: new Point(-1, -1), parameter.OpeningIterations, BorderType.Constant, new MCvScalar(0)));
        element.Execute(x => x.Dispose());
    }

    private static void SegmentHsvColorSpace(IManagedMat hsvMat, IManagedMat mask, SegmentationTemplate parameter)
    {
        var lowGreen = new ScalarArray(new MCvScalar(parameter.HLow / 360d * 255d, parameter.SLow / 100d * 255d, parameter.LLow / 100d * 255d));
        var highGreen = new ScalarArray(new MCvScalar(parameter.HHigh / 360d * 255d, parameter.SHigh / 100d * 255d, parameter.LHigh / 100d * 255d));
        hsvMat.Execute(mask, (x, y) => CvInvoke.InRange(x, lowGreen, highGreen, y));
        lowGreen.Dispose();
        highGreen.Dispose();
    }
}
