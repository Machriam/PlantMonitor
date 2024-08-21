using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Plantmonitor.Server.Features.AutomaticPhotoTour;

public static class ITsvFormattableExtensions
{
    public static string GetTsvHeader(this ITsvFormattable model) => model.GetType().GetProperties().Select(p => p.Name).Concat("\t");

    public static string GetTsv(this ITsvFormattable model, bool withHeader, string tableName)
    {
        var result = new StringBuilder();
        if (withHeader)
        {
            result.AppendLine(tableName.Where(char.IsLetterOrDigit).Concat(""));
            result.AppendLine(GetTsvHeader(model));
        }
        foreach (var property in model.GetType().GetProperties()) result.Append(model.FormatValue(property.Name, property.GetValue(model)) + "\t");
        return result.ToString();
    }
}

public interface ITsvFormattable
{
    string FormatValue(string name, object? value);

    IEnumerable<object> FromTsvRowData(Dictionary<string, List<string>> table);
}

public record struct VirtualImageMetaDataModel()
{
    public class ImageDimensions
    {
        public ImageDimensions() { }
        public ImageDimensions(int width, int height, int imageWidth, int imageHeight, int leftPadding, int topPadding, int imagesPerRow, int rowCount, int imageCount, string comment)
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
        }

        public int Width { get; set; }
        public int Height { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public int LeftPadding { get; set; }
        public int TopPadding { get; set; }
        public int ImagesPerRow { get; set; }
        public int RowCount { get; set; }
        public int ImageCount { get; set; }
        public string Comment { get; set; } = "";

        public string FormatValue(string name, object? value) => value?.ToString() ?? "";
        private static object ParseFromText(string key, string text) => key switch
        {
            nameof(Comment) => text,
            _ => int.Parse(text),
        };

        public IEnumerable<object> FromTsvRowData(Dictionary<string, List<string>> table)
        {
            var result = new List<object>();
            var sortedEntries = table
                .SelectMany(t => t.Value.WithIndex().Select(v => (t.Key, v.Item, v.Index)))
                .OrderBy(r => r.Index)
                .ToLookup(r => r.Index);
            var propertyNames = typeof(ImageDimensions).GetProperties().ToList();
            foreach (var entry in sortedEntries)
            {
                var newEntry = new ImageDimensions();
                propertyNames.ForEach(p => p.SetValue(newEntry, ParseFromText(p.Name, entry.First(e => e.Key == p.Name).Item)));
                result.Add(newEntry);
            }
            return result;
        }
    }

    public record struct ImageMetaDatum(int ImageIndex, string ImageName, string ImageComment, bool HasIr, bool HasVis, DateTime IrTime, DateTime VisTime, float IrTempInC) : ITsvFormattable
    {
        public readonly string FormatValue(string name, object? value)
        {
            if (value == null) return "";
            return name switch
            {
                nameof(IrTime) or nameof(VisTime) => ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss"),
                nameof(IrTempInC) => ((float)value).ToString("0.00 °C", CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? "",
            };
        }

        public IEnumerable<object> FromTsvRowData(Dictionary<string, List<string>> table)
        {
            return [];
        }
    }

    public record struct TimeInfo(DateTime StartTime, DateTime EndTime) : ITsvFormattable
    {
        public readonly string FormatValue(string name, object? value)
        {
            if (value == null) return "";
            return name switch
            {
                nameof(StartTime) or nameof(EndTime) => ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss"),
                _ => value?.ToString() ?? "",
            };
        }

        public IEnumerable<object> FromTsvRowData(Dictionary<string, List<string>> table)
        {
            return [];
        }
    }

    public record struct TemperatureReading(string SensorId, string Comment, float TemperatureInC, DateTime Time) : ITsvFormattable
    {
        public readonly string FormatValue(string name, object? value)
        {
            if (value == null) return "";
            return name switch
            {
                nameof(TemperatureInC) => TemperatureInC.ToString("0.00 °C", CultureInfo.InvariantCulture),
                nameof(Time) => ((DateTime)value).ToString("yyyy-MM-dd HH:mm:ss"),
                _ => value?.ToString() ?? "",
            };
        }

        public IEnumerable<object> FromTsvRowData(Dictionary<string, List<string>> table)
        {
            return [];
        }
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
            { nameof(ImageDimensions),new() },
            { nameof(ImageMetaDatum),new() },
            { nameof(TimeInfo),new() },
            { nameof(TemperatureReading),new()},
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
            for (var i = 0; i < headers.Count; i++)
            {
                currentTable[headers[i]].Add(dataColumns[i]);
            }
        }
        return new VirtualImageMetaDataModel()
        {
            Dimensions = new ImageDimensions().FromTsvRowData(tableDataByTableName[nameof(ImageDimensions)]).Cast<ImageDimensions>().FirstOrDefault(),
            ImageMetaData = new ImageMetaDatum().FromTsvRowData(tableDataByTableName[nameof(ImageMetaDatum)]).Cast<ImageMetaDatum>().ToList(),
            TemperatureReadings = new TemperatureReading().FromTsvRowData(tableDataByTableName[nameof(TemperatureReading)]).Cast<TemperatureReading>().ToList(),
            TimeInfos = new TimeInfo().FromTsvRowData(tableDataByTableName[nameof(TimeInfo)]).Cast<TimeInfo>().FirstOrDefault(),
        };
    }

    public readonly string ExportAsTsv()
    {
        var thisObject = this;
        var data = typeof(VirtualImageMetaDataModel).GetProperties()
            .Where(p => p.GetMethod != null)
            .OrderBy(p => p.Name)
            .Select(p => p.GetMethod!.Invoke(thisObject, null)!).ToList();
        var result = new StringBuilder();
        foreach (var item in data)
        {
            var typeInfo = item.GetType();
            if (typeInfo.IsAssignableTo(typeof(System.Collections.IEnumerable)))
            {
                var exportHeader = true;
                foreach (var listModel in (System.Collections.IEnumerable)item)
                {
                    result.AppendLine((listModel as ITsvFormattable)!.GetTsv(exportHeader, typeInfo.Name));
                    exportHeader = false;
                }
            }
            if (typeInfo.IsAssignableTo(typeof(ITsvFormattable)))
            {
                result.AppendLine((item as ITsvFormattable)!.GetTsv(true, typeInfo.Name));
            }
            result.AppendLine();
        }
        result.Replace("\r\n", "\n");
        return result.ToString();
    }
}
