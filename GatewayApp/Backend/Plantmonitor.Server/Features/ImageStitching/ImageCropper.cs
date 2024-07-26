using NpgsqlTypes;
using Emgu.CV;
using System.Drawing;
using Emgu.CV.Util;
using Emgu.CV.CvEnum;

namespace Plantmonitor.Server.Features.ImageStitching;

public class ImageCropper
{
    public Mat CropImage(string imageFile, NpgsqlPoint[] polygon)
    {
        var image = CvInvoke.Imread(imageFile);
        var minX = polygon.Min(p => p.X);
        var minY = polygon.Min(p => p.Y);
        var width = polygon.Max(p => p.X) - minX;
        var height = polygon.Max(p => p.Y) - minY;
        var roi = new Rectangle((int)minX, (int)minY, (int)width, (int)height);
        var polygonCrop = new Mat(image.Rows, image.Cols, image.Depth, image.NumberOfChannels);
        var cvPolygon = new VectorOfVectorOfPoint(new VectorOfPoint(polygon.Select(p => new Point((int)p.X, (int)p.Y)).ToArray()));
        CvInvoke.FillPoly(polygonCrop, cvPolygon, new Emgu.CV.Structure.MCvScalar(255, 255, 255));
        image.CopyTo(polygonCrop, polygonCrop);
        var resultCrop = new Mat(polygonCrop, roi);
        image.Dispose();
        polygonCrop.Dispose();
        return resultCrop;
    }
}
