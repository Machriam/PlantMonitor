using NSubstitute;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.ImageStitching;
using NpgsqlTypes;
using Emgu.CV;
using FluentAssertions;
using Emgu.CV.Structure;
using Plantmonitor.Server.Tests.Features.AutomaticPhotoTourTests;

namespace Plantmonitor.Server.Tests.Features.ImageStitching;

public class ImageCropperTests
{
    private static readonly NpgsqlPoint[] s_manyPlantsPolygon = [
        new NpgsqlPoint(885.4214876033056, 729.2826446280991),
        new NpgsqlPoint(1259.6994991652755,1138.5375626043406),
        new NpgsqlPoint(440.41402337228715,1076.9949916527546),
        new NpgsqlPoint(405.79632721202,271.1719532554257),
        new NpgsqlPoint(678.8914858096828,117.31552587646077),
        new NpgsqlPoint(1667.4190317195325,101.92988313856426),
        new NpgsqlPoint(1967.4390651085141,509.6494156928214),
        new NpgsqlPoint(1913.5893155258764,1009.6828046744574),
        new NpgsqlPoint(1259.6994991652755,1138.5375626043406)];

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

    public ImageCropperTests()
    {
    }

    private ImageCropper CreateImageCropper()
    {
        return new ImageCropper();
    }

