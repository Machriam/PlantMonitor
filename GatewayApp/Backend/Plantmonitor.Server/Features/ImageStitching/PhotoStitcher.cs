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

    public int CalculateImagesPerRow(int length, int width, int height)
    {
        if (length <= 1) return 1;
        var imagesPerRow = Enumerable.Range(1, length).Select(columns =>
        {
            var rows = float.Ceiling(length / (float)columns);
            return (Ratio: Math.Abs(DesiredRatio - (columns * width / (height * rows))), Columns: columns);
        });
        return imagesPerRow.MinBy(ipr => ipr.Ratio).Columns;
    }

    public (Mat VisImage, Mat IrColorImage, Mat IrRawData, string MetaDataTable) CreateVirtualImage(IEnumerable<PhotoStitchData> images,
        int specimenWidth, int specimenHeight, int spacingBetweenSpecimen)
    {
        var imageList = images.ToList();
        var height = specimenHeight + spacingBetweenSpecimen;
        var width = specimenWidth + spacingBetweenSpecimen;
        var imagesPerRow = CalculateImagesPerRow(imageList.Count, width, height);
        var visImage = ConcatImages(width, height, imagesPerRow, imageList, psd => psd?.VisImage);
        var irColorImage = ConcatImages(width, height, imagesPerRow, imageList, psd => psd?.ColoredIrImage);
        var irData = ConcatImages(width, height, imagesPerRow, imageList, psd => psd?.IrImageRawData);
        var metaDataHeader = new string[] { "Image Height", "Image Width", "Spacing after Image", "Images per Row", "Row Count", "Image Count", "Comment" };
        var metaDataInfo = new object[] { specimenHeight, specimenWidth, spacingBetweenSpecimen, imagesPerRow,
            (int)float.Ceiling(imageList.Count / (float)imagesPerRow), imageList.Count,"Raw IR in °C, first channel full degree, second channel decimal values" }
        .Select(md => md.ToString())
        .ToList();
        var dataHeader = new string[] { "Index", "Name", "Comment" };
        var data = imageList.WithIndex()
        .Select(im => new string[] { im.Index.ToString(), im.Item.Name, im.Item.Comment }.Concat("\t"))
        .Concat("\n");
        var metaDataTsv = new List<string>() { metaDataHeader.Concat("\t") }
            .Append(metaDataInfo.Concat("\t"))
            .Append(dataHeader.Concat("\t"))
            .Append(data);
        return (visImage, irColorImage, irData, metaDataTsv.Concat("\n"));
    }

    private static Mat ConcatImages(int width, int height, int imagesPerRow, IList<PhotoStitchData> images, Func<PhotoStitchData?, Mat?> selector)
    {
        var length = images.Count;
        var firstMat = selector(images.FirstOrDefault());
        if (firstMat == null) return new Mat((int)(height * float.Ceiling(images.Count / (float)imagesPerRow)), imagesPerRow * width, DepthType.Cv8U, 3);
        var depth = firstMat.Depth;
        var channels = firstMat.NumberOfChannels;
        var result = new Mat();
        var size = new Size(width, height);
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
