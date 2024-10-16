using System.Collections.Concurrent;
using System.IO.Compression;
using Emgu.CV;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.ImageWorker;
using Plantmonitor.Server.Features.AppConfiguration;

namespace Plantmonitor.Server.Features.Dashboard;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(IDataContext context, IEnvironmentConfiguration configuration, IWebHostEnvironment webHost, IPhotoTourSummaryWorker photoTourSummary,
    IPhotoStitcher stitcher)
{
    private const double InverseGigabyte = 1d / (1024d * 1024d * 1024d);
    private static readonly ConcurrentDictionary<string, DownloadInfo> s_fileReadyToDownload = new();
    public record struct DownloadInfo(long PhotoTourId, string Path, double CurrentSize, double SizeToDownloadInGb, bool ReadyToDownload);
    public record struct TemperatureSummaryData(string Device, IEnumerable<TemperatureDatum> Data);
    public record struct VirtualImageInfo(string Name, DateTime CreationDate);
    public record struct TemperatureDatum(DateTime Time, float Temperature, float Deviation);
    public record struct SegmentationParameter(DateTime TripTime, SegmentationTemplate Template);
    public record struct SubImageRequest(string FileName, SegmentationTemplate? Template, IEnumerable<string> PlantNames, long PhotoTourId, bool ShowSegmentation);

    [HttpGet("virtualimagelist")]
    public IEnumerable<VirtualImageInfo> VirtualImageList(long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        return Directory.EnumerateFiles(configuration.VirtualImagePath(photoTour.Name, photoTour.Id))
            .Select(f => new VirtualImageInfo(Path.GetFileName(f), PhotoTourTrip.DataFromVirtualImage(f).Timestamp));
    }

    [HttpGet("fullsummaryinformation")]
    public IEnumerable<VirtualImageSummary> SummaryForTour(long photoTourId)
    {
        return SummariesById(context, photoTourId)
            .ToList()
            .Where(s => s.ImageDescriptors.PlantDescriptors.Any() && !s.IsDark())
            .OrderBy(s => s.VirtualImageCreationDate);
    }

    [HttpPost("summaryexport")]
    public string CreatePhotoSummaryExport(long photoTourId)
    {
        var name = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId).Name;
        var resultJson = SummariesById(context, photoTourId)
            .ToList()
            .AsJson();
        var fileName = name.SanitizeFileName().Replace(" ", "") + ".json";
        var path = Path.GetTempPath() + fileName;
        File.WriteAllText(path, resultJson);
        using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(path, fileName);
        }
        var downloadFilePath = DownloadFolder() + fileName + ".zip";
        File.WriteAllBytes(downloadFilePath, zipStream.ToArray());
        async Task DeleteFile()
        {
            await Task.Delay(TimeSpan.FromMinutes(5));
            File.Delete(downloadFilePath);
            File.Delete(path);
        }
        DeleteFile().RunInBackground(ex => ex.LogError());
        return Path.Combine(IWebHostEnvironmentExtensions.DownloadFolder, Path.GetFileName(downloadFilePath));
    }

    [HttpPost("subimages")]
    public byte[] GetSubImages([FromBody] SubImageRequest request)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == request.PhotoTourId);
        var folder = configuration.VirtualImagePath(photoTour.Name, photoTour.Id);
        var zipFile = Path.Combine(folder, Path.GetFileName(request.FileName));
        var plantSet = request.PlantNames.ToHashSet();
        if (!Path.Exists(zipFile)) return [];
        var resultList = photoTourSummary.SplitInSubImages(zipFile, plantSet);
        var resultMat = stitcher.CreateCombinedImage(resultList.OrderByNumericString(p => p.Name).Select(p => p.Image).ToList());
        if (request.ShowSegmentation)
        {
            var mask = photoTourSummary.GetPlantMask(resultMat, request.Template ?? SegmentationTemplate.GetDefault());
            resultMat.Dispose();
            var maskBytes = mask.BytesFromMat();
            mask.Dispose();
            return maskBytes;
        }
        var resultBytes = resultMat.BytesFromMat();
        resultMat.Dispose();
        return resultBytes;
    }

    [HttpGet("temperaturedata")]
    public IEnumerable<TemperatureSummaryData> TemperatureSummary(long photoTourId)
    {
        return SummariesById(context, photoTourId).ToList()
            .SelectMany(vis => vis.ImageDescriptors.DeviceTemperatures.Select(dt => new { Temperature = dt, vis.ImageDescriptors.TripStart }))
            .GroupBy(dt => dt.Temperature.Name)
            .Select(g => new TemperatureSummaryData(g.Key,
                g.Select(dt => new TemperatureDatum(dt.TripStart, dt.Temperature.AverageTemperature, dt.Temperature.TemperatureDeviation))
                .OrderBy(dt => dt.Time)));
    }

    private static IQueryable<VirtualImageSummary> SummariesById(IDataContext context, long photoTourId)
    {
        var summaries = context.VirtualImageSummaryByPhotoTourIds
            .Where(vis => vis.PhotoTourId == photoTourId)
            .Select(vis => vis.Id)
            .ToHashSet();
        return context.VirtualImageSummaries
            .Where(vis => summaries.Contains(vis.Id));
    }

    [HttpGet("virtualimage")]
    public byte[]? VirtualImage(string name, long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        var folder = configuration.VirtualImagePath(photoTour.Name, photoTour.Id);
        var zipFile = Path.Combine(folder, Path.GetFileName(name));
        if (!Path.Exists(zipFile)) return null;
        using var zip = ZipFile.Open(zipFile, ZipArchiveMode.Read);
        var visPicture = zip.Entries.First(e => e.Name.Contains(PhotoTourTrip.VisPrefix));
        return visPicture.Open().ConvertToArray();
    }

    [HttpGet("plantmaskparameter")]
    public IEnumerable<SegmentationParameter> PlantMaskParameterFor(long? photoTourId)
    {
        var defaultEntry = new SegmentationParameter(DateTime.MinValue, SegmentationTemplate.GetDefault());
        if (photoTourId == null) return [defaultEntry];
        var photoTour = context.AutomaticPhotoTours
            .Include(apt => apt.PhotoTourTrips)
            .First(apt => apt.Id == photoTourId);
        return photoTour.PhotoTourTrips
            .Where(ptt => ptt.SegmentationTemplateJson != null)
            .OrderBy(ptt => ptt.Timestamp)
            .Select(ptt => new SegmentationParameter(ptt.Timestamp, ptt.SegmentationTemplateJson!))
            .ToList()
            .Push(defaultEntry) ?? [defaultEntry];
    }

    [HttpPost("recalculatesummaries")]
    public void RecalculateImageSummaries(long photoTourId)
    {
        photoTourSummary.RecalculateSummaries(photoTourId);
    }

    [HttpPost("storecustomsegmentation")]
    public void StoreCustomSegmentation([FromBody] SegmentationTemplate parameter, DateTime virtualImageTime, long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours
            .Include(apt => apt.PhotoTourTrips)
            .First(apt => apt.Id == photoTourId);
        if (photoTour.PhotoTourTrips.Any(ptt => ptt.SegmentationTemplateJson?.Name == parameter.Name && parameter.Name != SegmentationTemplate.GetDefault().Name))
            throw new Exception("Name already taken");
        var trip = photoTour.PhotoTourTrips
            .OrderBy(ptt => Math.Abs((ptt.Timestamp - virtualImageTime).TotalMilliseconds))
            .First();
        trip.SegmentationTemplateJson = parameter == SegmentationTemplate.GetDefault() ? null : parameter;
        context.SaveChanges();
    }

    [HttpPost("segmentedimage")]
    public byte[]? SegmentedImage(string name, long photoTourId, [FromBody] SegmentationTemplate? parameter = null)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        var folder = configuration.VirtualImagePath(photoTour.Name, photoTour.Id);
        var zipFile = Path.Combine(folder, Path.GetFileName(name));
        if (!Path.Exists(zipFile)) return null;
        using var zip = ZipFile.Open(zipFile, ZipArchiveMode.Read);
        var visPicture = zip.Entries.First(e => e.Name.Contains(PhotoTourTrip.VisPrefix));
        var tempPng = Path.Combine(Directory.CreateTempSubdirectory().FullName, "temp.png");
        File.WriteAllBytes(tempPng, visPicture.Open().ConvertToArray());
        var visMat = CvInvoke.Imread(tempPng).AsManaged();
        var mask = photoTourSummary.GetPlantMask(visMat, parameter ?? SegmentationTemplate.GetDefault());
        visMat.Dispose();
        var result = mask.BytesFromMat();
        mask.Dispose();
        return result;
    }

    [HttpGet("statusofdownloadtourdata")]
    public IEnumerable<DownloadInfo> StatusOfDownloadTourData()
    {
        return s_fileReadyToDownload.Select(f =>
        {
            var path = webHost.DownloadFolderPath() + Path.GetFileName(f.Value.Path);
            if (!File.Exists(path)) return default;
            if (f.Value.ReadyToDownload) return f.Value;
            var currentSize = new FileInfo(path).Length * InverseGigabyte;
            return new DownloadInfo(f.Value.PhotoTourId, f.Value.Path, currentSize, f.Value.SizeToDownloadInGb, f.Value.ReadyToDownload);
        }).Where(f => f != default);
    }

    private string DownloadFolder()
    {
        var folder = webHost.DownloadFolderPath();
        Directory.CreateDirectory(folder);
        return folder;
    }

    [HttpPost("deletetourdata")]
    public void DeleteTourData(long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        var folder = configuration.VirtualImagePath(photoTour.Name, photoTour.Id);
        var zipFile = Path.Combine(DownloadFolder(), photoTour.Name.SanitizeFileName() + ".zip");
        if (File.Exists(zipFile)) File.Delete(zipFile);
    }

    [HttpGet("requestdownloadtourdata")]
    public DownloadInfo RequestDownloadTourData(long photoTourId)
    {
        var photoTour = context.AutomaticPhotoTours.First(apt => apt.Id == photoTourId);
        var folder = configuration.VirtualImagePath(photoTour.Name, photoTour.Id);
        var zipFile = Path.Combine(DownloadFolder(), photoTour.Name.SanitizeFileName() + ".zip");
        var sizeToDownload = new DirectoryInfo(folder).GetFiles()
            .Aggregate((0L), (a, f) => a += f.Length) * InverseGigabyte;
        var downloadFile = Path.Combine(IWebHostEnvironmentExtensions.DownloadFolder, Path.GetFileName(zipFile));
        var info = new DownloadInfo(photoTourId, downloadFile, 0d, sizeToDownload, false);
        async Task CreateAndDeleteZip()
        {
            await Task.Yield();
            s_fileReadyToDownload.Remove(zipFile, out _);
            if (File.Exists(zipFile)) File.Delete(zipFile);
            s_fileReadyToDownload.AddOrUpdate(zipFile, info, (_1, _2) => info);
            ZipFile.CreateFromDirectory(folder, zipFile, CompressionLevel.Fastest, true);
            if (s_fileReadyToDownload.TryGetValue(zipFile, out var currentInfo))
            {
                var size = new FileInfo(zipFile).Length * InverseGigabyte;
                var finalInfo = new DownloadInfo(currentInfo.PhotoTourId, currentInfo.Path, size, currentInfo.SizeToDownloadInGb, true);
                s_fileReadyToDownload.TryUpdate(zipFile, finalInfo, info);
            }
            await Task.Delay(TimeSpan.FromHours(12));
            File.Delete(zipFile);
            s_fileReadyToDownload.Remove(zipFile, out _);
        }
        CreateAndDeleteZip().RunInBackground(ex => ex.LogError());
        return info;
    }
}
