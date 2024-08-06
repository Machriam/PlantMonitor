using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using NSubstitute;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AppConfiguration;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.ImageStitching;
using Plantmonitor.Shared.Extensions;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Plantmonitor.Server.Tests.Features.ImageStitching;

public class VirtualImageWorkerTests
{
    private readonly IServiceScopeFactory _ServiceScopeFactory;
    private readonly IEnvironmentConfiguration _EnvironmentConfiguration;
    private readonly ILogger<VirtualImageWorker> _Logger;

    public VirtualImageWorkerTests()
    {
        _ServiceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _EnvironmentConfiguration = Substitute.For<IEnvironmentConfiguration>();
        _Logger = Substitute.For<ILogger<VirtualImageWorker>>();
    }

    private VirtualImageWorker CreateVirtualImageWorker()
    {
        return new VirtualImageWorker(_ServiceScopeFactory, _EnvironmentConfiguration, _Logger);
    }

    [Fact]
    public void RunImageCreation_NegativeIrHeight_ShouldWork()
    {
        var sut = CreateVirtualImageWorker();
        var dataContext = Substitute.For<IDataContext>();
        var stitcher = new PhotoStitcher();
        var cropper = new ImageCropper();
        var configuration = Substitute.For<IEnvironmentConfiguration>();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest2/IrData";
        var visFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest2/VisData";
        var testData = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest2/TestData";
        var result = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest2/Result";
        dataContext.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>() { new PhotoTourTrip() {
            Id=1,
            PhotoTourFk=1,
            IrDataFolder=irFolder,
            VisDataFolder=visFolder,
            Timestamp=DateTime.Now,
            PhotoTourFkNavigation=new AutomaticPhotoTour(){Name="Test"}
        } });
        var extractionTemplates = File.ReadAllText($"{testData}/ExtractionTemplate.json")
            .FromJson<QueryableList<PlantExtractionTemplate>>() ?? [];
        var plants = File.ReadAllText($"{testData}/Plants.json").FromJson<QueryableList<PhotoTourPlant>>() ?? [];
        foreach (var template in extractionTemplates) template.PhotoTripFkNavigation = new PhotoTourTrip() { PhotoTourFk = 1, Timestamp = DateTime.Now.AddYears(-1) };
        foreach (var plant in plants) plant.PhotoTourFk = 1;
        dataContext.PlantExtractionTemplates.ReturnsForAnyArgs(extractionTemplates);
        dataContext.PhotoTourPlants.ReturnsForAnyArgs(plants);
        configuration.VirtualImagePath("", 0).ReturnsForAnyArgs(result);
        sut.RunImageCreation(dataContext, stitcher, cropper, configuration);
    }

    [Fact]
    public void RunImageCreation_ShouldWork()
    {
        var sut = CreateVirtualImageWorker();
        var dataContext = Substitute.For<IDataContext>();
        var stitcher = new PhotoStitcher();
        var cropper = new ImageCropper();
        var configuration = Substitute.For<IEnvironmentConfiguration>();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest/IrData";
        var visFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest/VisData";
        var testData = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest/TestData";
        var result = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest/Result";
        dataContext.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>() { new PhotoTourTrip() {
            Id=1,
            PhotoTourFk=1,
            IrDataFolder=irFolder,
            VisDataFolder=visFolder,
            Timestamp=DateTime.Now,
            PhotoTourFkNavigation=new AutomaticPhotoTour(){Name="Test"}
        } });
        var extractionTemplates = File.ReadAllText($"{testData}/ExtractionTemplate.json")
            .FromJson<QueryableList<PlantExtractionTemplate>>() ?? [];
        var plants = File.ReadAllText($"{testData}/Plants.json").FromJson<QueryableList<PhotoTourPlant>>() ?? [];
        foreach (var template in extractionTemplates) template.PhotoTripFkNavigation = new PhotoTourTrip() { PhotoTourFk = 1, Timestamp = DateTime.Now.AddYears(-1) };
        foreach (var plant in plants) plant.PhotoTourFk = 1;
        dataContext.PlantExtractionTemplates.ReturnsForAnyArgs(extractionTemplates);
        dataContext.PhotoTourPlants.ReturnsForAnyArgs(plants);
        configuration.VirtualImagePath("", 0).ReturnsForAnyArgs(result);
        sut.RunImageCreation(dataContext, stitcher, cropper, configuration);
    }
}
