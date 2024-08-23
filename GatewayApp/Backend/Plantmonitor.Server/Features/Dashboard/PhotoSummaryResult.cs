using Emgu.CV;
using Emgu.CV.CvEnum;
using Plantmonitor.Server.Features.AutomaticPhotoTour;

namespace Plantmonitor.Server.Features.Dashboard;

public class PhotoSummaryResult(float pixelSizeInMm)
{
    public record struct PixelInfo(int Left, int Top, float Temperature, byte[] pixelColorInRgb);
    public record struct ImageResult(VirtualImageMetaDataModel.ImageMetaDatum Plant, float Size, float AverageTemperature, float MedianTemperature, float TemperatureDev,
        int Height, int Width, float Extent);
    private readonly Dictionary<VirtualImageMetaDataModel.ImageMetaDatum, List<PixelInfo>> _result = [];

    public void AddPixelInfo(VirtualImageMetaDataModel.ImageMetaDatum image, int left, int top, float temperature, byte[] pixelColorInRgb)
    {
        if (_result.TryGetValue(image, out var list)) list.Add(new(left, top, temperature, pixelColorInRgb));
        else _result[image] = [new(left, top, temperature, pixelColorInRgb)];
    }

    public List<ImageResult> GetResults()
    {
        foreach (var (image, pixelList) in _result)
        {
            File.WriteAllText($"C:\\Repos\\Plantmonitor\\PlantMonitor\\PlantMonitorControl.Tests\\TestData\\PhotoTourSummaryTest\\{image.ImageName}.json", pixelList.AsJson());
            var width = pixelList.Max(p => p.Left) - pixelList.Min(p => p.Left);
            var leftOffset = pixelList.Min(p => p.Left);
            var height = pixelList.Max(p => p.Top) - pixelList.Min(p => p.Top);
            var topOffset = pixelList.Min(p => p.Top);
            var testMat = new Mat(height, width, DepthType.Cv8U, 3);
            var testMatData = new byte[height * width * 3];
            var data = testMat.GetData(true);
            var emptyPixel = new byte[] { 255, 255, 255 };
            for (var row = 0; row < testMat.Rows; row++)
            {
                for (var col = 0; col < testMat.Cols; col++)
                {
                    var left = col + leftOffset;
                    var top = row + topOffset;
                    var pixel = pixelList.Find(p => p.Left == left && p.Top == top);
                    var rgb = pixel == default ? emptyPixel : pixel.pixelColorInRgb;
                    for (var i = 0; i < rgb.Length; i++) testMatData[row * col + i] = rgb[i];
                }
            }
            testMat.SetTo(testMatData);
            CvInvoke.Imshow(image.ImageName, testMat);
            CvInvoke.WaitKey();
        }
        return [];
    }
}
