using Microsoft.Extensions.DependencyInjection;
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
            _serviceScopeFactory);
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
