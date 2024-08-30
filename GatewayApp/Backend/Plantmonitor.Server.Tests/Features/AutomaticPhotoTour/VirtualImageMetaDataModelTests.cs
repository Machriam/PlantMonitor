using FluentAssertions;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Shared.Extensions;

namespace Plantmonitor.Server.Tests.Features.AutomaticPhotoTour;

public class VirtualImageMetaDataModelTests
{
    private static string TestFilePath(string file) => Path.Combine(Directory.GetCurrentDirectory().GetApplicationRootGitPath()!,
        "GatewayApp", "Backend", "Plantmonitor.Server.Tests", "Features", "AutomaticPhotoTour", file);

    private const string DefaultTestFile = "ExportAsTsv_Result.txt";
    private const string OutdatedFormat = "ExportAsTsv_OutdatedFormat.txt";
    private const string TestFileWithEmptyList = "ExportAsTsv_WithEmptyList_Result.txt";

    [Fact]
    public void FromTsvFile_ShouldWork()
    {
        var sut = CreateDefaultTestModel().AsJson();
        var importedModel = VirtualImageMetaDataModel.FromTsvFile(File.ReadAllText(TestFilePath(DefaultTestFile))).AsJson();
        sut.Should().BeEquivalentTo(importedModel);
    }

    [Fact]
    public void FromTsvFile_OutdatedFormat_ShouldNotThrow()
    {
        var sut = new VirtualImageMetaDataModel().AsJson();
        var importedModel = VirtualImageMetaDataModel.FromTsvFile(File.ReadAllText(TestFilePath(OutdatedFormat))).AsJson();
    }

    [Fact]
    public void FromTsvFile_WithEmptyList_ShouldWork()
    {
        var sut = CreateDefaultTestModel();
        sut.TemperatureReadings = [];
        var sutJson = sut.AsJson();
        var importedModel = VirtualImageMetaDataModel.FromTsvFile(File.ReadAllText(TestFilePath(TestFileWithEmptyList))).AsJson();
        sutJson.Should().BeEquivalentTo(importedModel);
    }

    [Fact]
    public void ExportAsTsv_ShouldWork()
    {
        var sut = CreateDefaultTestModel();
        var result = sut.ExportAsTsv();
        var expected = File.ReadAllText(TestFilePath(DefaultTestFile));
        result.Should().Be(expected);
    }

    [Fact]
    public void ExportAsTsv_EmptyLists_ShouldNotShowUp()
    {
        var sut = CreateDefaultTestModel();
        sut.TemperatureReadings = [];
        var result = sut.ExportAsTsv();
        var expected = File.ReadAllText(TestFilePath(TestFileWithEmptyList));
        result.Should().Be(expected);
    }

    private static VirtualImageMetaDataModel CreateDefaultTestModel()
    {
        var date = new DateTime(2024, 12, 31, 12, 50, 50);
        return new VirtualImageMetaDataModel()
        {
            Dimensions = new VirtualImageMetaDataModel.ImageDimensions(1, 2, 3, 4, 5, 6, 7, 8, 9, "Comment", 0.2f),
            ImageMetaData = Enumerable.Range(1, 10).Select(i => new VirtualImageMetaDataModel
                .ImageMetaDatum(i, $"Plant {i}", $"Comment {i}", true, false, date.AddDays(-i), date.AddDays(i), (273 + i) * 100)).ToArray(),
            TemperatureReadings = Enumerable.Range(1, 10).Select(i => new VirtualImageMetaDataModel
                .TemperatureReading($"Sensor {i}", $"Comment {i}", i, date.AddSeconds(i))).ToArray(),
            TimeInfos = new(date.AddMinutes(-10), date.AddMinutes(10), "Trip", 1, 2)
        };
    }
}
