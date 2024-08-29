using System.Collections.Concurrent;
using System.Drawing;
using System.IO.Compression;
using Emgu.CV;
using Emgu.CV.Structure;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.AutomaticPhotoTour;

namespace Plantmonitor.Server.Features.Dashboard;

public class PhotoTourSummaryWorker(IEnvironmentConfiguration configuration, IServiceScopeFactory scopeFactory) : IHostedService
{
    private Timer? _processImageTimer;
    private Timer? _processFindImagesToProcessTimer;
    private static readonly ConcurrentDictionary<string, DateTime> s_imagesToProcess = new();
    private static readonly object s_lock = new();
    private static bool s_isProcessing;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _processImageTimer = new Timer(_ => FindNextImageToProcess(), default, (int)TimeSpan.FromSeconds(10).TotalMilliseconds, (int)TimeSpan.FromSeconds(5).TotalMilliseconds);
        _processFindImagesToProcessTimer = new Timer(_ => FindImagesToProcess(), default,
            (int)TimeSpan.FromSeconds(20).TotalMilliseconds, (int)TimeSpan.FromMinutes(5).TotalMilliseconds);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processImageTimer == null || _processFindImagesToProcessTimer == null) return;
        await _processImageTimer.DisposeAsync();
        await _processFindImagesToProcessTimer.DisposeAsync();
    }

    public void FindImagesToProcess()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        var existingResults = context.VirtualImageSummaries
            .Select(vis => new { vis.VirtualImageCreationDate, vis.VirtualImagePath })
            .ToList()
            .Select(x => (x.VirtualImageCreationDate, x.VirtualImagePath))
            .ToHashSet();
        foreach (var folder in configuration.VirtualImageFolders().OrderBy(f => f))
        {
            foreach (var file in Directory.EnumerateFiles(folder).OrderBy(f => f).Select(f => new FileInfo(f)))
            {
                if (existingResults.Contains((file.CreationTime, file.FullName))) continue;
                s_imagesToProcess.TryAdd(file.FullName, file.CreationTimeUtc);
            }
        }
    }

    public void FindNextImageToProcess()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDataContext>();
        KeyValuePair<string, DateTime> nextImage;
        lock (s_lock)
        {
            if (s_isProcessing) return;
            if (s_imagesToProcess.IsEmpty) return;
            nextImage = s_imagesToProcess.First();
            s_isProcessing = true;
        }
        Action action = () =>
        {
            var pixelSummary = ProcessImage(nextImage.Key);
            var imageResults = pixelSummary.GetResults();
            context.VirtualImageSummaries.Add(new VirtualImageSummary()
            {
                VirtualImageCreationDate = nextImage.Value,
                ImageDescriptors = new PhotoTourDescriptor()
                {
                    PlantDescriptors = imageResults.ConvertAll(ir => ir.GetDataModel()),
                    TripStart = pixelSummary.GetPhotoTripData.TripStart,
                    TourName = pixelSummary.GetPhotoTripData.TourName,
                    TripEnd = pixelSummary.GetPhotoTripData.TripEnd,
                    DeviceTemperatures = pixelSummary.DeviceTemperatures.Select(dt => new DeviceTemperature()
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
        };
        action.Try(ex =>
        {
            context.VirtualImageSummaries.Add(new VirtualImageSummary
            {
                VirtualImageCreationDate = nextImage.Value,
                VirtualImagePath = nextImage.Key,
                ImageDescriptors = new()
            });
            context.SaveChanges();
            ex.LogError();
        });
        lock (s_lock)
        {
            s_imagesToProcess.TryRemove(nextImage.Key, out _);
            s_isProcessing = false;
        }
    }

    public PhotoSummaryResult ProcessImage(string image)
    {
        var (visMat, rawIrMat, metaData) = GetDataFromZip(image);
        var mask = GetPlantMask(visMat);
        var maskData = mask.GetData(true);
        var irData = rawIrMat.GetData(true);
        var getImage = metaData.BuildCoordinateToImageFunction();
        var visData = visMat.GetData(true);
        var resultData = new PhotoSummaryResult(metaData.Dimensions.SizeOfPixelInMm);
        var deviceTemperatureInfo = metaData.TemperatureReadings
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
            });
        resultData.AddDeviceTemperatures(deviceTemperatureInfo);
        resultData.AddPhotoTripData("", metaData.TimeInfos.StartTime, metaData.TimeInfos.EndTime);
        for (var row = 0; row < mask.Rows; row++)
        {
            for (var col = 0; col < mask.Cols; col++)
            {
                var value = (byte)maskData.GetValue(row, col)!;
                if (value == 0) continue;
                var leafOutOfRange = false;
                if (row == 0 || col == 0 || row == mask.Rows - 1 || col == mask.Cols - 1) leafOutOfRange = true;
                var imageData = getImage((col, row));
                var temperatureInteger = (byte)irData.GetValue(row, col, 0)!;
                var temperatureFraction = (byte)irData.GetValue(row, col, 1)!;
                var rValue = (byte)visData.GetValue(row, col, 0)!;
                var gValue = (byte)visData.GetValue(row, col, 1)!;
                var bValue = (byte)visData.GetValue(row, col, 2)!;
                resultData.AddPixelInfo(imageData, col, row, temperatureInteger + (temperatureFraction / 100f), [rValue, gValue, bValue], leafOutOfRange);
            }
        }
        mask.Dispose();
        visMat.Dispose();
        rawIrMat.Dispose();
        return resultData;
    }

    public (Mat VisImage, Mat RawIrImage, VirtualImageMetaDataModel MetaData) GetDataFromZip(string image)
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
        zip.Dispose();
        var visMat = CvInvoke.Imread(files.First(f => Path.GetFileName(f).StartsWith(PhotoTourTrip.VisPrefix)));
        var rawIrMat = CvInvoke.Imread(files.First(f => Path.GetFileName(f).StartsWith(PhotoTourTrip.RawIrPrefix)));
        var metaData = VirtualImageMetaDataModel.FromTsvFile(File.ReadAllText(files.First(f => Path.GetFileName(f).StartsWith(PhotoTourTrip.MetaDataPrefix))));
        return (visMat, rawIrMat, metaData);
    }

    public Mat GetPlantMask(Mat visMat)
    {
        var hsvMat = new Mat();
        CvInvoke.CvtColor(visMat, hsvMat, Emgu.CV.CvEnum.ColorConversion.Rgb2Hsv);
        var lowGreen = new ScalarArray(new MCvScalar(50, 50, 50));
        var highGreen = new ScalarArray(new MCvScalar(110, 255, 255));
        var mask = new Mat();
        var element = CvInvoke.GetStructuringElement(Emgu.CV.CvEnum.ElementShape.Rectangle, new Size(3, 3), new Point(-1, -1));
        CvInvoke.InRange(hsvMat, lowGreen, highGreen, mask);
        CvInvoke.Erode(mask, mask, element, anchor: new Point(-1, -1), 2, Emgu.CV.CvEnum.BorderType.Constant, new MCvScalar(0));
        CvInvoke.Dilate(mask, mask, element, anchor: new Point(-1, -1), 2, Emgu.CV.CvEnum.BorderType.Constant, new MCvScalar(0));
        element.Dispose();
        hsvMat.Dispose();
        lowGreen.Dispose();
        highGreen.Dispose();
        return mask;
    }
}
