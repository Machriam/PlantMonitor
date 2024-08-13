using NpgsqlTypes;
using Emgu.CV;
using System.Drawing;
using Emgu.CV.Util;
using Emgu.CV.Structure;
using Plantmonitor.Shared.Features.ImageStreaming;
using Serilog;
using Emgu.CV.CvEnum;
using Plantmonitor.Server.Features.AppConfiguration;

namespace Plantmonitor.Server.Features.ImageStitching;

public interface IImageCropper
{
    (Mat VisImage, Mat? IrImage) CropImages(string visImage, string? irImage, NpgsqlPoint[] visPolygon, NpgsqlPoint irOffset, int scalingHeightInPx);

    void ApplyIrColorMap(Mat irImage);

    Mat CreateRawIr(Mat irImage);

    byte[] MatToByteArray(Mat mat, bool disposeMat = true);
}

public class ImageCropper() : IImageCropper
{
    private const int ZeroDegreeCelsius = 27315;

    public Mat CreateRawIr(Mat irImage)
    {
        var data = irImage.GetData(true);
        var integerMat = new Mat(irImage.Rows, irImage.Cols, DepthType.Cv8U, 1);
        integerMat.SetTo(new MCvScalar(0));
        var zeroMat = new Mat(irImage.Rows, irImage.Cols, DepthType.Cv8U, 1);
        zeroMat.SetTo(new MCvScalar(0));
        var decimalMat = new Mat(irImage.Rows, irImage.Cols, DepthType.Cv8U, 1);
        decimalMat.SetTo(new MCvScalar(0d));
        var integerData = new byte[irImage.Rows * irImage.Cols];
        var decimalData = new byte[irImage.Rows * irImage.Cols];
        var index = 0;
        for (var row = 0; row < irImage.Rows; row++)
        {
            for (var col = 0; col < irImage.Cols; col++)
            {
                var rawValue = data.GetValue(row, col);
                var value = ZeroDegreeCelsius;
                if (rawValue is float) value = (int)(float)rawValue;
                else if (rawValue is int) value = (int)rawValue;
                value -= ZeroDegreeCelsius;
                var intValue = (value / 100);
                var decimalValue = (int)(((value / 100f) - intValue) * 100);
                integerData[index] = (byte)intValue;
                decimalData[index] = (byte)(decimalValue);
                index++;
            }
        }
        integerMat.SetTo(integerData);
        decimalMat.SetTo(decimalData);

        var resultMat = new Mat(irImage.Rows, irImage.Cols, DepthType.Cv8U, 3);
        resultMat.SetTo(new MCvScalar(0d, 0d, 0d));
        var decimalRange = decimalMat.GetValueRange();
        var fullRange = integerMat.GetValueRange();
        var emptyRange = zeroMat.GetValueRange();
        CvInvoke.Merge(new VectorOfMat(integerMat, decimalMat, zeroMat), resultMat);
        integerMat.Dispose();
        decimalMat.Dispose();
        zeroMat.Dispose();
        if (decimalRange.Max > 100)
        {
            Log.Logger.Error("decimal channel was over 100: {from}-{to}", decimalRange.Min, decimalRange.Max);
            throw new Exception("decimal channel was over 100");
        }
        if (fullRange.Min < 5 || fullRange.Max > 100)
        {
            Log.Logger.Error("integer channel was not in bounds: {from}-{to}", fullRange.Min, fullRange.Max);
            throw new Exception("integer channel was not in bounds: {from}-{to}");
        }
        if (emptyRange.Min > 0 || emptyRange.Max > 0)
        {
            Log.Logger.Error("empty channel was not zero: {from}-{to}", emptyRange.Min, emptyRange.Max);
            throw new Exception("empty channel was not zero: {from}-{to}");
        }
        return resultMat;
    }

