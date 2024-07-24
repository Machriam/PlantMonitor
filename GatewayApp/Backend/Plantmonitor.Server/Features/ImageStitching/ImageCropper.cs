using NpgsqlTypes;
using Emgu.CV;
using System.Drawing;

namespace Plantmonitor.Server.Features.ImageStitching;

public class ImageCropper
{
    public void CropImage(string imageFile, NpgsqlPoint[] polygon)
    {
        var image = CvInvoke.Imread(imageFile);
        var minX = polygon.Min(p => p.X);
        var minY = polygon.Min(p => p.Y);
        var width = polygon.Max(p => p.X) - minX;
        var height = minY - polygon.Max(p => p.Y);
        var roi = new Rectangle((int)minX, (int)minY, (int)width, (int)height);
        var crop = new Mat(image, roi);
        CvInvoke.Imshow("bla", image);
        CvInvoke.WaitKey(1000);
        image.Dispose();
    }
}
