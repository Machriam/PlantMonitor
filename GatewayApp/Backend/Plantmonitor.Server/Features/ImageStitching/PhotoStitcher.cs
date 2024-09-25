using System.Drawing;
using System.Globalization;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using static Plantmonitor.Server.Features.ImageStitching.PhotoStitcher;

namespace Plantmonitor.Server.Features.ImageStitching;

public interface IPhotoStitcher
{
    (IManagedMat VisImage, IManagedMat IrColorImage, IManagedMat IrRawData, VirtualImageMetaDataModel MetaData) CreateVirtualImage(IEnumerable<PhotoStitchData> images,
        int width, int height, float pixelSizeInMm);

    IManagedMat CreateCombinedImage(List<IManagedMat> sameSizeSubImages);
}

public class PhotoStitcher(ILogger<IPhotoStitcher> logger) : IPhotoStitcher
{
    public const int WhiteBorderSize = 5;
    private const float DesiredRatio = 16 / 9f;
    public record class PhotoStitchData : IDisposable
    {
        public PhotoStitchData() { }
        public IManagedMat? VisImage { get; set; }
        public IManagedMat? IrImageRawData { get; set; }
        public IManagedMat? ColoredIrImage { get; set; }
        public string Name { get; init; } = "";
        public DateTime IrImageTime { get; set; }
        public DateTime VisImageTime { get; set; }
        public int MotorPosition { get; set; }
        public int IrTemperatureInK { get; set; }
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

    public (IManagedMat VisImage, IManagedMat IrColorImage, IManagedMat IrRawData, VirtualImageMetaDataModel MetaData) CreateVirtualImage(IEnumerable<PhotoStitchData> images,
        int width, int height, float pixelSizeInMm)
    {
        var imageList = images.ToList();
        var imagesPerRow = CalculateImagesPerRow(imageList.Count, width, height);
        logger.LogInformation("Concatenating vis images");
        var visImage = ConcatImages(width, height, imagesPerRow, imageList, psd => psd?.VisImage, out var finalMatSize);
        logger.LogInformation("Concatenating ir color images");
        var irColorImage = ConcatImages(width, height, imagesPerRow, imageList, psd => psd?.ColoredIrImage, out _);
        logger.LogInformation("Concatenating ir images");
        var irData = ConcatImages(width, height, imagesPerRow, imageList, psd => psd?.IrImageRawData, out _);
        logger.LogInformation("Creating metadata");
        var metaData = new VirtualImageMetaDataModel()
        {
            Dimensions = new(finalMatSize.Width, finalMatSize.Height, width, height, WhiteBorderSize, WhiteBorderSize, imagesPerRow,
                            (int)float.Ceiling(imageList.Count / (float)imagesPerRow), imageList.Count, "Raw IR in °C, depending on order, first or last channel full degree, middle channel decimal values, third channel zero",
                            pixelSizeInMm),
            ImageMetaData = imageList.WithIndex().Select(im => new VirtualImageMetaDataModel.ImageMetaDatum(im.Index, im.Item.Name, im.Item.Comment,
                            im.Item.ColoredIrImage != null, im.Item.VisImage != null, im.Item.IrImageTime, im.Item.VisImageTime,
                            im.Item.IrTemperatureInK, im.Item.MotorPosition)).ToArray()
        };
        return (visImage, irColorImage, irData, metaData);
    }

