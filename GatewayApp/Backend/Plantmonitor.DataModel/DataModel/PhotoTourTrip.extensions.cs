using System.Globalization;

namespace Plantmonitor.DataModel.DataModel;
public record class SegmentationTemplate(string Name, double HLow, double HHigh, double SLow, double SHigh,
    double LLow, double LHigh, bool UseOtsu, int OpeningIterations)
{
    public static SegmentationTemplate GetDefault() => new("Default", 40d, 130d, 5d, 100d, 20d, 100d, true, 2);
}
public record struct VirtualImageFileInfo(string? Path, DateTime Timestamp, long PhotoTourId, string? Name);

public partial class PhotoTourTrip
{
    public const string VirtualImageFolderPrefix = "VirtualImages_";
    public const string CustomTourDataPrefix = "CustomTourData_";
    public const string ImageFolderPrefix = "Images_";
    public const string IrPrefix = "ir_";
    public const string VisPrefix = "vis_";
    public const string RawIrPrefix = "rawIr_";
    public const string MetaDataPrefix = "data_";
    public const string TimestampFormat = "yyyyMMdd_HHmmss_fff";
    public const string ZipFilePrefix = "trip_";
    public SegmentationTemplate? SegmentationTemplateJson { get; set; }

    public string VirtualImageFileName(string folder)
    {
        return $"{folder}/trip_{Timestamp.ToString(TimestampFormat)}.zip";
    }

    public static VirtualImageFileInfo DataFromVirtualImage(string? path)
    {
        if (path == null) return new(path, DateTime.MinValue, default, default);
        var folderName = Directory.GetParent(path)?.Name ?? "";
        long id = 0;
        var name = "";
        if (folderName.StartsWith(VirtualImageFolderPrefix))
        {
            var split = folderName.Split('_');
            id = long.TryParse(split[1], out var parsedId) ? parsedId : 0;
            name = split[2];
        }
        var fileName = Path.GetFileNameWithoutExtension(path);
        var date = DateTime.TryParseExact(fileName.Substring(ZipFilePrefix.Length, TimestampFormat.Length),
            TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result) ? result : DateTime.MinValue;
        return new(path, date, id, name);
    }
}
