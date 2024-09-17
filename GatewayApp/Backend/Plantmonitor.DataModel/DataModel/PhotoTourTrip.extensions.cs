using System;
using System.Collections.Generic;
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;

namespace Plantmonitor.DataModel.DataModel;
public record class SegmentationTemplate(double HLow, double HHigh, double SLow, double SHigh, double LLow, double LHigh, bool UseOtsu, int OpeningIterations)
{
    public static SegmentationTemplate GetDefault() => new(40d, 130d, 5d, 100d, 20d, 100d, true, 2);
}

public partial class PhotoTourTrip
{
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

    public static DateTime DateFromVirtualImage(string? path)
    {
        if (path == null) return DateTime.MinValue;
        var fileName = Path.GetFileNameWithoutExtension(path);
        return DateTime.TryParseExact(fileName.Substring(ZipFilePrefix.Length, TimestampFormat.Length),
            TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var result) ? result : DateTime.MinValue;
    }
}
