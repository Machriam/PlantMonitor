using NSubstitute;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.ImageStitching;
using NpgsqlTypes;
using Emgu.CV;
using FluentAssertions;

namespace Plantmonitor.Server.Tests.Features.ImageStitching;

public class ImageCropperTests
{
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
        var result = sut.CropImage(imageFile, [new NpgsqlPoint(885.4214876033056, 729.2826446280991),
           new NpgsqlPoint(771.1735537190082, 628.3636363636363),
           new NpgsqlPoint(780.694214876033, 508.4033057851239),
           new NpgsqlPoint(944.4495867768594, 449.3752066115702),
           new NpgsqlPoint(1024.4231404958678, 544.5818181818181),
           new NpgsqlPoint(1022.5190082644627, 624.5553719008263),
           new NpgsqlPoint(982.5322314049586, 696.9123966942149),
           new NpgsqlPoint(885.4214876033056, 729.2826446280991)], new(0, 0));
        CvInvoke.Imshow("Cropped Image", result);
        CvInvoke.WaitKey(300);
        result.Cols.Should().Be(253);
        result.Rows.Should().Be(279);
        result.Dispose();
    }

    [Fact]
    public void CropImage_WithOffset_ShouldWork()
    {
        var sut = CreateImageCropper();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var imageFile = $"{applicationPath}/PlantMonitorControl.Tests/TestData/CropTest/Plants.png";
        var result = sut.CropImage(imageFile, [new NpgsqlPoint(885.4214876033056, 729.2826446280991),
           new NpgsqlPoint(771.1735537190082, 628.3636363636363),
           new NpgsqlPoint(780.694214876033, 508.4033057851239),
           new NpgsqlPoint(944.4495867768594, 449.3752066115702),
           new NpgsqlPoint(1024.4231404958678, 544.5818181818181),
           new NpgsqlPoint(1022.5190082644627, 624.5553719008263),
           new NpgsqlPoint(982.5322314049586, 696.9123966942149),
           new NpgsqlPoint(885.4214876033056, 729.2826446280991)], new(100, 100));
        CvInvoke.Imshow("Cropped Image", result);
        CvInvoke.WaitKey(300);
        result.Cols.Should().Be(253);
        result.Rows.Should().Be(279);
        result.Dispose();
    }
}
