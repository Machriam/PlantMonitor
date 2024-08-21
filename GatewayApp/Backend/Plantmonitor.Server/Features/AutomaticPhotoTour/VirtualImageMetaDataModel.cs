using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Plantmonitor.Server.Features.AutomaticPhotoTour;

public static class ITsvFormattableExtensions
{
    public static string GetTsv(this ITsvFormattable model, bool withHeader)
    {
        var result = new StringBuilder();
        if (withHeader)
        {
            foreach (var property in model.GetType().GetProperties()) result.Append(property.Name + "\t");
            result.AppendLine();
        }
        foreach (var property in model.GetType().GetProperties()) result.Append(model.FormatValue(property.Name, property.GetValue(model)) + "\t");
        return result.ToString();
    }
}

public interface ITsvFormattable
{
    string FormatValue(string name, object? value);
}

public record struct VirtualImageMetaDataModel()
{
    public record struct ImageDimensions(int Width, int Height, int ImageWidth, int ImageHeight, int LeftPadding, int TopPadding, int ImagesPerRow, int RowCount, int ImageCount, string Comment) : ITsvFormattable
    {
        public readonly string FormatValue(string name, object? value) => value?.ToString() ?? "";
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
    }

    public ImageDimensions Dimensions { get; init; } = new();
    public ImageMetaDatum[] ImageMetaData { get; init; } = [];
    public TimeInfo TimeInfos { get; set; } = new();
    public ICollection<TemperatureReading> TemperatureReadings { get; init; } = [];

    public static VirtualImageMetaDataModel FromTsvFile(string tsv)
    {
        return new();
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
                    result.AppendLine((listModel as ITsvFormattable)!.GetTsv(exportHeader));
                    exportHeader = false;
                }
            }
            if (typeInfo.IsAssignableTo(typeof(ITsvFormattable)))
            {
                result.AppendLine((item as ITsvFormattable)!.GetTsv(true));
            }
            result.AppendLine();
        }
        result.Replace("\r\n", "\n");
        return result.ToString();
    }
}
