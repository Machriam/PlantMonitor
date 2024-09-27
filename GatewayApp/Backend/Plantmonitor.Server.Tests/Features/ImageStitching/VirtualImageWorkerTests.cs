using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Npgsql;
using NSubstitute;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.ImageWorker;
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
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IEnvironmentConfiguration _environmentConfiguration;
    private readonly ILogger<VirtualImageWorker> _logger;

    public VirtualImageWorkerTests()
    {
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _environmentConfiguration = Substitute.For<IEnvironmentConfiguration>();
        _logger = Substitute.For<ILogger<VirtualImageWorker>>();
    }

    private VirtualImageWorker CreateVirtualImageWorker()
    {
        return new VirtualImageWorker(_serviceScopeFactory, _environmentConfiguration, _logger);
    }

    [Fact]
    public void RunImageCreation_NoVisImages_ShouldWork()
    {
        var sut = CreateVirtualImageWorker();
        var dataContext = Substitute.For<IDataContext>();
        var stitcher = new PhotoStitcher(Substitute.For<ILogger<IPhotoStitcher>>());
        var cropper = new ImageCropper();
        var configuration = Substitute.For<IEnvironmentConfiguration>();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoVis/IrData";
        var visFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoVis/VisData";
        var testData = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoVis/TestData";
        var result = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoVis/Result";
        dataContext.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>() { new() {
            Id=1,
            PhotoTourFk=1,
            IrDataFolder=irFolder,
            VisDataFolder=visFolder,
            Timestamp=DateTime.Now,
            PhotoTourFkNavigation=new DataModel.DataModel.AutomaticPhotoTour(){Name="Test"}
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
    public void RunImageCreation_NoImages_ShouldWork()
    {
        var sut = CreateVirtualImageWorker();
        var dataContext = Substitute.For<IDataContext>();
        var stitcher = new PhotoStitcher(Substitute.For<ILogger<IPhotoStitcher>>());
        var cropper = new ImageCropper();
        var configuration = Substitute.For<IEnvironmentConfiguration>();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoImages/IrData";
        var visFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoImages/VisData";
        var testData = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoImages/TestData";
        var result = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoImages/Result";
        dataContext.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>() { new() {
            Id=1,
            PhotoTourFk=1,
            IrDataFolder=irFolder,
            VisDataFolder=visFolder,
            Timestamp=DateTime.Now,
            PhotoTourFkNavigation=new DataModel.DataModel.AutomaticPhotoTour(){Name="Test"}
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
    public void RunImageCreation_NoIR_ShouldWork()
    {
        var sut = CreateVirtualImageWorker();
        var dataContext = Substitute.For<IDataContext>();
        var stitcher = new PhotoStitcher(Substitute.For<ILogger<IPhotoStitcher>>());
        var cropper = new ImageCropper();
        var configuration = Substitute.For<IEnvironmentConfiguration>();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoIr/IrData";
        var visFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoIr/VisData";
        var testData = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoIr/TestData";
        var result = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NoIr/Result";
        dataContext.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>() { new() {
            Id=1,
            PhotoTourFk=1,
            IrDataFolder=irFolder,
            VisDataFolder=visFolder,
            Timestamp=DateTime.Now,
            PhotoTourFkNavigation=new DataModel.DataModel.AutomaticPhotoTour(){Name="Test"}
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
    public void RunImageCreation_NegativeBounds_ShouldWork()
    {
        var sut = CreateVirtualImageWorker();
        var dataContext = Substitute.For<IDataContext>();
        var stitcher = new PhotoStitcher(Substitute.For<ILogger<IPhotoStitcher>>());
        var cropper = new ImageCropper();
        var configuration = Substitute.For<IEnvironmentConfiguration>();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NegativeBounds/IrData";
        var visFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NegativeBounds/VisData";
        var testData = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NegativeBounds/TestData";
        var result = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest_NegativeBounds/Result";
        dataContext.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>() { new() {
            Id=1,
            PhotoTourFk=1,
            IrDataFolder=irFolder,
            VisDataFolder=visFolder,
            Timestamp=DateTime.Now,
            PhotoTourFkNavigation=new DataModel.DataModel.AutomaticPhotoTour(){Name="Test"}
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
        var stitcher = new PhotoStitcher(Substitute.For<ILogger<IPhotoStitcher>>());
        var cropper = new ImageCropper();
        var configuration = Substitute.For<IEnvironmentConfiguration>();
        var applicationPath = Directory.GetCurrentDirectory().GetApplicationRootGitPath();
        var irFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest/IrData";
        var visFolder = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest/VisData";
        var testData = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest/TestData";
        var result = $"{applicationPath}/PlantMonitorControl.Tests/TestData/VirtualImageTest/Result";
        var measurement = new TemperatureMeasurement()
        {
            PhotoTourFk = 1,
            Comment = "Comment",
            SensorId = "9x99",
            StartTime = DateTime.Now.AddMinutes(1),
        };
        dataContext.TemperatureMeasurementValues.ReturnsForAnyArgs(new QueryableList<TemperatureMeasurementValue>()
        {
            new(){MeasurementFkNavigation=measurement,Temperature=123,Timestamp=DateTime.Now.AddMinutes(1)},
            new(){MeasurementFkNavigation=measurement,Temperature=23,Timestamp=DateTime.Now.AddMinutes(2)},
            new(){MeasurementFkNavigation=measurement,Temperature=3,Timestamp=DateTime.Now.AddMinutes(3)},
        });
        dataContext.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>() { new() {
            Id=1,
            PhotoTourFk=1,
            IrDataFolder=irFolder,
            VisDataFolder=visFolder,
            Timestamp=DateTime.Now,
            PhotoTourFkNavigation=new DataModel.DataModel.AutomaticPhotoTour(){Name="Test"}
        },new() {
            Id=2,
            PhotoTourFk=1,
            IrDataFolder=irFolder,
            VisDataFolder=visFolder,
            Timestamp=DateTime.Now.AddHours(1),
            PhotoTourFkNavigation=new DataModel.DataModel.AutomaticPhotoTour(){Name="Test"}
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
