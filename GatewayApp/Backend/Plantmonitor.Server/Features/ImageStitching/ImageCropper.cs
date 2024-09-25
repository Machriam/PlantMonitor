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
    (IManagedMat VisImage, IManagedMat? IrImage) CropImages(string visImage, string? irImage, NpgsqlPoint[] visPolygon, NpgsqlPoint irOffset, int scalingHeightInPx);

    void ApplyIrColorMap(IManagedMat irImage);

    IManagedMat CreateRawIr(IManagedMat irImage);

    byte[] MatToByteArray(IManagedMat mat, bool disposeMat = true);
}

public class ImageCropper() : IImageCropper
{
    private const int ZeroDegreeCelsius = 27315;

    public IManagedMat CreateRawIr(IManagedMat irImage)
    {
        var data = irImage.LogCall(x => x.GetData(true));
        var integerMat = new Mat(irImage.LogCall(x => x.Rows), irImage.LogCall(x => x.Cols), DepthType.Cv8U, 1).AsManaged();
        integerMat.LogCall(x => x.SetTo(new MCvScalar(0)));
        var zeroMat = new Mat(irImage.LogCall(x => x.Rows), irImage.LogCall(x => x.Cols), DepthType.Cv8U, 1).AsManaged();
        zeroMat.LogCall(x => x.SetTo(new MCvScalar(0)));
        var decimalMat = new Mat(irImage.LogCall(x => x.Rows), irImage.LogCall(x => x.Cols), DepthType.Cv8U, 1).AsManaged();
        decimalMat.LogCall(x => x.SetTo(new MCvScalar(0d)));
        var rowCount = irImage.LogCall(x => x.Rows);
        var colCount = irImage.LogCall(x => x.Cols);
        var integerData = new byte[rowCount * colCount];
        var decimalData = new byte[rowCount * colCount];
        var index = 0;
        for (var row = 0; row < rowCount; row++)
        {
            for (var col = 0; col < colCount; col++)
            {
                var rawValue = data.GetValue(row, col);
                var value = ZeroDegreeCelsius;
                if (rawValue is float) value = (int)(float)rawValue;
                else if (rawValue is int) value = (int)rawValue;
                if (value != 0) value -= ZeroDegreeCelsius;
                var intValue = (value / 100);
                var decimalValue = (int)(((value / 100f) - intValue) * 100);
                integerData[index] = (byte)intValue;
                decimalData[index] = (byte)(decimalValue);
                index++;
            }
        }
        integerMat.LogCall(x => x.SetTo(integerData));
        decimalMat.LogCall(x => x.SetTo(decimalData));

        var resultMat = new Mat(rowCount, colCount, DepthType.Cv8U, 3).AsManaged();
        resultMat.LogCall(x => x.SetTo(new MCvScalar(0d, 0d, 0d)));
        var decimalRange = decimalMat.LogCall(x => x.GetValueRange());
        var fullRange = integerMat.LogCall(x => x.GetValueRange());
        var emptyRange = zeroMat.LogCall(x => x.GetValueRange());
        integerMat.LogCall(decimalMat, zeroMat, resultMat, (x1, x2, x3, x4) => CvInvoke.Merge(new VectorOfMat(x1, x2, x3), x4));
        integerMat.LogCall(x => x.Dispose());
        decimalMat.LogCall(x => x.Dispose());
        zeroMat.LogCall(x => x.Dispose());
        if (decimalRange.Max > 100)
        {
            Log.Logger.Error("decimal channel was over 100: {from}-{to}", decimalRange.Min, decimalRange.Max);
            throw new Exception("decimal channel was over 100");
        }
        if (fullRange.Min < 0 || fullRange.Max > 100)
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

    public void ApplyIrColorMap(IManagedMat irImage)
    {
        if (irImage.IsDisposed || irImage == null) return;
        var rowCount = irImage.LogCall(x => x.Rows);
        var colCount = irImage.LogCall(x => x.Cols);
        if (rowCount == 0 || colCount == 0) return;
        var baselineMat = new Mat(rowCount, colCount, irImage.LogCall(x => x.Depth), 1).AsManaged();
        baselineMat.LogCall(x => x.SetTo(new MCvScalar(ZeroDegreeCelsius + 1500)));
        var scaleMat = new Mat(rowCount, colCount, DepthType.Cv32F, 1).AsManaged();
        scaleMat.LogCall(x => x.SetTo(new MCvScalar(1 / 10d)));
        irImage.LogCall(baselineMat, (x, y) => CvInvoke.Subtract(x, y, x));
        irImage.LogCall(x => x.ConvertTo(x, DepthType.Cv32F));
        irImage.LogCall(scaleMat, (x, y) => CvInvoke.Multiply(x, y, x, 1, DepthType.Cv32F));
        irImage.LogCall(x => x.ConvertTo(x, DepthType.Cv8U));
        var inverseMat = new Mat(rowCount, colCount, DepthType.Cv8U, 1).AsManaged();
        inverseMat.LogCall(x => x.SetTo(new MCvScalar(255)));
        inverseMat.LogCall(irImage, (x, y) => CvInvoke.Subtract(x, y, y));
        irImage.LogCall(x => CvInvoke.ApplyColorMap(x, x, ColorMapType.Rainbow));
        baselineMat.LogCall(x => x.Dispose());
        scaleMat.LogCall(x => x.Dispose());
        inverseMat.LogCall(x => x.Dispose());
    }

    public IManagedMat? MatFromFile(string filename, out bool isIr)
    {
        isIr = false;
        if (!filename.EndsWith(CameraType.IR.GetInfo().FileEnding)) return CvInvoke.Imread(filename).AsManaged();
        var irMat = new Mat(ImageConstants.IrHeight, ImageConstants.IrWidth, DepthType.Cv32S, 1).AsManaged();
        var tempArray = File.ReadAllBytes(filename).Chunk(4).Select(b => (int)BitConverter.ToUInt32(b)).ToArray();
        if (tempArray.Length != ImageConstants.IrPixelCount)
        {
            irMat.LogCall(x => x.Dispose());
            return null;
        }
        irMat.LogCall(x => x.SetTo(tempArray));
        isIr = true;
        return irMat;
    }

    public byte[] MatToByteArray(IManagedMat mat, bool disposeMat = true)
    {
        if (mat.IsDisposed || mat.LogCall(x => x.Width) == 0 || mat.LogCall(x => x.Height) == 0) return [];
        var resultFile = Guid.NewGuid().ToString() + ".png";
        var fullPath = Path.Combine(Path.GetTempPath(), resultFile);
        mat.LogCall(x => CvInvoke.Imwrite(fullPath, x));
        if (disposeMat) mat.LogCall(x => x.Dispose());
        var result = File.ReadAllBytes(fullPath);
        File.Delete(fullPath);
        return result;
    }

    public void Resize(IManagedMat mat, int height)
    {
        if (mat.LogCall(x => x.Depth) != DepthType.Cv8U) mat.LogCall(x => x.ConvertTo(x, DepthType.Cv32F));
        mat.LogCall(x => CvInvoke.Resize(x, x, new Size((int)(mat.LogCall(x => x.Width) * height / (float)mat.LogCall(x => x.Height)), height)));
    }

    public (IManagedMat VisImage, IManagedMat? IrImage) CropImages(string visImage, string? irImage, NpgsqlPoint[] visPolygon, NpgsqlPoint irOffset, int scalingHeightInPx)
    {
        var visMat = MatFromFile(visImage, out _) ?? throw new Exception("Could not read vis image file");
        var resizeRatio = scalingHeightInPx / (double)visMat.LogCall(x => x.Height);
        visPolygon = visPolygon.Select(vp => new NpgsqlPoint(vp.X * resizeRatio, vp.Y * resizeRatio)).ToArray();
        irOffset = new NpgsqlPoint(-irOffset.X * scalingHeightInPx / Const.IrScalingHeight, -irOffset.Y * scalingHeightInPx / Const.IrScalingHeight);
        Resize(visMat, scalingHeightInPx);
        var visCrop = CutImage(visPolygon, visMat);
        if (irImage.IsEmpty())
        {
            visMat.LogCall(x => x.Dispose());
            return (visCrop, null);
        }
        var irMat = MatFromFile(irImage!, out _);
        if (irMat == null)
        {
            visMat.LogCall(x => x.Dispose());
            return (visCrop, null);
        }
        Resize(irMat, scalingHeightInPx);
        var irPolygon = visPolygon
            .Select(p => new NpgsqlPoint(p.X + irOffset.X, p.Y + irOffset.Y))
            .ToArray();
        var irCrop = CutIrImage(irPolygon, irMat);
        irCrop = PadIrToVisSize(irPolygon, visCrop, irCrop);
        visMat.LogCall(x => x.Dispose());
        irMat.LogCall(x => x.Dispose());
        return (visCrop, irCrop);
    }

    private static IManagedMat PadIrToVisSize(NpgsqlPoint[] irPolygon, IManagedMat visCrop, IManagedMat irCrop)
    {
        var irHeight = irCrop.LogCall(x => x.Height);
        var irWidth = irCrop.LogCall(x => x.Width);
        var visHeight = visCrop.LogCall(x => x.Height);
        var visWidth = visCrop.LogCall(x => x.Width);
        if (irHeight == visHeight && irWidth == visWidth) return irCrop;
        var result = new Mat(new Size(visWidth, visHeight), irCrop.LogCall(x => x.Depth), irCrop.LogCall(x => x.NumberOfChannels)).AsManaged();
        var padLeftSign = irPolygon.Min(p => p.X) < 0 ? -1 : 1;
        var padTopSign = irPolygon.Min(p => p.Y) < 0 ? -1 : 1;
        var xPadding = Math.Max(0, padLeftSign * (irWidth - visWidth));
        var yPadding = Math.Max(0, padTopSign * (irHeight - visHeight));
        var resultData = new float[visHeight * visWidth];
        var irCropData = irCrop.LogCall(x => x.GetData(true));
        for (var row = 0; row < irHeight; row++)
        {
            for (var col = 0; col < irWidth; col++)
            {
                var index = ((row + yPadding) * visWidth) + col + xPadding;
                var irCropValue = irCropData.GetValue(row, col) as float?;
                resultData[index] = irCropValue ?? 0;
            }
        }
        result.LogCall(x => x.SetTo(resultData));
        irCrop.LogCall(x => x.Dispose());
        return result;
    }

    private static IManagedMat CutIrImage(NpgsqlPoint[] polygon, IManagedMat mat)
    {
        var roi = CalculateInboundRoi(polygon, mat);
        var result = mat.LogCall(x => new Mat(x, roi).AsManaged());
        mat.LogCall(x => x.Dispose());
        return result;
    }

    private static Rectangle CalculateInboundRoi(NpgsqlPoint[] polygon, IManagedMat mat)
    {
        var minPolygonX = Math.Max(0, polygon.Min(p => p.X));
        var minPolygonY = Math.Max(0, polygon.Min(p => p.Y));
        var maxPolygonX = Math.Max(0, polygon.Max(p => p.X));
        var maxPolygonY = Math.Max(0, polygon.Max(p => p.Y));
        var minX = Math.Min(mat.LogCall(x => x.Cols), minPolygonX);
        var minY = Math.Min(mat.LogCall(x => x.Rows), minPolygonY);
        var width = Math.Min(mat.LogCall(x => x.Cols), maxPolygonX) - minX;
        var height = Math.Min(mat.LogCall(x => x.Rows), maxPolygonY) - minY;
        return new Rectangle((int)minX, (int)minY, (int)width, (int)height);
    }

    private static Point CalculateSafePoint(NpgsqlPoint point, IManagedMat mat)
    {
        var x = (int)point.X;
        var y = (int)point.Y;
        var cols = mat.LogCall(x => x.Cols);
        var rows = mat.LogCall(x => x.Rows);
        return new Point(x < 0 ? 0 : x > cols ? cols : x, y < 0 ? 0 : y > rows ? rows : y);
    }

    private static IManagedMat CutImage(NpgsqlPoint[] polygon, IManagedMat mat)
    {
        var roi = CalculateInboundRoi(polygon, mat);
        var cols = mat.LogCall(x => x.Cols);
        var rows = mat.LogCall(x => x.Rows);
        var polygonCrop = new Mat(rows, cols, mat.LogCall(x => x.Depth), mat.LogCall(x => x.NumberOfChannels)).AsManaged();
        var mask = new Mat(rows, cols, DepthType.Cv8U, 1).AsManaged();
        polygonCrop.LogCall(x => x.SetTo(new MCvScalar(0, 0, 0)));
        mask.LogCall(x => x.SetTo(new MCvScalar(0)));
        var cvPolygon = new VectorOfVectorOfPoint(new VectorOfPoint(polygon.Select(p => CalculateSafePoint(p, polygonCrop)).ToArray()));
        mask.LogCall(x => CvInvoke.FillPoly(x, cvPolygon, new MCvScalar(255)));
        mat.LogCall(polygonCrop, mask, (x, y, z) => x.CopyTo(y, z));
        var result = polygonCrop.LogCall(x => new Mat(x, roi).AsManaged());
        polygonCrop.LogCall(x => x.Dispose());
        mask.LogCall(x => x.Dispose());
        return result;
    }
}
