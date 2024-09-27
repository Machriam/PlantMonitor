using System.Globalization;
using System.Text;

namespace Plantmonitor.ImageWorker;

public static class ITsvFormattableExtensions
{
    public static string GetTsv(this ITsvFormattable model, bool withHeader, string tableName)
    {
        var result = new StringBuilder();
        if (withHeader)
        {
            result.AppendLine(tableName.Where(char.IsLetterOrDigit).Concat(""));
            result.AppendLine(model.GetType().GetProperties().Select(p => p.Name).Concat("\t"));
        }
        foreach (var property in model.GetType().GetProperties()) result.Append(model.FormatValue(property.Name, property.GetValue(model)) + "\t");
        return result.ToString();
    }
}

public interface ITsvFormattable
{
    string FormatValue(string name, object? value);

    object ParseFromText(string key, string? text);
}

public record struct VirtualImageMetaDataModel()
{
    private static readonly string s_minDateString = DateTime.MinValue.ToString(DateFormat, CultureInfo.InvariantCulture);
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss:fff";
    public class ImageDimensions : ITsvFormattable
    {
        public ImageDimensions() { }
        public ImageDimensions(int width, int height, int imageWidth, int imageHeight, int leftPadding,
            int topPadding, int imagesPerRow, int rowCount, int imageCount, string comment, float sizeOfPixelInMm)
        {
            Width = width;
            Height = height;
            ImageWidth = imageWidth;
            ImageHeight = imageHeight;
            LeftPadding = leftPadding;
            TopPadding = topPadding;
            ImagesPerRow = imagesPerRow;
            RowCount = rowCount;
            ImageCount = imageCount;
            Comment = comment;
            SizeOfPixelInMm = sizeOfPixelInMm;
        }

        public int Width { get; set; }
        public int Height { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public float SizeOfPixelInMm { get; set; }
        public int LeftPadding { get; set; }
        public int TopPadding { get; set; }
        public int ImagesPerRow { get; set; }
        public int RowCount { get; set; }
        public int ImageCount { get; set; }
        public string Comment { get; set; } = "";

        public string FormatValue(string name, object? value) => name switch
        {
            nameof(SizeOfPixelInMm) => ((float?)value ?? 0f).ToString("0.00", CultureInfo.InvariantCulture),
            _ => value?.ToString() ?? ""
        };
        public object ParseFromText(string key, string? text) => key switch
        {
            nameof(Comment) => text ?? "",
            nameof(SizeOfPixelInMm) => float.TryParse(text ?? "", CultureInfo.InvariantCulture, out var size) ? size : 0.2f,
            _ => int.TryParse(text, CultureInfo.InvariantCulture, out var number) ? number : 0,
        };
    }

    public class ImageMetaDatum : ITsvFormattable
    {
        public ImageMetaDatum() { }
        public ImageMetaDatum(int imageIndex, string imageName, string imageComment, bool hasIr, bool hasVis, DateTime irTime,
            DateTime visTime, int irTempInK, int motorPosition)
        {
            ImageIndex = imageIndex;
            ImageName = imageName;
            ImageComment = imageComment;
            HasIr = hasIr;
            HasVis = hasVis;
            IrTime = irTime;
            VisTime = visTime;
            IrTempInC = irTempInK.KelvinToCelsius();
            MotorPosition = motorPosition;
        }

        public int ImageIndex { get; set; }
        public string ImageName { get; set; } = "";
        public string ImageComment { get; set; } = "";
        public int MotorPosition { get; set; }
        public bool HasIr { get; set; }
        public bool HasVis { get; set; }
        public DateTime IrTime { get; set; }
        public DateTime VisTime { get; set; }
        public float IrTempInC { get; set; }

