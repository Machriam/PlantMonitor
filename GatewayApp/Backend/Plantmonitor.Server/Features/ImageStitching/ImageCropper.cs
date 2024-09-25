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
        var data = irImage.Execute(x => x.GetData(true));
        var integerMat = new Mat(irImage.Execute(x => x.Rows), irImage.Execute(x => x.Cols), DepthType.Cv8U, 1).AsManaged();
        integerMat.Execute(x => x.SetTo(new MCvScalar(0)));
        var zeroMat = new Mat(irImage.Execute(x => x.Rows), irImage.Execute(x => x.Cols), DepthType.Cv8U, 1).AsManaged();
        zeroMat.Execute(x => x.SetTo(new MCvScalar(0)));
        var decimalMat = new Mat(irImage.Execute(x => x.Rows), irImage.Execute(x => x.Cols), DepthType.Cv8U, 1).AsManaged();
        decimalMat.Execute(x => x.SetTo(new MCvScalar(0d)));
        var rowCount = irImage.Execute(x => x.Rows);
        var colCount = irImage.Execute(x => x.Cols);
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
        integerMat.Execute(x => x.SetTo(integerData));
        decimalMat.Execute(x => x.SetTo(decimalData));

        var resultMat = new Mat(rowCount, colCount, DepthType.Cv8U, 3).AsManaged();
        resultMat.Execute(x => x.SetTo(new MCvScalar(0d, 0d, 0d)));
        var decimalRange = decimalMat.Execute(x => x.GetValueRange());
        var fullRange = integerMat.Execute(x => x.GetValueRange());
        var emptyRange = zeroMat.Execute(x => x.GetValueRange());
        integerMat.Execute(decimalMat, zeroMat, resultMat, (x1, x2, x3, x4) => CvInvoke.Merge(new VectorOfMat(x1, x2, x3), x4));
        integerMat.Execute(x => x.Dispose());
        decimalMat.Execute(x => x.Dispose());
        zeroMat.Execute(x => x.Dispose());
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
        var rowCount = irImage.Execute(x => x.Rows);
        var colCount = irImage.Execute(x => x.Cols);
        if (rowCount == 0 || colCount == 0) return;
        var baselineMat = new Mat(rowCount, colCount, irImage.Execute(x => x.Depth), 1).AsManaged();
        baselineMat.Execute(x => x.SetTo(new MCvScalar(ZeroDegreeCelsius + 1500)));
        var scaleMat = new Mat(rowCount, colCount, DepthType.Cv32F, 1).AsManaged();
        scaleMat.Execute(x => x.SetTo(new MCvScalar(1 / 10d)));
        irImage.Execute(baselineMat, (x, y) => CvInvoke.Subtract(x, y, x));
        irImage.Execute(x => x.ConvertTo(x, DepthType.Cv32F));
        irImage.Execute(scaleMat, (x, y) => CvInvoke.Multiply(x, y, x, 1, DepthType.Cv32F));
        irImage.Execute(x => x.ConvertTo(x, DepthType.Cv8U));
        var inverseMat = new Mat(rowCount, colCount, DepthType.Cv8U, 1).AsManaged();
        inverseMat.Execute(x => x.SetTo(new MCvScalar(255)));
        inverseMat.Execute(irImage, (x, y) => CvInvoke.Subtract(x, y, y));
        irImage.Execute(x => CvInvoke.ApplyColorMap(x, x, ColorMapType.Rainbow));
        baselineMat.Execute(x => x.Dispose());
        scaleMat.Execute(x => x.Dispose());
        inverseMat.Execute(x => x.Dispose());
    }

    public IManagedMat? MatFromFile(string filename, out bool isIr)
    {
        isIr = false;
        if (!filename.EndsWith(CameraType.IR.GetInfo().FileEnding)) return CvInvoke.Imread(filename).AsManaged();
        var irMat = new Mat(ImageConstants.IrHeight, ImageConstants.IrWidth, DepthType.Cv32S, 1).AsManaged();
        var tempArray = File.ReadAllBytes(filename).Chunk(4).Select(b => (int)BitConverter.ToUInt32(b)).ToArray();
        if (tempArray.Length != ImageConstants.IrPixelCount)
        {
            irMat.Execute(x => x.Dispose());
            return null;
        }
        irMat.Execute(x => x.SetTo(tempArray));
        isIr = true;
        return irMat;
    }

    public byte[] MatToByteArray(IManagedMat mat, bool disposeMat = true)
    {
        if (mat.IsDisposed || mat.Execute(x => x.Width) == 0 || mat.Execute(x => x.Height) == 0) return [];
        var resultFile = Guid.NewGuid().ToString() + ".png";
        var fullPath = Path.Combine(Path.GetTempPath(), resultFile);
        mat.Execute(x => CvInvoke.Imwrite(fullPath, x));
        if (disposeMat) mat.Execute(x => x.Dispose());
        var result = File.ReadAllBytes(fullPath);
        File.Delete(fullPath);
        return result;
    }

    public void Resize(IManagedMat mat, int height)
    {
        if (mat.Execute(x => x.Depth) != DepthType.Cv8U) mat.Execute(x => x.ConvertTo(x, DepthType.Cv32F));
        mat.Execute(x => CvInvoke.Resize(x, x, new Size((int)(mat.Execute(x => x.Width) * height / (float)mat.Execute(x => x.Height)), height)));
    }

    public (IManagedMat VisImage, IManagedMat? IrImage) CropImages(string visImage, string? irImage, NpgsqlPoint[] visPolygon, NpgsqlPoint irOffset, int scalingHeightInPx)
    {
        var visMat = MatFromFile(visImage, out _) ?? throw new Exception("Could not read vis image file");
        var resizeRatio = scalingHeightInPx / (double)visMat.Execute(x => x.Height);
        visPolygon = visPolygon.Select(vp => new NpgsqlPoint(vp.X * resizeRatio, vp.Y * resizeRatio)).ToArray();
        irOffset = new NpgsqlPoint(-irOffset.X * scalingHeightInPx / Const.IrScalingHeight, -irOffset.Y * scalingHeightInPx / Const.IrScalingHeight);
        Resize(visMat, scalingHeightInPx);
        var visCrop = CutImage(visPolygon, visMat);
        if (irImage.IsEmpty())
        {
            visMat.Execute(x => x.Dispose());
            return (visCrop, null);
        }
        var irMat = MatFromFile(irImage!, out _);
        if (irMat == null)
        {
            visMat.Execute(x => x.Dispose());
            return (visCrop, null);
        }
        Resize(irMat, scalingHeightInPx);
        var irPolygon = visPolygon
            .Select(p => new NpgsqlPoint(p.X + irOffset.X, p.Y + irOffset.Y))
            .ToArray();
        var irCrop = CutIrImage(irPolygon, irMat);
        irCrop = PadIrToVisSize(irPolygon, visCrop, irCrop);
        visMat.Execute(x => x.Dispose());
        irMat.Execute(x => x.Dispose());
        return (visCrop, irCrop);
    }

    private static IManagedMat PadIrToVisSize(NpgsqlPoint[] irPolygon, IManagedMat visCrop, IManagedMat irCrop)
    {
        var irHeight = irCrop.Execute(x => x.Height);
        var irWidth = irCrop.Execute(x => x.Width);
        var visHeight = visCrop.Execute(x => x.Height);
        var visWidth = visCrop.Execute(x => x.Width);
        if (irHeight == visHeight && irWidth == visWidth) return irCrop;
        var result = new Mat(new Size(visWidth, visHeight), irCrop.Execute(x => x.Depth), irCrop.Execute(x => x.NumberOfChannels)).AsManaged();
        var padLeftSign = irPolygon.Min(p => p.X) < 0 ? -1 : 1;
        var padTopSign = irPolygon.Min(p => p.Y) < 0 ? -1 : 1;
        var xPadding = Math.Max(0, padLeftSign * (irWidth - visWidth));
        var yPadding = Math.Max(0, padTopSign * (irHeight - visHeight));
        var resultData = new float[visHeight * visWidth];
        var irCropData = irCrop.Execute(x => x.GetData(true));
        for (var row = 0; row < irHeight; row++)
        {
            for (var col = 0; col < irWidth; col++)
            {
                var index = ((row + yPadding) * visWidth) + col + xPadding;
                var irCropValue = irCropData.GetValue(row, col) as float?;
                resultData[index] = irCropValue ?? 0;
            }
        }
        result.Execute(x => x.SetTo(resultData));
        irCrop.Execute(x => x.Dispose());
        return result;
    }

    private static IManagedMat CutIrImage(NpgsqlPoint[] polygon, IManagedMat mat)
    {
        var roi = CalculateInboundRoi(polygon, mat);
        var result = mat.Execute(x => new Mat(x, roi).AsManaged());
        mat.Execute(x => x.Dispose());
        return result;
    }

    private static Rectangle CalculateInboundRoi(NpgsqlPoint[] polygon, IManagedMat mat)
    {
        var minPolygonX = Math.Max(0, polygon.Min(p => p.X));
        var minPolygonY = Math.Max(0, polygon.Min(p => p.Y));
        var maxPolygonX = Math.Max(0, polygon.Max(p => p.X));
        var maxPolygonY = Math.Max(0, polygon.Max(p => p.Y));
        var minX = Math.Min(mat.Execute(x => x.Cols), minPolygonX);
        var minY = Math.Min(mat.Execute(x => x.Rows), minPolygonY);
        var width = Math.Min(mat.Execute(x => x.Cols), maxPolygonX) - minX;
        var height = Math.Min(mat.Execute(x => x.Rows), maxPolygonY) - minY;
        return new Rectangle((int)minX, (int)minY, (int)width, (int)height);
    }

    private static Point CalculateSafePoint(NpgsqlPoint point, IManagedMat mat)
    {
        var x = (int)point.X;
        var y = (int)point.Y;
        var cols = mat.Execute(x => x.Cols);
        var rows = mat.Execute(x => x.Rows);
        return new Point(x < 0 ? 0 : x > cols ? cols : x, y < 0 ? 0 : y > rows ? rows : y);
    }

    private static IManagedMat CutImage(NpgsqlPoint[] polygon, IManagedMat mat)
    {
        var roi = CalculateInboundRoi(polygon, mat);
        var cols = mat.Execute(x => x.Cols);
        var rows = mat.Execute(x => x.Rows);
        var polygonCrop = new Mat(rows, cols, mat.Execute(x => x.Depth), mat.Execute(x => x.NumberOfChannels)).AsManaged();
        var mask = new Mat(rows, cols, DepthType.Cv8U, 1).AsManaged();
        polygonCrop.Execute(x => x.SetTo(new MCvScalar(0, 0, 0)));
        mask.Execute(x => x.SetTo(new MCvScalar(0)));
        var cvPolygon = new VectorOfVectorOfPoint(new VectorOfPoint(polygon.Select(p => CalculateSafePoint(p, polygonCrop)).ToArray()));
        mask.Execute(x => CvInvoke.FillPoly(x, cvPolygon, new MCvScalar(255)));
        mat.Execute(polygonCrop, mask, (x, y, z) => x.CopyTo(y, z));
        var result = polygonCrop.Execute(x => new Mat(x, roi).AsManaged());
        polygonCrop.Execute(x => x.Dispose());
        mask.Execute(x => x.Dispose());
        return result;
    }
}
