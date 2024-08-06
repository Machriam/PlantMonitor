using System.Drawing;
using System.Globalization;
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
    (Mat VisImage, Mat IrColorImage, Mat IrRawData, string MetaDataTable) CreateVirtualImage(IEnumerable<PhotoStitchData> images, int width, int height);
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
        public DateTime IrImageTime { get; set; }
        public DateTime VisImageTime { get; set; }
        public float IrTemperatureInK { get; set; }
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
        int width, int height)
    {
        var imageList = images.ToList();
        var imagesPerRow = CalculateImagesPerRow(imageList.Count, width, height);
        var visImage = ConcatImages(width, height, imagesPerRow, imageList, psd => psd?.VisImage, out var finalMatSize);
        var irColorImage = ConcatImages(width, height, imagesPerRow, imageList, psd => psd?.ColoredIrImage, out _);
        var irData = ConcatImages(width, height, imagesPerRow, imageList, psd => psd?.IrImageRawData, out _);
        var metaDataHeader = new string[] { "Image Height", "Image Width", "Spacing after Image", "Images per Row", "Row Count", "Image Count", "Comment" };
        var metaDataInfo = new object[] { finalMatSize.Height, finalMatSize.Width, imagesPerRow,
            (int)float.Ceiling(imageList.Count / (float)imagesPerRow), imageList.Count,"Raw IR in °C, first channel full degree, second channel decimal values" }
        .Select(md => md.ToString())
        .ToList();
        var dataHeader = new string[] { "Index", "Name", "Comment", "Has IR", "Has VIS", "IR Time", "Vis Time", "IR Temp" };
        var data = imageList.WithIndex()
        .Select(im => new string[] { im.Index.ToString(), im.Item.Name, im.Item.Comment,
            im.Item.ColoredIrImage == null ? "false" : "true", im.Item.VisImage == null ? "false" : "true",
            im.Item.IrImageTime.ToString("yyyy.MM.dd_HH:mm:ss",CultureInfo.InvariantCulture),
            im.Item.VisImageTime.ToString("yyyy.MM.dd_HH:mm:ss",CultureInfo.InvariantCulture),
            (im.Item.IrTemperatureInK/100f).ToString("0.00 K",CultureInfo.InvariantCulture),
        }.Concat("\t"))
        .Concat("\n");
        var metaDataTsv = new List<string>() { metaDataHeader.Concat("\t") }
            .Append(metaDataInfo.Concat("\t"))
            .Append($"\n{dataHeader.Concat("\t")}")
            .Append(data);
        return (visImage, irColorImage, irData, metaDataTsv.Concat("\n"));
    }

    private static Mat ConcatImages(int width, int height, int imagesPerRow, IList<PhotoStitchData> images, Func<PhotoStitchData?, Mat?> selector, out Size finalMatSize)
    {
        finalMatSize = new Size(width, height);
        var length = images.Count;
        const DepthType Depth = DepthType.Cv8U;
        const int Channels = 3;
        var result = new Mat();
        var size = new Size(width, height);
        var emptyMat = new Mat(size, Depth, Channels);
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
                var bottomPadding = size.Height - mat.Rows;
                var rightPadding = size.Width - mat.Cols;
                const int WhiteBorderSize = 5;
                var refHeight = size.Height;
                var textSize = CvInvoke.GetTextSize("test", FontFace.HersheySimplex, 2d, 3, ref refHeight);
                CvInvoke.CopyMakeBorder(mat, mat, 0, bottomPadding + textSize.Height + WhiteBorderSize, 0, rightPadding, BorderType.Constant, new MCvScalar(0d, 0d, 0d));
                CvInvoke.CopyMakeBorder(mat, mat, WhiteBorderSize, WhiteBorderSize, WhiteBorderSize, WhiteBorderSize, BorderType.Constant, new MCvScalar(255d, 255d, 255d));
                if (index < images.Count)
                {
                    CvInvoke.PutText(mat, images[index].Name, new Point(WhiteBorderSize, mat.Height - (WhiteBorderSize * 2)), FontFace.HersheySimplex, 2d, new MCvScalar(255d, 255d, 255d), thickness: 3);
                }
                finalMatSize = new Size(mat.Width, mat.Height);
                concatImages.Add(mat);
            }
            var hConcatMat = new Mat();
            CvInvoke.HConcat([.. concatImages], hConcatMat);
            horizontalSlices.Add(hConcatMat);
            foreach (var image in concatImages) image.Dispose();
        }
        CvInvoke.VConcat([.. horizontalSlices], result);
        foreach (var slice in horizontalSlices) slice.Dispose();
        emptyMat.Dispose();
        return result;
    }
}
