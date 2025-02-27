﻿using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Shared.Extensions;

namespace Plantmonitor.ImageWorker;

public class PhotoSummaryResult(float pixelSizeInMm)
{
    public record struct PhotoTripData(string TourName, DateTime TripStart, DateTime TripEnd, long PhotoTourId, long PhotoTripId);
    public record struct DeviceTemperatureInfo(string Name, float MaxTemperature,
        float MinTemperature, float AverageTemperature, float MedianTemperature, float TemperatureDeviation, int CountOfMeasurements);
    public record struct PixelInfo(int Left, int Top, float Temperature, byte[] PixelColorInRgb, bool LeafOutOfRange);
    public record struct ImageResult(VirtualImageMetaDataModel.ImageMetaDatum Plant, float SizeInMm2, float AverageTemperature,
        float MedianTemperature, float TemperatureDev, float MaxTemperature, float MinTemperature, int PixelCount,
        float HeightInMm, float WidthInMm, float Extent, float ConvexHullAreaInMm2, float Solidity, int LeafCount, bool LeafOutOfRange, float[] HslAverage,
        float[] HslMedian, float[] HslMax, float[] HslMin, float[] HslDeviation, bool NoImage, List<DeviceTemperatureInfo> DeviceTemperatures)
    {
        public readonly PlantImageDescriptors GetDataModel()
        {
            return new PlantImageDescriptors()
            {
                AverageTemperature = AverageTemperature,
                ConvexHullAreaInMm2 = ConvexHullAreaInMm2,
                Extent = Extent,
                HeightInMm = HeightInMm,
                HslAverage = HslAverage,
                HslDeviation = HslDeviation,
                HslMax = HslMax,
                HslMedian = HslMedian,
                HslMin = HslMin,
                LeafCount = LeafCount,
                LeafOutOfRange = LeafOutOfRange,
                MaxTemperature = MaxTemperature,
                PixelCount = PixelCount,
                MedianTemperature = MedianTemperature,
                MinTemperature = MinTemperature,
                NoImage = NoImage,
                Plant = new ReferencedPlant()
                {
                    HasIr = Plant.HasIr,
                    HasVis = Plant.HasVis,
                    ImageComment = Plant.ImageComment,
                    ImageIndex = Plant.ImageIndex,
                    ImageName = Plant.ImageName,
                    MotorPosition = Plant.MotorPosition,
                    IrTempInC = Plant.IrTempInC,
                    IrTime = Plant.IrTime,
                    VisTime = Plant.VisTime
                },
                SizeInMm2 = SizeInMm2,
                Solidity = Solidity,
                TemperatureDev = TemperatureDev,
                WidthInMm = WidthInMm
            };
        }
    }
    private readonly Dictionary<VirtualImageMetaDataModel.ImageMetaDatum, List<PixelInfo>> _result = [];
    private PhotoTripData _photoTripData = new();
    public PhotoTripData GetPhotoTripData => _photoTripData;
    public List<DeviceTemperatureInfo> DeviceTemperatures { get; } = [];

    public void AddPhotoTripData(string tourName, DateTime tripStart, DateTime tripEnd, long photoTourId, long photoTripId) => _photoTripData = new(tourName, tripStart, tripEnd, photoTourId, photoTripId);

    public void AddPixelInfo(VirtualImageMetaDataModel.ImageMetaDatum image, int left, int top, float temperature, byte[] pixelColorInRgb, bool leafOutOfRange)
    {
        if (_result.TryGetValue(image, out var list)) list.Add(new(left, top, temperature, pixelColorInRgb, leafOutOfRange));
        else _result[image] = [new(left, top, temperature, pixelColorInRgb, leafOutOfRange)];
    }

    public Dictionary<VirtualImageMetaDataModel.ImageMetaDatum, List<PixelInfo>> GetPixelInfo() => _result;

