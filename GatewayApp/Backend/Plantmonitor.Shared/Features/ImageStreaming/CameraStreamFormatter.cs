using System.Globalization;
using Plantmonitor.Shared.Extensions;

namespace Plantmonitor.Shared.Features.ImageStreaming;

public record struct CameraStreamData(DateTime Timestamp, int Steps, int TemperatureInK, byte[]? PictureData);

public class CameraStreamFormatter
{
    public const string PictureDateFormat = "yyyy-MM-dd HH-mm-ss-fff";

    public static CameraStreamFormatter FromBytes(byte[] bytes)
    {
        return new CameraStreamFormatter()
        {
            Steps = bytes.Length >= 4 ? BitConverter.ToInt32(bytes.AsSpan()[0..4]) : default,
            Timestamp = bytes.Length >= 12 ? new DateTime(BitConverter.ToInt64(bytes.AsSpan()[4..12])) : default,
            TemperatureInK = bytes.Length >= 16 ? BitConverter.ToInt32(bytes.AsSpan()[12..16]) : default,
            PictureData = bytes.Length > 16 ? bytes[16..] : null,
        };
    }

    public CameraStreamData ConvertToStreamObject() => new(Timestamp, Steps, TemperatureInK, PictureData);

    public int Steps { get; set; }
    public DateTime Timestamp { get; set; }
    public int TemperatureInK { get; set; }
    public byte[]? PictureData { get; set; }

    public void WriteToFile(string basePath, CameraTypeInfo cameraInfo)
    {
        File.WriteAllBytes(Path.Combine(basePath, $"{Timestamp.ToUniversalTime().ToString(PictureDateFormat)}_{Steps}_{TemperatureInK}{cameraInfo.FileEnding}"), PictureData ?? []);
    }

    private static readonly HashSet<string> s_validFiles = Enum.GetValues<CameraType>().Cast<CameraType>().Select(c => c.Attribute<CameraTypeInfo>().FileEnding).ToHashSet();

    public static bool FromFile(string path, out CameraStreamFormatter result)
    {
        result = new();
        var extension = Path.GetExtension(path);
        if (!s_validFiles.Contains(extension)) return false;
        var split = Path.GetFileNameWithoutExtension(path).Split('_');
        if (split.Length < 2) return false;
        if (!DateTime.TryParseExact(split[0], PictureDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return false;
        result.Timestamp = date;
        if (!int.TryParse(split[1], out var steps)) return false;
        result.Steps = steps;
        result.PictureData = File.ReadAllBytes(path);
        if (split.Length == 2 || !int.TryParse(split[2], out var temperature)) return true;
        result.TemperatureInK = temperature;
        return true;
    }

    public CameraStreamFormatter TemperatureFromFileName(string fileName)
    {
        var temperatureText = Path.GetFileNameWithoutExtension(fileName).Split('_');
        if (temperatureText.Length > 1 && int.TryParse(temperatureText[1], out var temperature)) TemperatureInK = temperature;
        return this;
    }

    public byte[] GetBytes()
    {
        return [.. BitConverter.GetBytes(Steps), .. BitConverter.GetBytes(Timestamp.Ticks), .. BitConverter.GetBytes(TemperatureInK), .. PictureData];
    }
}
