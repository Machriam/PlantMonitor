namespace Plantmonitor.Server.Features.AutomaticPhotoTour;

public readonly record struct VirtualImageMetaDataModel()
{
    public record struct ImageDimensions(int Width, int Height, int ImageWidth, int ImageHeight, int LeftPadding, int TopPadding, int ImagesPerRow, int RowCount, int ImageCount, string Comment);
    public record struct ImageMetaDatum(int ImageIndex, string ImageName, string ImageComment, bool HasIr, bool HasVis, DateTime IrTime, DateTime VisTime, float IrTempInK);
    public record struct TimeInfo(DateTime StartTime, DateTime EndTime);
    public record struct TemperatureReading(string SensorId, string Comment, float TemperatureInC, DateTime Time);
    public ImageDimensions Dimensions { get; init; } = new();
    public ImageMetaDatum[] ImageMetaData { get; init; } = [];
    public TimeInfo[] TimeInfos { get; init; } = [];
    public ICollection<TemperatureReading> TemperatureReadings { get; init; } = [];
    public static VirtualImageMetaDataModel FromTsvFile(string tsv)
    {
        return new();
    }
    public string ExportAsTsv()
    {
        return "";
    }
}
