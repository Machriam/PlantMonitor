using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Plantmonitor.Server.Features.AutomaticPhotoTour;

namespace Plantmonitor.Server.Features.Dashboard;

public class PhotoSummaryResult(float pixelSizeInMm)
{
    public record struct PixelInfo(int Left, int Top, float Temperature, byte[] PixelColorInRgb, bool LeafOutOfRange);
    public record struct ImageResult(VirtualImageMetaDataModel.ImageMetaDatum Plant, float SizeInMm2, float AverageTemperature,
        float MedianTemperature, float TemperatureDev, float MaxTemperature, float MinTemperature,
        float HeightInMm, float WidthInMm, float Extent, float ConvexHullAreaInMm2, float Solidity, int LeafCount, bool LeafOutOfRange, float[] HslAverage,
        float[] HslMedian, float[] HslMax, float[] HslMin, float[] HslDeviation, bool NoImage);
    private readonly Dictionary<VirtualImageMetaDataModel.ImageMetaDatum, List<PixelInfo>> _result = [];

    public void AddPixelInfo(VirtualImageMetaDataModel.ImageMetaDatum image, int left, int top, float temperature, byte[] pixelColorInRgb, bool leafOutOfRange)
    {
        if (_result.TryGetValue(image, out var list)) list.Add(new(left, top, temperature, pixelColorInRgb, leafOutOfRange));
        else _result[image] = [new(left, top, temperature, pixelColorInRgb, leafOutOfRange)];
    }

    public List<ImageResult> GetResults()
    {
        var resultList = new List<ImageResult>();
        foreach (var (image, pixelList) in _result)
        {
            var result = new ImageResult { Plant = image };
            if (pixelList.Count == 0)
            {
                result.NoImage = true;
                resultList.Add(result);
                continue;
            }
            var subImage = CreateSubImage(pixelList);
            var grayImage = new Mat();
            CvInvoke.CvtColor(subImage, grayImage, ColorConversion.Rgb2Gray);
            var hslValues = pixelList.ConvertAll(pl => pl.PixelColorInRgb.Rgb2Hsl());
            result.HslAverage = [hslValues.Average(hsl => hsl[0]), hslValues.Average(hsl => hsl[1]), hslValues.Average(hsl => hsl[2])];
            result.HslMax = [hslValues.Max(hsl => hsl[0]), hslValues.Max(hsl => hsl[1]), hslValues.Max(hsl => hsl[2])];
            result.HslMin = [hslValues.Min(hsl => hsl[0]), hslValues.Min(hsl => hsl[1]), hslValues.Min(hsl => hsl[2])];
            result.HslMedian = [
                hslValues.OrderBy(hsl => hsl[0]).Median(hsl=>hsl[0]),
                hslValues.OrderBy(hsl => hsl[1]).Median(hsl=>hsl[1]),
                hslValues.OrderBy(hsl => hsl[2]).Median(hsl=>hsl[2])];
            result.HslDeviation = [
                hslValues.Deviation(result.HslAverage[0], hsl => hsl[0]),
                hslValues.Deviation(result.HslAverage[1], hsl => hsl[1]),
                hslValues.Deviation(result.HslAverage[2], hsl => hsl[2])];
            result.AverageTemperature = pixelList.Average(p => p.Temperature);
            result.LeafOutOfRange = pixelList.Any(pl => pl.LeafOutOfRange);
            result.MedianTemperature = pixelList.OrderBy(p => p.Temperature).Median(p => p.Temperature);
            result.MaxTemperature = pixelList.MaxBy(p => p.Temperature).Temperature;
            result.MinTemperature = pixelList.MinBy(p => p.Temperature).Temperature;
            result.TemperatureDev = pixelList.Deviation(result.AverageTemperature, pi => pi.Temperature);
            result.HeightInMm = subImage.Rows * pixelSizeInMm;
            result.WidthInMm = subImage.Cols * pixelSizeInMm;
            result.SizeInMm2 = pixelList.Count * pixelSizeInMm * pixelSizeInMm;
            result.Extent = (pixelList.Count / ((float)subImage.Height * subImage.Width));
            using var contours = new VectorOfVectorOfPoint();
            CvInvoke.FindContours(grayImage, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);
            var pointArray = new List<Point>();
            for (var i = 0; i < contours.Size; i++)
            {
                for (var j = 0; j < contours[i].Size; j++) pointArray.Add(contours[i][j]);
            }
            using var hullPoints = new VectorOfPoint(pointArray.ToArray());
            using var hull = new VectorOfPoint();
            CvInvoke.ConvexHull(hullPoints, hull, false, true);
            var convexHullArea = (float)CvInvoke.ContourArea(hull);
            result.ConvexHullAreaInMm2 = convexHullArea * pixelSizeInMm * pixelSizeInMm;
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
                var rgb = pixel == default ? emptyPixel : pixel.PixelColorInRgb;
                var index = ((row * width) + col) * 3;
                for (var i = 0; i < rgb.Length; i++) subImageData[index + i] = rgb[i];
            }
        }
        subImageMat.SetTo(subImageData);
        return subImageMat;
    }
}
