using Emgu.CV;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using NpgsqlTypes;
using NSubstitute;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.ImageStitching;
using System;
using Xunit;

namespace Plantmonitor.Server.Tests.Features.ImageStitching;

public class PhotoStitcherTests
{
    private static readonly NpgsqlPoint[] s_singlePlantBottomMiddlePolygon_1WeekLaterAndMoved = [
    new NpgsqlPoint(1261.6227045075125,928.9081803005008),
        new NpgsqlPoint(967.372287145242,913.5225375626043),
        new NpgsqlPoint(967.372287145242,711.5859766277129),
        new NpgsqlPoint(1132.7679465776293,615.4257095158598),
        new NpgsqlPoint(1246.237061769616,669.2754590984974),
        new NpgsqlPoint(1280.8547579298831,853.9031719532554),
        new NpgsqlPoint(1261.6227045075125,928.9081803005008)
        ];

    private static readonly NpgsqlPoint[] s_singlePlantBottomMiddlePolygon = [
        new NpgsqlPoint(1140.4607679465776,932.7545909849749),
        new NpgsqlPoint(915.4457429048414,907.7529215358932),
        new NpgsqlPoint(936.601001669449,688.5075125208681),
        new NpgsqlPoint(1078.9181969949916,603.8864774624374),
        new NpgsqlPoint(1202.0033388981635,676.9682804674458),
        new NpgsqlPoint(1205.8497495826377,876.9816360601002),
        new NpgsqlPoint(1140.4607679465776,932.7545909849749)
        ];

    public PhotoStitcherTests()
    {
    }

    private PhotoStitcher CreatePhotoStitcher()
    {
        return new PhotoStitcher();
    }

    [Fact]
    public void CreateVirtualImage_ShouldWork()
    {
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFile2 = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-33-19-047_-6000_29710.rawir";
        var visFile2 = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-26-10-207_-6000_0.jpg";
        var irFile1 = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-22_12-59-14-908_-6000_29801.rawir";
        var visFile1 = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-22_12-59-14-748_-6000_0.jpg";
        var cropper = new ImageCropper();
        var result1 = cropper.CropImages(visFile1, irFile1, s_singlePlantBottomMiddlePolygon, new(121, 39), 960);
        var color1 = result1.IrImage!.Clone();
        cropper.ApplyIrColorMap(color1);
        var result2 = cropper.CropImages(visFile2, irFile2, s_singlePlantBottomMiddlePolygon_1WeekLaterAndMoved, new(121, 39), 960);
        var color2 = result2.IrImage!.Clone();
        cropper.ApplyIrColorMap(color2);
        var sut = CreatePhotoStitcher();
        var images = Enumerable.Range(0, 10).Select(i => new PhotoStitcher.PhotoStitchData()
        {
            ColoredIrImage = i % 2 == 0 ? color1.Clone() : color2.Clone(),
            Comment = "Comment",
            IrImageRawData = i % 2 == 0 ? result1.IrImage.Clone() : result2.IrImage.Clone(),
            Name = "Name",
            VisImage = i % 2 == 0 ? result1.VisImage.Clone() : result2.VisImage.Clone(),
        });
        var result = sut.CreateVirtualImage(images, 300, 500, 100);
        CvInvoke.Imshow("Virtual VIS", result.VisImage);
        CvInvoke.Imshow("Virtual IR", result.IrColorImage);
        cropper.ApplyIrColorMap(result.IrRawData);
        CvInvoke.Imshow("Virtual Raw", result.IrRawData);
        CvInvoke.WaitKey();
        result1.IrImage.Dispose();
        result1.VisImage.Dispose();
        result2.IrImage.Dispose();
        result2.VisImage.Dispose();
        color1.Dispose();
        color2.Dispose();
    }
}