    public void ApplyIrColorMap(Mat irImage)
    {
        if (irImage.Height == 0 || irImage.Width == 0) return;
        var baselineMat = new Mat(irImage.Rows, irImage.Cols, irImage.Depth, 1);
        baselineMat.SetTo(new MCvScalar(ZeroDegreeCelsius + 1500));
        var scaleMat = new Mat(irImage.Rows, irImage.Cols, DepthType.Cv32F, 1);
        scaleMat.SetTo(new MCvScalar(1 / 10d));
        CvInvoke.Subtract(irImage, baselineMat, irImage);
        irImage.ConvertTo(irImage, DepthType.Cv32F);
        CvInvoke.Multiply(irImage, scaleMat, irImage, 1, DepthType.Cv32F);
        irImage.ConvertTo(irImage, DepthType.Cv8U);
        var inverseMat = new Mat(irImage.Rows, irImage.Cols, DepthType.Cv8U, 1);
        inverseMat.SetTo(new MCvScalar(255));
        CvInvoke.Subtract(inverseMat, irImage, irImage);
        CvInvoke.ApplyColorMap(irImage, irImage, ColorMapType.Rainbow);
        baselineMat.Dispose();
        scaleMat.Dispose();
        inverseMat.Dispose();
    }

    public Mat MatFromFile(string filename, out bool isIr)
    {
        isIr = false;
        if (!filename.EndsWith(CameraType.IR.GetInfo().FileEnding)) return CvInvoke.Imread(filename);
        var irMat = new Mat(120, 160, DepthType.Cv32S, 1);
        var tempArray = File.ReadAllBytes(filename).Chunk(4).Select(b => (int)BitConverter.ToUInt32(b)).ToArray();
        irMat.SetTo(tempArray);
        isIr = true;
        return irMat;
    }

    public byte[] MatToByteArray(Mat mat, bool disposeMat = true)
    {
        if (mat.Width == 0 || mat.Height == 0) return [];
        var resultFile = Guid.NewGuid().ToString() + ".png";
        var fullPath = Path.Combine(Path.GetTempPath(), resultFile);
        CvInvoke.Imwrite(fullPath, mat);
        if (disposeMat) mat.Dispose();
        var result = File.ReadAllBytes(fullPath);
        File.Delete(fullPath);
        return result;
    }

    public void Resize(Mat mat, int height)
    {
        if (mat.Depth != DepthType.Cv8U) mat.ConvertTo(mat, DepthType.Cv32F);
        CvInvoke.Resize(mat, mat, new Size((int)(mat.Width * height / (float)mat.Height), height));
    }

    public (Mat VisImage, Mat? IrImage) CropImages(string visImage, string? irImage, NpgsqlPoint[] visPolygon, NpgsqlPoint irOffset, int scalingHeightInPx)
    {
        var visMat = MatFromFile(visImage, out _);
        var resizeRatio = scalingHeightInPx / (double)visMat.Height;
        visPolygon = visPolygon.Select(vp => new NpgsqlPoint(vp.X * resizeRatio, vp.Y * resizeRatio)).ToArray();
        irOffset = new NpgsqlPoint(-irOffset.X * scalingHeightInPx / Const.IrScalingHeight, -irOffset.Y * scalingHeightInPx / Const.IrScalingHeight);
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
        var roi = CalculateInboundRoi(polygon, mat);
        return new Mat(mat, roi);
    }

    private static Rectangle CalculateInboundRoi(NpgsqlPoint[] polygon, Mat mat)
    {
        var minPolygonX = Math.Max(0, polygon.Min(p => p.X));
        var minPolygonY = Math.Max(0, polygon.Min(p => p.Y));
        var maxPolygonX = Math.Max(0, polygon.Max(p => p.X));
        var maxPolygonY = Math.Max(0, polygon.Max(p => p.Y));
        var minX = Math.Min(mat.Cols, minPolygonX);
        var minY = Math.Min(mat.Rows, minPolygonY);
        var width = Math.Min(mat.Cols, maxPolygonX) - minX;
        var height = Math.Min(mat.Rows, maxPolygonY) - minY;
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
        var roi = CalculateInboundRoi(polygon, mat);
        var polygonCrop = new Mat(mat.Rows, mat.Cols, mat.Depth, mat.NumberOfChannels);
        var mask = new Mat(mat.Rows, mat.Cols, DepthType.Cv8U, 1);
        polygonCrop.SetTo(new MCvScalar(0, 0, 0));
        mask.SetTo(new MCvScalar(0));
        var cvPolygon = new VectorOfVectorOfPoint(new VectorOfPoint(polygon.Select(p => CalculateSafePoint(p, polygonCrop)).ToArray()));
        CvInvoke.FillPoly(mask, cvPolygon, new MCvScalar(255));
        mat.CopyTo(polygonCrop, mask);
        var result = new Mat(polygonCrop, roi);
        polygonCrop.Dispose();
        mask.Dispose();
        return result;
    }
}
