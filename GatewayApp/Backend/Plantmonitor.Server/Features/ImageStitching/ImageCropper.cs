using NpgsqlTypes;
using Emgu.CV;
using System.Drawing;
using Emgu.CV.Util;

namespace Plantmonitor.Server.Features.ImageStitching;

public interface IImageCropper
{
    (Mat VisImage, Mat IrImage) CropImages(string visImage, string? irImage, NpgsqlPoint[] visPolygon, NpgsqlPoint irOffset);
}

public class ImageCropper : IImageCropper
{
    public (Mat VisImage, Mat IrImage) CropImages(string visImage, string? irImage, NpgsqlPoint[] visPolygon, NpgsqlPoint irOffset)
    {
        var irMat = new Mat(120, 160, Emgu.CV.CvEnum.DepthType.Cv32S, 1);
        if (!irImage.IsEmpty())
        {
            var tempArray = File.ReadAllBytes(irImage!).Chunk(4).Select(b => (int)BitConverter.ToUInt32(b)).ToArray();
            irMat.SetTo(tempArray);
        }
        var visMat = CvInvoke.Imread(visImage);
        var irPolygon = visPolygon
            .Select(p => new NpgsqlPoint((p.X + irOffset.X) / visMat.Cols * irMat.Cols, (p.Y + irOffset.Y) / visMat.Rows * irMat.Rows))
            .ToArray();

        var visCrop = CutImage(visPolygon, visMat);
        var irCrop = CutIrImage(irPolygon, irMat);
        var scaledIrMat = new Mat(visCrop.Rows, visCrop.Cols, Emgu.CV.CvEnum.DepthType.Cv32S, 1);
        var rowScaling = 1f / (visCrop.Rows / (float)irCrop.Rows);
        var columnScaling = 1f / (visCrop.Cols / (float)irCrop.Cols);
        var data = new List<int>();
        var irCropArray = irCrop.GetData(true);
        for (var row = 0; row < visCrop.Rows; row++)
        {
            for (var col = 0; col < visCrop.Cols; col++)
            {
                data.Add((int)irCropArray.GetValue((int)(row * rowScaling), (int)(col * columnScaling))!);
            }
        }
        scaledIrMat.SetTo(data.ToArray());
        visMat.Dispose();
        return (visCrop, scaledIrMat);
    }

    private Mat CutIrImage(NpgsqlPoint[] polygon, Mat mat)
    {
        var minX = polygon.Min(p => p.X);
        var minY = polygon.Min(p => p.Y);
        var width = polygon.Max(p => p.X) - minX;
        var height = polygon.Max(p => p.Y) - minY;
        var roi = new Rectangle((int)minX, (int)minY, (int)width, (int)height);
        return new Mat(mat, roi);
    }

    private Mat CutImage(NpgsqlPoint[] polygon, Mat mat)
    {
        var minX = polygon.Min(p => p.X);
        var minY = polygon.Min(p => p.Y);
        var width = polygon.Max(p => p.X) - minX;
        var height = polygon.Max(p => p.Y) - minY;
        var roi = new Rectangle((int)minX, (int)minY, (int)width, (int)height);
        var polygonCrop = new Mat(mat.Rows, mat.Cols, mat.Depth, mat.NumberOfChannels);
        var cvPolygon = new VectorOfVectorOfPoint(new VectorOfPoint(polygon.Select(p => new Point((int)p.X, (int)p.Y)).ToArray()));
        CvInvoke.FillPoly(polygonCrop, cvPolygon, new Emgu.CV.Structure.MCvScalar(255, 255, 255));
        mat.CopyTo(polygonCrop, polygonCrop);
        var result = new Mat(polygonCrop, roi);
        polygonCrop.Dispose();
        return result;
    }
}
