using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.Dashboard;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Tests.Features.AutomaticPhotoTourTests;
using Plantmonitor.Shared.Extensions;

namespace Plantmonitor.Server.Tests.Features.Dashboard;

public class PhotoTourSummaryWorkerTests
{
    private static readonly string s_testZipFolder = Path.Combine(Directory.GetCurrentDirectory().GetApplicationRootGitPath()!, "PlantMonitorControl.Tests", "TestData", "PhotoTourSummaryTest");
    private readonly IEnvironmentConfiguration _environmentConfiguration;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PhotoTourSummaryWorkerTests()
    {
        _environmentConfiguration = Substitute.For<IEnvironmentConfiguration>();
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
    }

    private PhotoTourSummaryWorker CreatePhotoTourSummaryWorker()
    {
        return new PhotoTourSummaryWorker(
            _environmentConfiguration,
            _serviceScopeFactory, Substitute.For<ILogger<PhotoTourSummaryWorker>>());
    }

    [Fact]
    public void ProcessImage_SmallPlants_ShouldWork()
    {
        var testZip = s_testZipFolder + "/SmallPlantsTest.zip";
        var sut = CreatePhotoTourSummaryWorker();
        var result = sut.ProcessImage(testZip);
        var imageDescriptors = result.GetResults().OrderBy(r => r.Plant.ImageIndex);
        File.WriteAllText(s_testZipFolder + "/SmallPlantsTestResult.json", imageDescriptors.AsJson(writeIndented: true));
        var expectedLeafCount = File.ReadAllText(s_testZipFolder + "/CorrectLeafCount_SmallPlantsTestResult.json").FromJson<List<PhotoSummaryResult.ImageResult>>() ?? new();
        var comparison = expectedLeafCount
            .OrderBy(ex => ex.Plant.ImageIndex)
            .Select(ex => $"Index: {ex.Plant.ImageIndex}\t\tExpected: " + ex.LeafCount + "\t\tDiff: " + (imageDescriptors.First(i => i.Plant.ImageIndex == ex.Plant.ImageIndex).LeafCount - ex.LeafCount)).Concat("\n");
        var totalMisses = expectedLeafCount.Sum(ex => ex.LeafCount - imageDescriptors.First(i => i.Plant.ImageIndex == ex.Plant.ImageIndex).LeafCount);
        comparison = $"Total misses: {totalMisses}\n{comparison}";
        File.WriteAllText(s_testZipFolder + "/SmallPlantsLeafComparison.txt", comparison);
    }

    [Fact]
    public void ProcessImage_EmptyImages_ShouldWork()
    {
        var testZip = s_testZipFolder + "/EmptyImage.zip";
        var sut = CreatePhotoTourSummaryWorker();
        var result = sut.ProcessImage(testZip);
        var imageDescriptors = result.GetResults().OrderBy(r => r.Plant.ImageIndex);
        imageDescriptors.Count().Should().Be(0);
        var photoTrip = result.GetPhotoTripData;
        var deviceTemps = result.DeviceTemperatures;
        deviceTemps.Count.Should().Be(1);
        deviceTemps[0].MedianTemperature.Should().Be(45f);
        deviceTemps[0].CountOfMeasurements.Should().Be(13);
        photoTrip.TripStart.Ticks.Should().Be(638605240420000000L);
        photoTrip.TripEnd.Ticks.Should().Be(00638605240720000000L);
    }

    [Fact]
    public void ProcessImage_LeafOutOfRange_ShouldWork()
    {
        var testZip = s_testZipFolder + "/LeafOutOfRange.zip";
        var expectedData = File.ReadAllText(s_testZipFolder + "/LeafOutOfRange_Expected.txt");
        var sut = CreatePhotoTourSummaryWorker();
        var result = sut.ProcessImage(testZip);
        var imageDescriptors = result.GetResults().OrderBy(r => r.Plant.ImageIndex);
        var leafsOutOfRange = imageDescriptors
            .Where(id => id.LeafOutOfRange)
            .Select(id => id.Plant.ImageName + ":" + id.LeafOutOfRange)
            .Concat("\n");
        leafsOutOfRange.Should().Be(expectedData);
    }

    [Fact]
    public void ProcessImage_6_6_NotInDictionary_ShouldWork()
    {
        var testZip = s_testZipFolder + "/6_6_NotInDictionaryTest.zip";
        var sut = CreatePhotoTourSummaryWorker();
        var result = sut.ProcessImage(testZip);
        var imageDescriptors = result.GetResults().OrderBy(r => r.Plant.ImageIndex);
    }

    [Fact]
    public void ProcessImage_BigPlants_ShouldWork()
    {
        var testZip = s_testZipFolder + "/BigPlantsTest.zip";
        var sut = CreatePhotoTourSummaryWorker();
        var result = sut.ProcessImage(testZip);
        var imageDescriptors = result.GetResults().OrderBy(r => r.Plant.ImageIndex);
        File.WriteAllText(s_testZipFolder + "/BigPlantsTestResult.json", imageDescriptors.AsJson(writeIndented: true));
        var expectedLeafCount = File.ReadAllText(s_testZipFolder + "/CorrectLeafCount_BigPlantsTestResult.json").FromJson<List<PhotoSummaryResult.ImageResult>>() ?? new();
        var comparison = expectedLeafCount
            .OrderBy(ex => ex.Plant.ImageIndex)
            .Select(ex => $"Index: {ex.Plant.ImageIndex}\t\tExpected: " + ex.LeafCount + "\t\tFound: " + imageDescriptors.First(i => i.Plant.ImageIndex == ex.Plant.ImageIndex).LeafCount).Concat("\n");
        var totalMisses = expectedLeafCount.Sum(ex => ex.LeafCount - imageDescriptors.First(i => i.Plant.ImageIndex == ex.Plant.ImageIndex).LeafCount);
        comparison = $"Total misses: {totalMisses}\n{comparison}";
        File.WriteAllText(s_testZipFolder + "/BigPlantsLeafComparison.txt", comparison);
    }

    [Fact]
    public void ProcessImage_GetBorderMask_ShouldWork()
    {
        var testZip = s_testZipFolder + "/LeafOutOfRange.zip";
        var sut = CreatePhotoTourSummaryWorker();
        var zipData = sut.GetDataFromZip(testZip);
        var result = sut.SubImageBorderMask(zipData.VisImage);
        var plantMask = sut.GetPlantMask(zipData.VisImage);
        result.ShowImage("PlantMask", 200);
        zipData.VisImage.ShowImage("Vis Original", 200);
        zipData.VisImage.Dispose();
        zipData.RawIrImage.Dispose();
        result.Dispose();
    }

    [Fact]
    public void ProcessImage_CreatePlantMask_ShouldLookGood()
    {
        var testZip = s_testZipFolder + "/BigPlantsTest.zip";
        var sut = CreatePhotoTourSummaryWorker();
        var zipData = sut.GetDataFromZip(testZip);
        var result = sut.GetPlantMask(zipData.VisImage);
        result.ShowImage("PlantMask", 200);
        zipData.VisImage.ShowImage("Vis Original", 200);
        zipData.VisImage.Dispose();
        zipData.RawIrImage.Dispose();
        result.Dispose();
    }
}
