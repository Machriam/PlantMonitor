using NpgsqlTypes;
using Emgu.CV;
using System.Drawing;
using Emgu.CV.Util;
using Emgu.CV.Structure;
using Plantmonitor.Shared.Features.ImageStreaming;
using Serilog;

namespace Plantmonitor.Server.Features.ImageStitching;

public interface IImageCropper
{
    (Mat VisImage, Mat? IrImage) CropImages(string visImage, string? irImage, NpgsqlPoint[] visPolygon, NpgsqlPoint irOffset, int scalingHeightInPx);

    void ApplyIrColorMap(Mat irImage);

    Mat CreateRawIr(Mat irImage);
}

public class ImageCropper() : IImageCropper
{
    private const int ZeroDegreeCelsius = 27315;

    public Mat CreateRawIr(Mat irImage)
    {
        var subtractMat = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv32F, 1);
        var divisor100 = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv32F, 1);
        divisor100.SetTo(new MCvScalar(1 / 100d));
        var multiply100 = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv32S, 1);
        multiply100.SetTo(new MCvScalar(100d));
        subtractMat.SetTo(new MCvScalar(ZeroDegreeCelsius));
        var currentValueMat = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv32F, 1);
        var commaValueMat = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv32F, 1);
        var emptyMat = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
        emptyMat.SetTo(new MCvScalar(0));

        CvInvoke.Subtract(irImage, subtractMat, currentValueMat, dtype: Emgu.CV.CvEnum.DepthType.Cv32F);
        currentValueMat.CopyTo(commaValueMat);

        subtractMat.SetTo(new MCvScalar(50d));
        CvInvoke.Subtract(currentValueMat, subtractMat, currentValueMat);
        CvInvoke.Multiply(currentValueMat, divisor100, currentValueMat, dtype: Emgu.CV.CvEnum.DepthType.Cv32S);
        var fullValueMat = currentValueMat.Clone();
        fullValueMat.ConvertTo(fullValueMat, Emgu.CV.CvEnum.DepthType.Cv8U);
        CvInvoke.Multiply(currentValueMat, multiply100, currentValueMat);
        CvInvoke.Subtract(commaValueMat, currentValueMat, commaValueMat, null, Emgu.CV.CvEnum.DepthType.Cv32F);

        commaValueMat.ConvertTo(commaValueMat, Emgu.CV.CvEnum.DepthType.Cv8U);

        var resultMat = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv8U, 3);
        var decimalRange = commaValueMat.GetValueRange();
        var fullRange = fullValueMat.GetValueRange();
        var emptyRange = emptyMat.GetValueRange();
        CvInvoke.Merge(new VectorOfMat(fullValueMat, commaValueMat, emptyMat), resultMat);
        subtractMat.Dispose();
        currentValueMat.Dispose();
        fullValueMat.Dispose();
        commaValueMat.Dispose();
        emptyMat.Dispose();
        divisor100.Dispose();
        multiply100.Dispose();
        if (decimalRange.Max > 100)
        {
            Log.Logger.Warning("decimal channel was over 100: {from}-{to}", decimalRange.Min, decimalRange.Max);
            return CreateRawIr(irImage);
        }
        if (fullRange.Min < 5 || fullRange.Max > 100)
        {
            Log.Logger.Warning("integer channel was not in bounds: {from}-{to}", fullRange.Min, fullRange.Max);
            return CreateRawIr(irImage);
        }
        if (emptyRange.Min > 0 || emptyRange.Max > 0)
        {
            Log.Logger.Warning("empty channel was not zero: {from}-{to}", emptyRange.Min, emptyRange.Max);
            return CreateRawIr(irImage);
        }
        return resultMat;
    }

    public void ApplyIrColorMap(Mat irImage)
    {
        var baselineMat = new Mat(irImage.Rows, irImage.Cols, irImage.Depth, 1);
        baselineMat.SetTo(new MCvScalar(ZeroDegreeCelsius + 1500));
        var scaleMat = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv32F, 1);
        scaleMat.SetTo(new MCvScalar(1 / 10d));
        CvInvoke.Subtract(irImage, baselineMat, irImage);
        irImage.ConvertTo(irImage, Emgu.CV.CvEnum.DepthType.Cv32F);
        CvInvoke.Multiply(irImage, scaleMat, irImage, 1, Emgu.CV.CvEnum.DepthType.Cv32F);
        irImage.ConvertTo(irImage, Emgu.CV.CvEnum.DepthType.Cv8U);
        var inverseMat = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
        inverseMat.SetTo(new MCvScalar(255));
        CvInvoke.Subtract(inverseMat, irImage, irImage);
        CvInvoke.ApplyColorMap(irImage, irImage, Emgu.CV.CvEnum.ColorMapType.Rainbow);
        baselineMat.Dispose();
        scaleMat.Dispose();
        inverseMat.Dispose();
    }

    public Mat MatFromFile(string filename, out bool isIr)
    {
        isIr = false;
        if (!filename.EndsWith(CameraType.IR.GetInfo().FileEnding)) return CvInvoke.Imread(filename);
        var irMat = new Mat(120, 160, Emgu.CV.CvEnum.DepthType.Cv32S, 1);
        var tempArray = File.ReadAllBytes(filename).Chunk(4).Select(b => (int)BitConverter.ToUInt32(b)).ToArray();
        irMat.SetTo(tempArray);
        isIr = true;
        return irMat;
    }

    public void Resize(Mat mat, int height)
    {
        if (mat.Depth != Emgu.CV.CvEnum.DepthType.Cv8U) mat.ConvertTo(mat, Emgu.CV.CvEnum.DepthType.Cv32F);
        CvInvoke.Resize(mat, mat, new Size((int)(mat.Width * height / (float)mat.Height), height));
    }

    public (Mat VisImage, Mat? IrImage) CropImages(string visImage, string? irImage, NpgsqlPoint[] visPolygon, NpgsqlPoint irOffset, int scalingHeightInPx)
    {
        var visMat = MatFromFile(visImage, out _);
        const float DefaultOffsetHeight = 480f;
        var resizeRatio = scalingHeightInPx / (double)visMat.Height;
        visPolygon = visPolygon.Select(vp => new NpgsqlPoint(vp.X * resizeRatio, vp.Y * resizeRatio)).ToArray();
        irOffset = new NpgsqlPoint(-irOffset.X * scalingHeightInPx / DefaultOffsetHeight, -irOffset.Y * scalingHeightInPx / DefaultOffsetHeight);
        Resize(visMat, scalingHeightInPx);
        var visCrop = CutImage(visPolygon, visMat);
        if (irImage.IsEmpty())
        {
            visMat.Dispose();
            return (visCrop, null);
        }
        var irMat = MatFromFile(irImage!, out _);
        Resize(irMat, scalingHeightInPx);
        var irPolygon = visPolygon
            .Select(p => new NpgsqlPoint(p.X + irOffset.X, p.Y + irOffset.Y))
            .ToArray();
        var irCrop = CutIrImage(irPolygon, irMat);
        visMat.Dispose();
        irMat.Dispose();
        return (visCrop, irCrop);
    }

    private static Mat CutIrImage(NpgsqlPoint[] polygon, Mat mat)
    {
        var minX = Math.Max(0, polygon.Min(p => p.X));
        var minY = Math.Max(0, polygon.Min(p => p.Y));
        var width = Math.Min(mat.Cols, polygon.Max(p => p.X)) - minX;
        var height = Math.Min(mat.Rows, polygon.Max(p => p.Y)) - minY;
        var roi = new Rectangle((int)minX, (int)minY, (int)width, (int)height);
        return new Mat(mat, roi);
    }

    private static Mat CutImage(NpgsqlPoint[] polygon, Mat mat)
    {
        var minX = polygon.Min(p => p.X);
        var minY = polygon.Min(p => p.Y);
        var width = polygon.Max(p => p.X) - minX;
        var height = polygon.Max(p => p.Y) - minY;
        var roi = new Rectangle((int)minX, (int)minY, (int)width, (int)height);
        var polygonCrop = new Mat(mat.Rows, mat.Cols, mat.Depth, mat.NumberOfChannels);
        var cvPolygon = new VectorOfVectorOfPoint(new VectorOfPoint(polygon.Select(p => new Point((int)p.X, (int)p.Y)).ToArray()));
        CvInvoke.FillPoly(polygonCrop, cvPolygon, new MCvScalar(255, 255, 255));
        mat.CopyTo(polygonCrop, polygonCrop);
        var result = new Mat(polygonCrop, roi);
        polygonCrop.Dispose();
        return result;
    }
}
