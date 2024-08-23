using Plantmonitor.Server.Features.AutomaticPhotoTour;

namespace Plantmonitor.Server.Features.Dashboard;

public class PhotoSummaryResult(float PixelSizeInMm)
{
    public record struct PixelInfo(int Left, int Top, float Temperature);
    public record struct ImageResult(VirtualImageMetaDataModel.ImageMetaDatum Plant, float Size, float AverageTemperature, float MedianTemperature, float TemperatureDev,
        int Height, int Width, float Extent);
    private readonly Dictionary<VirtualImageMetaDataModel.ImageMetaDatum, List<PixelInfo>> _result = [];

    public void AddPixelInfo(VirtualImageMetaDataModel.ImageMetaDatum image, int left, int top, float temperature)
    {
        if (_result.TryGetValue(image, out var list)) list.Add(new(left, top, temperature));
        else _result[image] = [new(left, top, temperature)];
    }

    public List<ImageResult> GetResults()
    {
        foreach (var (image, pixelList) in _result)
        {
        }
        return [];
    }
}
