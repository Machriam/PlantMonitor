using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.Dashboard;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Tests.Features.AutomaticPhotoTourTests;

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
        var result = sut.ProcessImage(testZip, 0.2f);
    }

    [Fact]
    public void ProcessImage_BigPlants_ShouldWork()
    {
        var testZip = s_testZipFolder + "/BigPlantsTest.zip";
        var sut = CreatePhotoTourSummaryWorker();
        var result = sut.ProcessImage(testZip, 0.2f);
        result.GetResults();
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
