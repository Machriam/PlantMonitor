using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.Dashboard;
using Plantmonitor.Server.Features.DeviceConfiguration;
using System;
using System.Threading.Tasks;
using Xunit;

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
        sut.ProcessImage(testZip);
    }

    [Fact]
    public void ProcessImage_BigPlants_ShouldWork()
    {
        var testZip = s_testZipFolder + "/BigPlantsTest.zip";
        var sut = CreatePhotoTourSummaryWorker();
        sut.ProcessImage(testZip);
    }
}
