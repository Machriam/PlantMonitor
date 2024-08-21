using FluentAssertions;
using NSubstitute;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Server.Features.DeviceConfiguration;
using System;
using Xunit;

namespace Plantmonitor.Server.Tests.Features.AutomaticPhotoTour;

public class VirtualImageMetaDataModelTests
{
    [Fact]
    public void FromTsvFile_ShouldWork()
    {
        string tsv = null;
    }

    [Fact]
    public void ExportAsTsv_ShouldWork()
    {
        var date = new DateTime(2024, 12, 31, 12, 50, 50);
        var sut = new VirtualImageMetaDataModel()
        {
            Dimensions = new VirtualImageMetaDataModel.ImageDimensions(1, 2, 3, 4, 5, 6, 7, 8, 9, "Comment"),
            ImageMetaData = Enumerable.Range(1, 10).Select(i => new VirtualImageMetaDataModel
                .ImageMetaDatum(i, $"Plant {i}", $"Comment {i}", true, false, date.AddDays(-i), date.AddDays(i), 273f + i)).ToArray(),
            TemperatureReadings = Enumerable.Range(1, 10).Select(i => new VirtualImageMetaDataModel
                .TemperatureReading($"Sensor {i}", $"Comment {i}", i, date.AddSeconds(i))).ToArray(),
            TimeInfos = new(date.AddMinutes(-10), date.AddMinutes(10))
        };
        var result = sut.ExportAsTsv();
        var expectedPath = Path.Combine(Directory.GetCurrentDirectory().GetApplicationRootGitPath()!,
            "GatewayApp", "Backend", "Plantmonitor.Server.Tests", "Features", "AutomaticPhotoTour", "ExportAsTsv_Result.txt");
        var expected = File.ReadAllText(expectedPath, System.Text.Encoding.ASCII);
        result.Should().Be(expected);
    }
}