        public string FormatValue(string name, object? value)
        {
            if (value == null) return "";
            return name switch
            {
                nameof(IrTime) or nameof(VisTime) => ((DateTime)value).ToString(DateFormat),
                nameof(IrTempInC) => ((float)value).ToString("0.00 °C", CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? "",
            };
        }

        public object ParseFromText(string key, string? text) => key switch
        {
            nameof(ImageName) or nameof(ImageComment) => text ?? "",
            nameof(HasIr) or nameof(HasVis) => bool.Parse(text ?? "False"),
            nameof(IrTempInC) => float.Parse(text?.Replace("°C", "") ?? "0", CultureInfo.InvariantCulture),
            nameof(IrTime) or nameof(VisTime) => DateTime.ParseExact(text ?? s_minDateString, DateFormat, CultureInfo.InvariantCulture),
            nameof(ImageIndex) or nameof(MotorPosition) => int.Parse(text ?? "0"),
            _ => text ?? "",
        };
    }

    public class TimeInfo : ITsvFormattable
    {
        public TimeInfo() { }
        public TimeInfo(DateTime startTime, DateTime endTime, string tripName, long photoTourId, long photoTripId)
        {
            StartTime = startTime;
            EndTime = endTime;
            TripName = tripName;
            PhotoTripId = photoTripId;
            PhotoTourId = photoTourId;
        }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string TripName { get; set; } = "";
        public long PhotoTripId { get; set; }
        public long PhotoTourId { get; set; }

        public string FormatValue(string name, object? value)
        {
            if (value == null) return "";
            return name switch
            {
                nameof(StartTime) or nameof(EndTime) => ((DateTime)value).ToString(DateFormat),
                _ => value?.ToString() ?? "",
            };
        }

        public object ParseFromText(string key, string? text) => key switch
        {
            nameof(StartTime) or nameof(EndTime) => DateTime.ParseExact(text ?? s_minDateString, DateFormat, CultureInfo.InvariantCulture),
            nameof(PhotoTripId) or nameof(PhotoTourId) => long.Parse(text ?? "0"),
            _ => text ?? "",
        };
    }

    public class TemperatureReading : ITsvFormattable
    {
        public TemperatureReading() { }
        public TemperatureReading(string sensorId, string comment, float temperatureInC, DateTime time)
        {
            SensorId = sensorId;
            Comment = comment;
            TemperatureInC = temperatureInC;
            Time = time;
        }

        public string SensorId { get; set; } = "";
        public string Comment { get; set; } = "";
        public float TemperatureInC { get; set; }
        public DateTime Time { get; set; }

        public string FormatValue(string name, object? value)
        {
            if (value == null) return "";
            return name switch
            {
                nameof(TemperatureInC) => TemperatureInC.ToString("0.00 °C", CultureInfo.InvariantCulture),
                nameof(Time) => ((DateTime)value).ToString(DateFormat),
                _ => value?.ToString() ?? "",
            };
        }

        public object ParseFromText(string key, string? text) => key switch
        {
            nameof(TemperatureInC) => float.Parse(text?.Replace("°C", "") ?? "0", CultureInfo.InvariantCulture),
            nameof(Time) => DateTime.ParseExact(text ?? s_minDateString, DateFormat, CultureInfo.InvariantCulture),
            _ => text ?? "",
        };
    }

    public ImageDimensions Dimensions { get; set; } = new();
    public ICollection<ImageMetaDatum> ImageMetaData { get; set; } = [];
    public TimeInfo TimeInfos { get; set; } = new();
    public ICollection<TemperatureReading> TemperatureReadings { get; set; } = [];

