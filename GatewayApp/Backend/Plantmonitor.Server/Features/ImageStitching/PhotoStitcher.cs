using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Stitching;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace Plantmonitor.Server.Features.ImageStitching;

public interface IPhotoStitcher
{
    Mat CreateVirtualImage(IEnumerable<PhotoStitcher.PhotoStitchData> images, float width, float height, float spacing);
}

public class PhotoStitcher : IPhotoStitcher
{
    private const float DesiredRatio = 16 / 9f;
    public record class PhotoStitchData : IDisposable
    {
        public PhotoStitchData() { }
        public Mat? VisImage { get; set; }
        public Mat? IrImage { get; set; }
        public string Name { get; init; } = "";
        public string Comment { get; init; } = "";

        public void Dispose()
        {
            VisImage?.Dispose();
            IrImage?.Dispose();
        }
    }

    public Mat CreateVirtualImage(IEnumerable<PhotoStitchData> images, float width, float height, float spacing)
    {
        var length = images.Count();
        var imagesPerRow = (int)(DesiredRatio / ((width + spacing) / (height + spacing)) * length);
        var imagesPerColumn = (int)float.Ceiling(length / (float)imagesPerRow);
        var result = new Mat();
        var finalHeight = (int)(imagesPerColumn * height);
        var finalWidth = (int)(imagesPerRow * height);
        var visImage = new Mat(finalHeight, finalWidth, DepthType.Cv8U, 3);
        var irImage = new Mat(finalHeight, finalWidth, DepthType.Cv32S, 1);
        foreach (var image in images)
        {
        }
        result.PushBack(visImage);
        result.PushBack(irImage);
        visImage.Dispose();
        irImage.Dispose();
        return result;
    }
}
