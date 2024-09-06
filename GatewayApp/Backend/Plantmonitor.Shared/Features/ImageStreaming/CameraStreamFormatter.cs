using System.Globalization;
using System.IO.Compression;
using Plantmonitor.Shared.Extensions;

namespace Plantmonitor.Shared.Features.ImageStreaming;

public record struct CompressionStatus(string Type, int ZippedImageCount, int TotalImages, int TemperatureInK)
{
    private static readonly Dictionary<string, CameraTypeInfo> s_cameraTypeInfoByName =
        Enum.GetValues<CameraType>()?.ToDictionary(ct => Enum.GetName(ct) ?? "", ct => ct.Attribute<CameraTypeInfo>()) ?? [];
    public (CompressionStatus Status, string Error) WriteFileToZip(ZipArchive archive, string[] files, string type, Func<DateTime, int> getStepCount)
    {
        var cameraInfo = s_cameraTypeInfoByName[type];
        string error = "";
        var file = files.FirstOrDefault();
        if (file == null) return (this, error);
        var creationDate = File.GetCreationTimeUtc(file);
        if (type == nameof(CameraType.IR)) TemperatureInK = file.TemperatureInKFromIrPath();
        var zipFileName = Path.GetFileName(new CameraStreamFormatter()
        {
            Steps = getStepCount(creationDate),
            Timestamp = creationDate,
            TemperatureInK = TemperatureInK,
        }.FormatFileInfo("", cameraInfo));
        Action addArchiveAction = () => archive.CreateEntryFromFile(file, zipFileName, CompressionLevel.Fastest);
        if (!addArchiveAction.Try(ex => error = $"Could not add to archive: {ex.Message}")) return (this, error);
        TotalImages = files.Length + ZippedImageCount;
        ZippedImageCount++;
        Action deleteAction = () => File.Delete(file);
        deleteAction.Try(ex => error = $"Could not delete: {ex.Message}");
        return (this, error);
    }
}
public record struct StoredDataStream(int CurrentStep, List<CompressionStatus> CompressionStatus, string ZipFileName, float DownloadStatus);
public record struct CameraStreamData(DateTime Timestamp, int Steps, int TemperatureInK, byte[]? PictureData);

public class CameraStreamFormatter
{
    public const string PictureDateFormat = "yyyy-MM-dd_HH-mm-ss-fff";
    public static byte[] FinishSignal { get; } = Enumerable.Repeat(byte.MaxValue, 3).ToArray();

    public static CameraStreamFormatter FromBytes(byte[] bytes)
    {
        return new CameraStreamFormatter()
        {
            Steps = bytes.Length >= 4 ? BitConverter.ToInt32(bytes.AsSpan()[0..4]) : default,
            Timestamp = bytes.Length >= 12 ? new DateTime(BitConverter.ToInt64(bytes.AsSpan()[4..12]), DateTimeKind.Utc) : default,
            TemperatureInK = bytes.Length >= 16 ? BitConverter.ToInt32(bytes.AsSpan()[12..16]) : default,
            PictureData = bytes.Length > 16 ? bytes[16..] : null,
            Finished = bytes.Length == FinishSignal.Length && bytes.All(b => b == byte.MaxValue)
        };
    }

    public CameraStreamData ConvertToStreamObject() => new(Timestamp, Steps, TemperatureInK, PictureData);

    public bool Finished { get; set; }
    public int Steps { get; set; }
    public DateTime Timestamp { get; set; }
    public int TemperatureInK { get; set; }
    public byte[]? PictureData { get; set; }
    public Func<byte[]?> FetchImageData { get; set; } = () => null;

    public static (string? FileName, CameraStreamFormatter? Formatter) FindInFolder(string path, int motorPosition)
    {
        return Directory.GetFiles(path)
            .Select(file => FromFileLazy(file, out var formatter) ? (File: file, Formatter: formatter) : (null, null))
            .FirstOrDefault(file => file.File != null && file.Formatter?.Steps == motorPosition);
    }

    public static bool FromFileLazy(string path, out CameraStreamFormatter result)
    {
        result = new();
        var extension = Path.GetExtension(path);
        if (!s_validFiles.Contains(extension)) return false;
        var fileName = Path.GetFileNameWithoutExtension(path);
        if (!DateTime.TryParseExact(fileName[0..PictureDateFormat.Length], PictureDateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)) return false;
        var split = Path.GetFileNameWithoutExtension(path)[PictureDateFormat.Length..].Split('_');
        if (split.Length < 2) return false;
        result.Timestamp = date;
        if (!int.TryParse(split[1], out var steps)) return false;
        result.Steps = steps;
        result.FetchImageData = () => File.ReadAllBytes(path);
        if (split.Length == 2 || !int.TryParse(split[2], out var temperature)) return true;
        result.TemperatureInK = temperature;
        return true;
    }

    public static bool FromFile(string path, out CameraStreamFormatter result)
    {
        result = new();
        var extension = Path.GetExtension(path);
        if (!s_validFiles.Contains(extension)) return false;
        var fileName = Path.GetFileNameWithoutExtension(path);
        if (!DateTime.TryParseExact(fileName[0..PictureDateFormat.Length], PictureDateFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)) return false;
        var split = Path.GetFileNameWithoutExtension(path)[PictureDateFormat.Length..].Split('_');
        if (split.Length < 2) return false;
        result.Timestamp = date;
        if (!int.TryParse(split[1], out var steps)) return false;
        result.Steps = steps;
        result.PictureData = File.ReadAllBytes(path);
        if (split.Length == 2 || !int.TryParse(split[2], out var temperature)) return true;
        result.TemperatureInK = temperature;
        return true;
    }

    public string FormatFileInfo(string basePath, CameraTypeInfo cameraInfo)
    {
        return Path.Combine(basePath, $"{Timestamp.ToUniversalTime().ToString(PictureDateFormat)}_{Steps}_{TemperatureInK}{cameraInfo.FileEnding}");
    }

    public void WriteToFile(string basePath, CameraTypeInfo cameraInfo)
    {
        File.WriteAllBytes(FormatFileInfo(basePath, cameraInfo), PictureData ?? []);
    }

    public byte[] GetBytes()
    {
        return [.. BitConverter.GetBytes(Steps), .. BitConverter.GetBytes(Timestamp.Ticks), .. BitConverter.GetBytes(TemperatureInK), .. PictureData];
    }

    private static readonly HashSet<string> s_validFiles = Enum.GetValues<CameraType>().Cast<CameraType>().Select(c => c.Attribute<CameraTypeInfo>().FileEnding).ToHashSet();
}