    [Fact]
    public void CropImage_ShouldWork()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var imageFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/Plants.png";
        var result = sut.CropImages(imageFile, "", s_singlePlantBottomMiddlePolygon, new(0, 0), 960);
        result.VisImage.ShowImage("CroppedImage");
        result.VisImage.LogCall(x => x.Cols).Should().Be(215);
        result.VisImage.LogCall(x => x.Rows).Should().Be(243);
        result.VisImage.Dispose();
    }

    [Fact]
    public void CropImage_ShouldNotRunForever_ShouldWork()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var imageFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/Plants.png";
        var result = sut.CropImages(imageFile, "", s_singlePlantBottomMiddlePolygon, new(0, 0), 960);
        result.VisImage.ShowImage("CroppedImage");
        result.VisImage.LogCall(x => x.Cols).Should().Be(215);
        result.VisImage.LogCall(x => x.Rows).Should().Be(243);
        result.VisImage.Dispose();
    }

    [Fact]
    public void CropImage_WithOffset_ShouldWork()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var imageFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/Plants.png";
        var result = sut.CropImages(imageFile, "", s_singlePlantBottomMiddlePolygon, new(100, 100), 960);
        result.VisImage.ShowImage("CroppedImage");
        result.VisImage.LogCall(x => x.Cols).Should().Be(215);
        result.VisImage.LogCall(x => x.Rows).Should().Be(243);
        result.VisImage.Dispose();
    }

    [Fact]
    public void IRColorMap_ShouldWork()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-33-19-047_-6000_29710.rawir";
        var irMat = sut.MatFromFile(irFile, out _);
        sut.ApplyIrColorMap(irMat!);
        sut.Resize(irMat!, 640);
        irMat!.ShowImage("CroppedIR");
        irMat!.Dispose();
    }

    [Fact]
    public void CreateRawIR_ShouldWork()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-33-19-047_-6000_29710.rawir";
        var irMat = sut.MatFromFile(irFile, out _);
        var resultMat = sut.CreateRawIr(irMat!);
        var resizeMat = resultMat.LogCall(x => x.Clone().AsManaged());
        sut.Resize(resizeMat, 640);
        resizeMat.ShowImage("CroppedIR", 100);
        resultMat.Dispose();
        resizeMat.Dispose();
        irMat!.Dispose();
    }

    [Fact]
    public void CropImage_OutOfRange_ShouldNotError()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-33-19-047_-6000_29710.rawir";
        var visFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-26-10-207_-6000_0.jpg";
        var result = sut.CropImages(visFile, irFile, s_singlePlantBottomMiddlePolygon_1WeekLaterAndMoved, new(-1000, -1000), 960);
        sut.ApplyIrColorMap(result.IrImage!);
        result.IrImage!.LogCall(x => x.Height).Should().Be(232);
        result.IrImage!.LogCall(x => x.Width).Should().Be(232);
        result.IrImage!.ShowImage("OutOfRange", 100);
        result.IrImage!.Dispose();
        result.VisImage.Dispose();
    }

    [Fact]
    public void CropImage_IR_ShouldWork()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-33-19-047_-6000_29710.rawir";
        var visFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-26-10-207_-6000_0.jpg";
        var result = sut.CropImages(visFile, irFile, s_singlePlantBottomMiddlePolygon_1WeekLaterAndMoved, new(121, 39), 960);//new(121, 39));
        sut.ApplyIrColorMap(result.IrImage!);
        result.IrImage!.ShowImage("CroppedIR");
        result.VisImage.ShowImage("CroppedVis");
        result.IrImage!.Dispose();
        result.VisImage.Dispose();
    }

    [Fact]
    public void CropImage_IROutOfRange_Left_ShouldHaveSameSize()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-33-19-047_-6000_29710.rawir";
        var visFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-26-10-207_-6000_0.jpg";
        var result = sut.CropImages(visFile, irFile, s_singlePlantBottomMiddlePolygon_1WeekLaterAndMoved, new(400, 150), 960);//new(121, 39));
        sut.ApplyIrColorMap(result.IrImage!);
        result.IrImage!.ShowImage("CroppedIR");
        result.VisImage.ShowImage("CroppedVis");
        result.IrImage!.LogCall(x => x.Height).Should().Be(result.VisImage.LogCall(x => x.Height));
        result.IrImage!.LogCall(x => x.Width).Should().Be(result.VisImage.LogCall(x => x.Width));
        result.IrImage!.Dispose();
        result.VisImage.Dispose();
    }

    [Fact]
    public void CropImage_IROutOfRange_TopRight_ShouldHaveSameSize()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-33-19-047_-6000_29710.rawir";
        var visFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-26-10-207_-6000_0.jpg";
        var result = sut.CropImages(visFile, irFile, s_singlePlantBottomMiddlePolygon_1WeekLaterAndMoved, new(-200, 300), 960);//new(121, 39));
        sut.ApplyIrColorMap(result.IrImage!);
        result.IrImage!.ShowImage("CroppedIR");
        result.VisImage.ShowImage("CroppedVis");
        result.IrImage!.LogCall(x => x.Height).Should().Be(result.VisImage.LogCall(x => x.Height));
        result.IrImage!.LogCall(x => x.Width).Should().Be(result.VisImage.LogCall(x => x.Width));
        result.IrImage!.Dispose();
        result.VisImage.Dispose();
    }

    [Fact]
    public void CropImage_IROutOfRange_Right_ShouldHaveSameSize()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-33-19-047_-6000_29710.rawir";
        var visFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-26-10-207_-6000_0.jpg";
        var result = sut.CropImages(visFile, irFile, s_singlePlantBottomMiddlePolygon_1WeekLaterAndMoved, new(-200, 150), 960);//new(121, 39));
        sut.ApplyIrColorMap(result.IrImage!);
        result.IrImage!.ShowImage("CroppedIR");
        result.VisImage.ShowImage("CroppedVis");
        result.IrImage!.LogCall(x => x.Height).Should().Be(result.VisImage.LogCall(x => x.Height));
        result.IrImage!.LogCall(x => x.Width).Should().Be(result.VisImage.LogCall(x => x.Width));
        result.IrImage!.Dispose();
        result.VisImage.Dispose();
    }

    [Fact]
    public void CropImage_IROutOfRange_Top_ShouldHaveSameSize()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-33-19-047_-6000_29710.rawir";
        var visFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-26-10-207_-6000_0.jpg";
        var result = sut.CropImages(visFile, irFile, s_singlePlantBottomMiddlePolygon_1WeekLaterAndMoved, new(100, 300), 960);//new(121, 39));
        sut.ApplyIrColorMap(result.IrImage!);
        result.IrImage!.ShowImage("CroppedIR");
        result.VisImage.ShowImage("CroppedVis");
        result.IrImage!.LogCall(x => x.Height).Should().Be(result.VisImage.LogCall(x => x.Height));
        result.IrImage!.LogCall(x => x.Width).Should().Be(result.VisImage.LogCall(x => x.Width));
        result.IrImage!.Dispose();
        result.VisImage.Dispose();
    }

    [Fact]
    public void CropImage_IROutOfRange_Bottom_ShouldHaveSameSize()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-33-19-047_-6000_29710.rawir";
        var visFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/2024-07-28_20-26-10-207_-6000_0.jpg";
        var result = sut.CropImages(visFile, irFile, s_singlePlantBottomMiddlePolygon_1WeekLaterAndMoved, new(170, -200), 960);//new(121, 39));
        sut.ApplyIrColorMap(result.IrImage!);
        result.IrImage!.ShowImage("CroppedIR");
        result.VisImage.ShowImage("CroppedVis");
        result.IrImage!.LogCall(x => x.Height).Should().Be(result.VisImage.LogCall(x => x.Height));
        result.IrImage!.LogCall(x => x.Width).Should().Be(result.VisImage.LogCall(x => x.Width));
        result.IrImage!.Dispose();
        result.VisImage.Dispose();
    }
}
