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
        var result = new Mat();
        foreach (var image in images)
        {
        }
        return result;
    }
}