    public static VirtualImageMetaDataModel FromTsvFile(string tsv)
    {
        var nextTable = true;
        var tableDataByTableName = new Dictionary<string, Dictionary<string, List<string>>>()
        {
            { nameof(Dimensions),new() },
            { nameof(ImageMetaData),new() },
            { nameof(TimeInfos),new() },
            { nameof(TemperatureReadings),new()},
        };
        var currentTable = tableDataByTableName.First().Value;
        var headers = new List<string>();
        foreach (var line in tsv.Split('\n', StringSplitOptions.None))
        {
            if (line.IsEmpty())
            {
                nextTable = true;
                continue;
            }
            if (nextTable && tableDataByTableName.TryGetValue(line, out var tableData))
            {
                currentTable = tableData;
                nextTable = false;
                headers = [];
                continue;
            }
            if (headers.Count == 0)
            {
                headers = line.Split('\t').Select(w => w.Trim()).ToList();
                foreach (var header in headers) currentTable.Add(header, []);
                continue;
            }
            var dataColumns = line.Split('\t');
            for (var i = 0; i < headers.Count && i < dataColumns.Length; i++)
            {
                currentTable[headers[i]].Add(dataColumns[i]);
            }
        }
        return new VirtualImageMetaDataModel()
        {
            Dimensions = FromTsvRowData<ImageDimensions>(tableDataByTableName[nameof(Dimensions)]).FirstOrDefault() ?? new(),
            ImageMetaData = [.. FromTsvRowData<ImageMetaDatum>(tableDataByTableName[nameof(ImageMetaData)])],
            TemperatureReadings = [.. FromTsvRowData<TemperatureReading>(tableDataByTableName[nameof(TemperatureReadings)])],
            TimeInfos = FromTsvRowData<TimeInfo>(tableDataByTableName[nameof(TimeInfos)]).FirstOrDefault() ?? new(),
        };
    }

    private static List<T> FromTsvRowData<T>(Dictionary<string, List<string>> table) where T : ITsvFormattable, new()
    {
        var result = new List<T>();
        var sortedEntries = table
            .SelectMany(t => t.Value.WithIndex().Select(v => (t.Key, v.Item, v.Index)))
            .OrderBy(r => r.Index)
            .ToLookup(r => r.Index);
        var propertyNames = typeof(T).GetProperties().ToList();
        foreach (var entry in sortedEntries)
        {
            var newEntry = new T();
            propertyNames.ForEach(p => p.SetValue(newEntry, newEntry.ParseFromText(p.Name, entry.FirstOrDefault(e => e.Key == p.Name).Item)));
            result.Add(newEntry);
        }
        return result;
    }

    public readonly Func<(int Left, int Top), ImageMetaDatum?> BuildCoordinateToImageFunction()
    {
        var imageList = ImageMetaData.OrderBy(imd => imd.ImageIndex).ToList();
        var index = 0;
        var result = new Dictionary<(int Row, int Col), ImageMetaDatum>();
        for (var ri = 0; ri < float.Ceiling(Dimensions.ImageCount / (float)Dimensions.ImagesPerRow); ri++)
        {
            for (var ci = 0; ci < Dimensions.ImagesPerRow; ci++)
            {
                if (index >= imageList.Count) break;
                result.Add((ri, ci), imageList[index]);
                index++;
            }
        }
        var inverseWidth = 1f / Dimensions.Width;
        var inverseHeight = 1f / Dimensions.Height;
        return (pixel) =>
        {
            var column = (int)(pixel.Left * inverseWidth);
            var row = (int)(pixel.Top * inverseHeight);
            if (result.TryGetValue((row, column), out var datum)) return datum;
            return null;
        };
    }

    public readonly string ExportAsTsv()
    {
        var thisObject = this;
        var data = typeof(VirtualImageMetaDataModel).GetProperties()
            .Where(p => p.GetMethod != null)
            .OrderBy(p => p.Name)
            .Select(p => (p.Name, Data: p.GetMethod!.Invoke(thisObject, null)!)).ToList();
        var result = new StringBuilder();
        foreach (var item in data)
        {
            var typeInfo = item.Data.GetType();
            if (typeInfo.IsAssignableTo(typeof(System.Collections.IEnumerable)))
            {
                var exportHeader = true;
                foreach (var listModel in (System.Collections.IEnumerable)item.Data)
                {
                    result.AppendLine((listModel as ITsvFormattable)!.GetTsv(exportHeader, item.Name));
                    exportHeader = false;
                }
            }
            if (typeInfo.IsAssignableTo(typeof(ITsvFormattable)))
            {
                result.AppendLine((item.Data as ITsvFormattable)!.GetTsv(true, item.Name));
            }
            result.AppendLine();
        }
        result.Replace("\r\n", "\n");
        return result.ToString();
    }
}
