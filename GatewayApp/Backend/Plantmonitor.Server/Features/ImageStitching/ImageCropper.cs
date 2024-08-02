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
        var decimalMat = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv32F, 1);
        var zeroMat = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv8U, 1);
        zeroMat.SetTo(new MCvScalar(0));

        CvInvoke.Subtract(irImage, subtractMat, currentValueMat, dtype: Emgu.CV.CvEnum.DepthType.Cv32F);
        currentValueMat.CopyTo(decimalMat);

        subtractMat.SetTo(new MCvScalar(50d));
        CvInvoke.Subtract(currentValueMat, subtractMat, currentValueMat);
        CvInvoke.Multiply(currentValueMat, divisor100, currentValueMat, dtype: Emgu.CV.CvEnum.DepthType.Cv32S);
        var integerMat = currentValueMat.Clone();
        integerMat.ConvertTo(integerMat, Emgu.CV.CvEnum.DepthType.Cv8U);
        CvInvoke.Multiply(currentValueMat, multiply100, currentValueMat);
        CvInvoke.Subtract(decimalMat, currentValueMat, decimalMat, null, Emgu.CV.CvEnum.DepthType.Cv32F);

        decimalMat.ConvertTo(decimalMat, Emgu.CV.CvEnum.DepthType.Cv8U);

        var resultMat = new Mat(irImage.Rows, irImage.Cols, Emgu.CV.CvEnum.DepthType.Cv8U, 3);
        var decimalRange = decimalMat.GetValueRange();
        var fullRange = integerMat.GetValueRange();
        var emptyRange = zeroMat.GetValueRange();
        CvInvoke.Merge(new VectorOfMat(integerMat, decimalMat, zeroMat), resultMat);
        subtractMat.Dispose();
        currentValueMat.Dispose();
        integerMat.Dispose();
        decimalMat.Dispose();
        zeroMat.Dispose();
        divisor100.Dispose();
        multiply100.Dispose();
        if (decimalRange.Max > 100)
        {
            Log.Logger.Warning("decimal channel was over 100: {from}-{to}", decimalRange.Min, decimalRange.Max);
            Thread.Sleep(50);
            return CreateRawIr(irImage);
        }
        if (fullRange.Min < 5 || fullRange.Max > 100)
        {
            Log.Logger.Warning("integer channel was not in bounds: {from}-{to}", fullRange.Min, fullRange.Max);
            Thread.Sleep(50);
            return CreateRawIr(irImage);
        }
        if (emptyRange.Min > 0 || emptyRange.Max > 0)
        {
            Log.Logger.Warning("empty channel was not zero: {from}-{to}", emptyRange.Min, emptyRange.Max);
            Thread.Sleep(50);
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
        var roi = CalculateSafeRoi(polygon, mat);
        return new Mat(mat, roi);
    }

    private static Rectangle CalculateSafeRoi(NpgsqlPoint[] polygon, Mat mat)
    {
        var minX = Math.Min(mat.Cols, Math.Max(0, polygon.Min(p => p.X)));
        var minY = Math.Min(mat.Rows, Math.Max(0, polygon.Min(p => p.Y)));
        var width = Math.Min(mat.Cols, polygon.Max(p => p.X)) - minX;
        var height = Math.Min(mat.Rows, polygon.Max(p => p.Y)) - minY;
        return new Rectangle((int)minX, (int)minY, (int)width, (int)height);
    }

    private static Point CalculateSafePoint(NpgsqlPoint point, Mat mat)
    {
        var x = (int)point.X;
        var y = (int)point.Y;
        return new Point(x < 0 ? 0 : x > mat.Cols ? mat.Cols : x, y < 0 ? 0 : y > mat.Rows ? mat.Rows : y);
    }

    private static Mat CutImage(NpgsqlPoint[] polygon, Mat mat)
    {
        var roi = CalculateSafeRoi(polygon, mat);
        var polygonCrop = new Mat(mat.Rows, mat.Cols, mat.Depth, mat.NumberOfChannels);
        var cvPolygon = new VectorOfVectorOfPoint(new VectorOfPoint(polygon.Select(p => CalculateSafePoint(p, polygonCrop)).ToArray()));
        CvInvoke.FillPoly(polygonCrop, cvPolygon, new MCvScalar(255, 255, 255));
        mat.CopyTo(polygonCrop, polygonCrop);
        var result = new Mat(polygonCrop, roi);
        var data = result.GetData(true);
        polygonCrop.Dispose();
        var cornerValues = GetCornerValues(result);
        if (!cornerValues.Any(cv => cv[0] == 0 && cv[1] == 0 && cv[2] == 0))
        {
            Log.Logger.Warning("Cropping of Vis-image did not work");
            Thread.Sleep(50);
            return CutImage(polygon, mat);
        }
        return result;
    }

    private static List<byte[]> GetCornerValues(Mat mat)
    {
        var data = mat.GetData(true);
        var upperLeft = Enumerable.Repeat(0, mat.NumberOfChannels).Select(c => (byte)data.GetValue(0, 0, c)!).ToArray();
        var bottomLeft = Enumerable.Repeat(0, mat.NumberOfChannels).Select(c => (byte)data.GetValue(mat.Rows - 1, 0, c)!).ToArray();
        var bottomRight = Enumerable.Repeat(0, mat.NumberOfChannels).Select(c => (byte)data.GetValue(mat.Rows - 1, mat.Cols - 1, c)!).ToArray();
        var upperRight = Enumerable.Repeat(0, mat.NumberOfChannels).Select(c => (byte)data.GetValue(mat.Rows - 1, 0, c)!).ToArray();
        return [upperLeft, bottomLeft, bottomRight, upperRight];
    }
}
