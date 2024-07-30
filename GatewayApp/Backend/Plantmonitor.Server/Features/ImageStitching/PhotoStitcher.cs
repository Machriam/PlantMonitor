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

    public (Mat VisImage, Mat IrColorImage, Mat IrRawData, string MetaDataTable) CreateVirtualImage(IEnumerable<PhotoStitchData> images,
        int specimenWidth, int specimenHeight, int spacingBetweenSpecimen)
    {
        var length = images.Count();
        var imagesPerRow = (int)float.Round(16 * length / 25f);
        var imageList = images.ToList();
        var visImage = ConcatImages(specimenWidth, specimenHeight, spacingBetweenSpecimen, imagesPerRow, imageList, psd => psd?.VisImage);
        var irColorImage = ConcatImages(specimenWidth, specimenHeight, spacingBetweenSpecimen, imagesPerRow, imageList, psd => psd?.ColoredIrImage);
        var irData = ConcatImages(specimenWidth, specimenHeight, spacingBetweenSpecimen, imagesPerRow, imageList, psd => psd?.IrImageRawData);
        return (visImage, irColorImage, irData, "");
    }

    private static Mat ConcatImages(int specimenWidth, int specimenHeight, int spacingBetweenSpecimen, int imagesPerRow,
        IList<PhotoStitchData> images, Func<PhotoStitchData?, Mat?> selector)
    {
        var length = images.Count;
        var firstMat = selector(images.FirstOrDefault());
        if (firstMat == null) return new Mat();
        var depth = firstMat.Depth;
        var channels = firstMat.NumberOfChannels;
        var finalHeight = ((int)(float.Ceiling(length / (float)imagesPerRow)) * (specimenHeight + spacingBetweenSpecimen)) + spacingBetweenSpecimen;
        var finalWidth = (imagesPerRow * (specimenWidth + spacingBetweenSpecimen)) + spacingBetweenSpecimen;
        var result = new Mat(finalHeight, finalWidth, depth, channels);
        var size = new Size(specimenWidth + spacingBetweenSpecimen, specimenHeight + spacingBetweenSpecimen);
        var emptyMat = new Mat(size, depth, channels);
        emptyMat.SetTo(new MCvScalar(0));
        var horizontalSlices = new List<Mat>();
        for (var row = 0; row < (length / (float)imagesPerRow); row++)
        {
            var concatImages = new List<Mat>();
            for (var column = 0; column < imagesPerRow; column++)
            {
                var index = (row * imagesPerRow) + column;
                Mat mat;
                var outOfBounds = index >= images.Count || selector(images[index]) == null;
                if (outOfBounds) mat = emptyMat.Clone();
                else mat = selector(images[index])!;
                CvInvoke.CopyMakeBorder(mat, mat, 0, size.Height - mat.Rows, 0,
                    size.Width - mat.Cols, BorderType.Constant, new MCvScalar(0d, 0d, 0d));
                concatImages.Add(mat);
            }
            var hConcatMat = new Mat();
            CvInvoke.HConcat([.. concatImages], hConcatMat);
            horizontalSlices.Add(hConcatMat);
            foreach (var image in concatImages) image.Dispose();
        }
        CvInvoke.VConcat([.. horizontalSlices], result);
        foreach (var slice in horizontalSlices) slice.Dispose();
        firstMat.Dispose();
        emptyMat.Dispose();
        return result;
    }
}
