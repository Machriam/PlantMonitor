using System;
using System.Collections.Generic;
using System.Globalization;
using static System.Net.Mime.MediaTypeNames;

namespace Plantmonitor.DataModel.DataModel;

public partial class PhotoTourTrip
{
    public const string IrPrefix = "ir_";
    public const string VisPrefix = "vis_";
    public const string RawIrPrefix = "rawIr_";
    public const string MetaDataPrefix = "data_";
    public const string TimestampFormat = "yyyyMMdd_HHmmss_fff";
    public const string ZipFilePrefix = "trip_";

    public string VirtualImageFileName(string folder)
    {
        return $"{folder}/trip_{Timestamp.ToString(TimestampFormat)}.zip";
    }

    public static DateTime DateFromVirtualImage(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        return DateTime.TryParseExact(fileName.Substring(ZipFilePrefix.Length, TimestampFormat.Length),
            TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) ? result : DateTime.MinValue;
    }
}
