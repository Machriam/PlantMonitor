using System.Drawing;
using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Features2D;
using Emgu.CV.Stitching;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using static Plantmonitor.Server.Features.ImageStitching.PhotoStitcher;

namespace Plantmonitor.Server.Features.ImageStitching;

public interface IPhotoStitcher
{
    (Mat VisImage, Mat IrColorImage, Mat IrRawData, string MetaDataTable) CreateVirtualImage(IEnumerable<PhotoStitchData> images, int specimenWidth, int specimenHeight, int spacingBetweenSpecimen);
}

public class PhotoStitcher : IPhotoStitcher
{
    private const float DesiredRatio = 16 / 9f;
    public record class PhotoStitchData : IDisposable
    {
        public PhotoStitchData() { }
        public Mat? VisImage { get; set; }
        public Mat? IrImageRawData { get; set; }
        public Mat? ColoredIrImage { get; set; }
        public string Name { get; init; } = "";
        public string Comment { get; init; } = "";

        public void Dispose()
        {
            VisImage?.Dispose();
            ColoredIrImage?.Dispose();
            IrImageRawData?.Dispose();
        }
    }

    public (Mat VisImage, Mat IrColorImage, Mat IrRawData, string MetaDataTable) CreateVirtualImage(IEnumerable<PhotoStitchData> images, int specimenWidth, int specimenHeight, int spacingBetweenSpecimen)
    {
        var length = images.Count();
        var imagesPerRow = (int)float.Round(16 * length / 25f);
        var finalHeight = ((int)(float.Ceiling(length / (float)imagesPerRow)) * (specimenHeight + spacingBetweenSpecimen)) + spacingBetweenSpecimen;
        var finalWidth = (imagesPerRow * (specimenWidth + spacingBetweenSpecimen)) + spacingBetweenSpecimen;
        var visImage = new Mat();
        var irData = new Mat(finalHeight, finalWidth, DepthType.Cv32S, 1);
        var irColorImage = new Mat(finalHeight, finalWidth, DepthType.Cv8U, 3);
        var counter = 0;
        var imageList = images.ToList();
        var horizontalSlices = new List<Mat>();
        for (var row = 0; row < (length / (float)imagesPerRow); row++)
        {
            var concatImages = new List<Mat>();
            for (var column = 0; column < imagesPerRow; column++)
            {
                var index = (row * imagesPerRow) + column;
                if (index >= imageList.Count) break;
                var image = imageList[index];
                var insertPosition = new Rectangle((column * (specimenWidth + spacingBetweenSpecimen)) + spacingBetweenSpecimen,
                    (row * (specimenHeight + spacingBetweenSpecimen)) + spacingBetweenSpecimen,
                    specimenWidth + spacingBetweenSpecimen, specimenHeight + spacingBetweenSpecimen);
                CvInvoke.Imshow($"{column}{row}", image.VisImage);
                var test = new Mat(insertPosition.Size, image.VisImage.Depth, image.VisImage.NumberOfChannels);
                image.VisImage.CopyTo(test);
                concatImages.Add(test);
                counter++;
            }
            var hConcatMat = new Mat();
            CvInvoke.WaitKey();
            CvInvoke.HConcat([.. concatImages], hConcatMat);
            CvInvoke.Imshow(row.ToString(), hConcatMat);
            horizontalSlices.Add(hConcatMat);
        }
        CvInvoke.VConcat([.. horizontalSlices], visImage);
        return (visImage, irColorImage, irData, "");
    }
}