    public IManagedMat CreateCombinedImage(List<IManagedMat> sameSizeSubImages)
    {
        if (sameSizeSubImages.Count == 0) return new Mat().AsManaged();
        var imagesPerRow = CalculateImagesPerRow(sameSizeSubImages.Count, sameSizeSubImages[0].Execute(x => x.Width), sameSizeSubImages[0].Execute(x => x.Height));
        var result = new Mat().AsManaged();
        var horizontalSlices = new List<IManagedMat>();
        var emptyMat = new Mat(sameSizeSubImages[0].Execute(x => x.Size), sameSizeSubImages[0].Execute(x => x.Depth), sameSizeSubImages[0].Execute(x => x.NumberOfChannels)).AsManaged();
        emptyMat.Execute(x => x.SetTo(new MCvScalar(0)));
        for (var row = 0; row < (sameSizeSubImages.Count / (float)imagesPerRow); row++)
        {
            var concatImages = new List<IManagedMat>();
            for (var column = 0; column < imagesPerRow; column++)
            {
                var index = (row * imagesPerRow) + column;
                if (sameSizeSubImages.Count <= index)
                {
                    concatImages.Add(emptyMat.Execute(x => x.Clone().AsManaged()));
                    continue;
                }
                concatImages.Add(sameSizeSubImages[index]);
            }
            var hConcatMat = new Mat().AsManaged();
            hConcatMat.Execute(concatImages, (x, y) => CvInvoke.HConcat([.. y], x));
            horizontalSlices.Add(hConcatMat);
            foreach (var image in concatImages) image.Dispose();
        }
        result.Execute(horizontalSlices, (x, y) => CvInvoke.VConcat([.. y], x));
        foreach (var slice in horizontalSlices) slice.Dispose();
        emptyMat.Dispose();
        return result;
    }

    private static IManagedMat ConcatImages(int width, int height, int imagesPerRow, IList<PhotoStitchData> images, Func<PhotoStitchData?, IManagedMat?> selector, out Size finalMatSize)
    {
        finalMatSize = new Size(width, height);
        var length = images.Count;
        const DepthType Depth = DepthType.Cv8U;
        const int Channels = 3;
        var result = new Mat().AsManaged();
        var size = new Size(width, height);
        var emptyMat = new Mat(size, Depth, Channels).AsManaged();
        emptyMat.Execute(x => x.SetTo(new MCvScalar(0)));
        var horizontalSlices = new List<IManagedMat>();
        for (var row = 0; row < (length / (float)imagesPerRow); row++)
        {
            var concatImages = new List<IManagedMat>();
            for (var column = 0; column < imagesPerRow; column++)
            {
                var index = (row * imagesPerRow) + column;
                IManagedMat mat;
                var outOfBounds = index >= images.Count || selector(images[index]) == null;
                if (outOfBounds) mat = emptyMat.Execute(x => x.Clone().AsManaged());
                else mat = selector(images[index])!;
                var bottomPadding = size.Height - mat.Execute(x => x.Rows);
                var rightPadding = size.Width - mat.Execute(x => x.Cols);
                var refHeight = size.Height;
                var textSize = CvInvoke.GetTextSize("test", FontFace.HersheySimplex, 2d, 3, ref refHeight);
                mat.Execute(x => CvInvoke.CopyMakeBorder(x, x, 0, bottomPadding + textSize.Height + WhiteBorderSize, 0, rightPadding, BorderType.Constant, new MCvScalar(0d, 0d, 0d)));
                mat.Execute(x => CvInvoke.CopyMakeBorder(x, x, WhiteBorderSize, WhiteBorderSize, WhiteBorderSize, WhiteBorderSize, BorderType.Constant, new MCvScalar(255d, 255d, 255d)));
                if (index < images.Count)
                {
                    mat.Execute(x => CvInvoke.PutText(x, images[index].Name, new Point(WhiteBorderSize,
                        x.Height - (WhiteBorderSize * 2)), FontFace.HersheySimplex, 2d, new MCvScalar(255d, 255d, 255d), thickness: 3));
                }
                finalMatSize = mat.Execute(x => x.Size);
                concatImages.Add(mat);
            }
            var hConcatMat = new Mat().AsManaged();
            hConcatMat.Execute(concatImages, (x, y) => CvInvoke.HConcat([.. y], x));
            horizontalSlices.Add(hConcatMat);
            foreach (var image in concatImages) image.Dispose();
        }
        result.Execute(horizontalSlices, (x, y) => CvInvoke.VConcat([.. y], x));
        foreach (var slice in horizontalSlices) slice.Dispose();
        emptyMat.Dispose();
        return result;
    }
}