    public void AddDeviceTemperatures(IEnumerable<DeviceTemperatureInfo> deviceTemperatures) =>
        DeviceTemperatures.AddRange(deviceTemperatures.Where(dt => dt.AverageTemperature > 0f));

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
            if (subImage.Execute(x => x.Width) == 0 || subImage.Execute(x => x.Height) == 0)
            {
                result.NoImage = true;
                resultList.Add(result);
                continue;
            }
            var grayImage = new Mat().AsManaged();
            subImage.Execute(grayImage, (x, y) => CvInvoke.CvtColor(x, y, ColorConversion.Rgb2Gray));
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
            result.LeafOutOfRange = pixelList.Any(pl => pl.LeafOutOfRange);
            var temperatureList = pixelList.Select(pl => pl.Temperature).Where(t => t > 1 && t < 100).ToList();
            if (temperatureList.Count == 0) temperatureList.Add(-1f);
            result.AverageTemperature = temperatureList.Average();
            result.MedianTemperature = temperatureList.Order().Median(p => p);
            result.MaxTemperature = temperatureList.Max();
            result.MinTemperature = temperatureList.Min();
            result.TemperatureDev = temperatureList.Deviation(result.AverageTemperature, pi => pi);
            result.PixelCount = pixelList.Count;
            result.HeightInMm = subImage.Execute(x => x.Rows * pixelSizeInMm);
            result.WidthInMm = subImage.Execute(x => x.Cols * pixelSizeInMm);
            result.SizeInMm2 = pixelList.Count * pixelSizeInMm * pixelSizeInMm;
            result.Extent = pixelList.Count / ((float)subImage.Execute(x => x.Height) * subImage.Execute(x => x.Width));
            result.LeafCount = CalculateLeafCount(grayImage);
            var convexHullArea = GetConvexHull(grayImage);
            result.ConvexHullAreaInMm2 = convexHullArea * pixelSizeInMm * pixelSizeInMm;
            result.Solidity = convexHullArea > 0f ? (float)(pixelList.Count / convexHullArea) : 0f;
            resultList.Add(result);
            subImage.Dispose();
            grayImage.Dispose();
        }
        return resultList;
    }

    private static float GetConvexHull(IManagedMat grayImage)
    {
        using var contours = new VectorOfVectorOfPoint();
        grayImage.Execute(x => CvInvoke.FindContours(x, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple));
        var pointArray = new List<Point>();
        if (contours.Size == 0) return 0f;
        for (var i = 0; i < contours.Size; i++)
        {
            for (var j = 0; j < contours[i].Size; j++) pointArray.Add(contours[i][j]);
        }
        using var hullPoints = new VectorOfPoint(pointArray.ToArray());
        using var hull = new VectorOfPoint();
        CvInvoke.ConvexHull(hullPoints, hull, false, true);
        return (float)CvInvoke.ContourArea(hull);
    }

    private static int CalculateLeafCount(IManagedMat grayImage)
    {
        using var contours = new VectorOfVectorOfPoint();
        var leafCountMat = new Mat().AsManaged();
        var element = LeafStructuringElement();
        grayImage.Execute(x => CvInvoke.FindContours(x, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple));
        var leafCount = contours.Size;
        var currentLeafCount = leafCount;
        while (currentLeafCount > 0)
        {
            var matToUse = leafCountMat.Execute(x => x.Cols) > 0 ? leafCountMat : grayImage;
            matToUse.Execute(leafCountMat, element, (x, y, z) => CvInvoke.Erode(x, y, z, new Point(-1, -1), 1, BorderType.Constant, new MCvScalar(0d)));
            leafCountMat.Execute(x => CvInvoke.FindContours(x, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple));
            currentLeafCount = contours.Size;
            leafCount = Math.Max(currentLeafCount, leafCount);
        }

        leafCountMat.Dispose();
        element.Dispose();
        return leafCount;
    }

    private static IManagedMat LeafStructuringElement()
    {
        const string StructuringElement = "0\t1\t0\n1\t1\t1\n0\t1\t0";
        var byteArray = StructuringElement.Split("\n").Select(l => l.Split("\t")).SelectMany(l => l.Select(c => byte.Parse(c))).ToArray();
        var element = new Mat(5, 5, DepthType.Cv8U, 1).AsManaged();
        element.Execute(x => x.SetTo(byteArray));
        return element;
    }

    public IManagedMat CreateSubImage(List<PixelInfo> pixelList)
    {
        var pixelByCoordinate = pixelList.ToDictionary(p => (p.Left, p.Top));
        var width = pixelList.Max(p => p.Left) - pixelList.Min(p => p.Left);
        var leftOffset = pixelList.Min(p => p.Left);
        var height = pixelList.Max(p => p.Top) - pixelList.Min(p => p.Top);
        var topOffset = pixelList.Min(p => p.Top);
        var subImageMat = new Mat(height, width, DepthType.Cv8U, 3).AsManaged();
        var subImageData = new byte[height * width * 3];
        var data = subImageMat.Execute(x => x.GetData(true));
        var emptyPixel = new byte[] { 0, 0, 0 };
        var rowCount = subImageMat.Execute(x => x.Rows);
        var colCount = subImageMat.Execute(x => x.Cols);
        for (var row = 0; row < rowCount; row++)
        {
            for (var col = 0; col < colCount; col++)
            {
                var left = col + leftOffset;
                var top = row + topOffset;
                var pixel = pixelByCoordinate.TryGetValue((left, top), out var pixelResult) ? pixelResult : default;
                var rgb = pixel == default ? emptyPixel : pixel.PixelColorInRgb;
                var index = (row * width + col) * 3;
                for (var i = 0; i < rgb.Length; i++) subImageData[index + i] = rgb[i];
            }
        }
        subImageMat.Execute(x => x.SetTo(subImageData));
        return subImageMat;
    }
}
