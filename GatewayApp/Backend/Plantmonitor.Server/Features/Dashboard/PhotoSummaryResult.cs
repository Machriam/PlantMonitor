using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Plantmonitor.Server.Features.AutomaticPhotoTour;

namespace Plantmonitor.Server.Features.Dashboard;

public class PhotoSummaryResult(float pixelSizeInMm)
{
    public record struct PixelInfo(int Left, int Top, float Temperature, byte[] pixelColorInRgb);
    public record struct ImageResult(VirtualImageMetaDataModel.ImageMetaDatum Plant, float SizeInMm2, float AverageTemperature,
        float MedianTemperature, float TemperatureDev, float MaxTemperature, float MinTemperature,
        float HeightInMm, float WidthInMm, float Extent, float Roundness, float ConvexHullAreaInMm2, float Solidity);
    private readonly Dictionary<VirtualImageMetaDataModel.ImageMetaDatum, List<PixelInfo>> _result = [];

    public void AddPixelInfo(VirtualImageMetaDataModel.ImageMetaDatum image, int left, int top, float temperature, byte[] pixelColorInRgb)
    {
        if (_result.TryGetValue(image, out var list)) list.Add(new(left, top, temperature, pixelColorInRgb));
        else _result[image] = [new(left, top, temperature, pixelColorInRgb)];
    }

    public List<ImageResult> GetResults()
    {
        var resultList = new List<ImageResult>();
        foreach (var (image, pixelList) in _result)
        {
            var result = new ImageResult { Plant = image };
            if (pixelList.Count == 0)
            {
                resultList.Add(result);
                continue;
            }
            var subImage = CreateSubImage(pixelList);
            var grayImage = new Mat();
            CvInvoke.CvtColor(subImage, grayImage, ColorConversion.Rgb2Gray);
            result.AverageTemperature = pixelList.Average(p => p.Temperature);
            result.MedianTemperature = pixelList.OrderBy(p => p.Temperature).Skip(pixelList.Count / 2).FirstOrDefault().Temperature;
            result.MaxTemperature = pixelList.MaxBy(p => p.Temperature).Temperature;
            result.MinTemperature = pixelList.MinBy(p => p.Temperature).Temperature;
            result.TemperatureDev = (float)Math.Sqrt(pixelList
                .Average(p => (p.Temperature - result.AverageTemperature) * (p.Temperature - result.AverageTemperature)));
            result.HeightInMm = subImage.Rows * pixelSizeInMm;
            result.WidthInMm = subImage.Cols * pixelSizeInMm;
            result.SizeInMm2 = pixelList.Count * pixelSizeInMm * pixelSizeInMm;
            result.Extent = (pixelList.Count / ((float)subImage.Height * subImage.Width));
            using var contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(grayImage, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
            CvInvoke.DrawContours(grayImage, contours, -1, new MCvScalar(100, 100, 100));
            var perimeter = 0f;
            var convexHullArea = 0f;
            for (var i = 0; i < contours.Size; i++)
            {
                var contour = contours[i];
                perimeter += (float)CvInvoke.ArcLength(contour, true);
                using var hull = new VectorOfPoint();
                CvInvoke.ConvexHull(contour, hull, false, true);
                CvInvoke.Polylines(grayImage, hull, true, new MCvScalar(100, 100, 100), 3);
                convexHullArea += (float)CvInvoke.ContourArea(hull);
            }
            result.ConvexHullAreaInMm2 = convexHullArea * pixelSizeInMm * pixelSizeInMm;
            result.Roundness = (float)(4f * Math.PI * pixelList.Count / (perimeter * perimeter));
            result.Solidity = (float)(pixelList.Count / convexHullArea);
            resultList.Add(result);
            subImage.Dispose();
            grayImage.Dispose();
        }
        return resultList;
    }

    private static Mat CreateSubImage(List<PixelInfo> pixelList)
    {
        var width = pixelList.Max(p => p.Left) - pixelList.Min(p => p.Left);
        var leftOffset = pixelList.Min(p => p.Left);
        var height = pixelList.Max(p => p.Top) - pixelList.Min(p => p.Top);
        var topOffset = pixelList.Min(p => p.Top);
        var subImageMat = new Mat(height, width, DepthType.Cv8U, 3);
        var subImageData = new byte[height * width * 3];
        var data = subImageMat.GetData(true);
        var emptyPixel = new byte[] { 0, 0, 0 };
        for (var row = 0; row < subImageMat.Rows; row++)
        {
            for (var col = 0; col < subImageMat.Cols; col++)
            {
                var left = col + leftOffset;
                var top = row + topOffset;
                var pixel = pixelList.Find(p => p.Left == left && p.Top == top);
                var rgb = pixel == default ? emptyPixel : pixel.pixelColorInRgb;
                var index = ((row * width) + col) * 3;
                for (var i = 0; i < rgb.Length; i++) subImageData[index + i] = rgb[i];
            }
        }
        subImageMat.SetTo(subImageData);
        return subImageMat;
    }
}
