using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Plantmonitor.Server.Tests.Features.AutomaticPhotoTourTests;

public class DeviceRestarterTests
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IServiceScope _serviceScope = Substitute.For<IServiceScope>();
    private readonly IServiceProvider _provider = Substitute.For<IServiceProvider>();
    private readonly IDeviceConnectionEventBus _eventBus = Substitute.For<IDeviceConnectionEventBus>();
    private readonly IDataContext _context = Substitute.For<IDataContext>();
    private readonly IIrImageTakingClient _irClient = Substitute.For<IIrImageTakingClient>();
    private readonly IVisImageTakingClient _visClient = Substitute.For<IVisImageTakingClient>();
    private readonly IDeviceApiFactory _deviceApi = Substitute.For<IDeviceApiFactory>();

    public DeviceRestarterTests()
    {
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScopeFactory.CreateScope().ReturnsForAnyArgs(_serviceScope);
        _serviceScope.ServiceProvider.ReturnsForAnyArgs(_provider);
        _provider.GetService(typeof(IDeviceConnectionEventBus)).Returns(_eventBus);
        _provider.GetService(typeof(IDataContext)).Returns(_context);
        _provider.GetService(typeof(IDeviceApiFactory)).Returns(_deviceApi);
        _deviceApi.IrImageTakingClient("").ReturnsForAnyArgs(_irClient);
        _deviceApi.VisImageTakingClient("").ReturnsForAnyArgs(_visClient);
        _context.CreatePhotoTourEventLogger(default).ReturnsForAnyArgs((message, type) => _context.PhotoTourEvents.Add(new PhotoTourEvent() { Message = message, Type = type }));
    }

    private DeviceRestarter CreateDeviceRestarter()
    {
        return new DeviceRestarter(_serviceScopeFactory);
    }

    [Fact]
    public async Task CheckDeviceHealth_InvalidPhotoTour_ShouldErrorLog()
    {
        var deviceGuid = Guid.NewGuid().ToString();
        var switchDevice = Guid.NewGuid().ToString();
        var sut = CreateDeviceRestarter();
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        _context.AutomaticPhotoTours.ReturnsForAnyArgs(new QueryableList<AutomaticPhotoTour>()
        {
            new(){ DeviceId=Guid.Parse(deviceGuid)}
        });
        var devices = new List<DeviceHealthState>() {
            new(new(default, switchDevice, "testSwitcher", HealthState.NA), 0, ""),
            new(new(default, deviceGuid, "faultyDevice", HealthState.ThermalCameraFunctional), 5, "")
        };
        _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
        var client = Substitute.For<ISwitchOutletsClient>();
        _deviceApi.SwitchOutletsClient("").ReturnsForAnyArgs(client);
        _context.DeviceSwitchAssociations.ReturnsForAnyArgs(new QueryableList<DeviceSwitchAssociation>()
        {
            new(){DeviceId=Guid.NewGuid(),OutletOffFkNavigation=new(){Code=1234},OutletOnFkNavigation=new(){ Code=5678} }
        });

        var result = await sut.CheckDeviceHealth(1, _serviceScope, _context);
        result.DeviceHealthy.Should().BeFalse();
    }

    [Fact]
    public async Task CheckDeviceHealth_DefaultCase_ShouldWork()
    {
        var deviceGuid = Guid.NewGuid().ToString();
        var sut = CreateDeviceRestarter();
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        _context.AutomaticPhotoTours.ReturnsForAnyArgs(new QueryableList<AutomaticPhotoTour>()
        {
            new(){Id=1, DeviceId=Guid.Parse(deviceGuid)}
        });
        var devices = new List<DeviceHealthState>() {
            new(new(default, deviceGuid, "device", HealthState.NA), 0, ""),
        };
        _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
        var client = Substitute.For<ISwitchOutletsClient>();
        _deviceApi.SwitchOutletsClient("").ReturnsForAnyArgs(client);
        var movementClient = Substitute.For<IMotorMovementClient>();
        _deviceApi.MovementClient("").ReturnsForAnyArgs(movementClient);
        movementClient.CurrentpositionAsync().ReturnsForAnyArgs(new MotorPosition(false, true, 0));
        await using var visStream = new MemoryStream(new byte[1000]);
        await using var irStream = new MemoryStream(new byte[1000]);
        _irClient.PreviewimageAsync().ReturnsForAnyArgs(new FileResponse(200, new Dictionary<string, IEnumerable<string>>(), irStream, default, default));
        _visClient.PreviewimageAsync().ReturnsForAnyArgs(new FileResponse(200, new Dictionary<string, IEnumerable<string>>(), visStream, default, default));

        await sut.CheckDeviceHealth(1, _serviceScope, _context);

        _context.PhotoTourEvents.Count().Should().Be(3);
        _context.PhotoTourEvents.Should().AllSatisfy(x => x.Type.Should().Be(PhotoTourEventType.Information));
        _context.PhotoTourEvents.Skip(1).First().Message.Should().Contain("Checking Camera");
        _context.PhotoTourEvents.Last().Message.Should().Contain("Camera working");
    }

    [Fact]
    public async Task CheckDeviceHealth_NoImages_ShouldErrorLog()
    {
        var deviceGuid = Guid.NewGuid().ToString();
        var sut = CreateDeviceRestarter();
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        _context.AutomaticPhotoTours.ReturnsForAnyArgs(new QueryableList<AutomaticPhotoTour>()
        {
            new(){Id=1, DeviceId=Guid.Parse(deviceGuid),TemperatureMeasurements=new QueryableList<TemperatureMeasurement>(){
                new TemperatureMeasurement(){SensorId=TemperatureMeasurement.FlirLeptonSensorId}
            } }
        });
        var devices = new List<DeviceHealthState>() {
            new(new(default, deviceGuid, "device", HealthState.NA), 0, ""),
        };
        _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
        var client = Substitute.For<ISwitchOutletsClient>();
        _deviceApi.SwitchOutletsClient("").ReturnsForAnyArgs(client);
        await using var failStream = new MemoryStream();
        await using var successStream = new MemoryStream(new byte[1000]);
        _irClient.PreviewimageAsync().ReturnsForAnyArgs(new FileResponse(200, new Dictionary<string, IEnumerable<string>>(), failStream, default, default));
        _visClient.PreviewimageAsync().ReturnsForAnyArgs(new FileResponse(200, new Dictionary<string, IEnumerable<string>>(), successStream, default, default));
        var movementClient = Substitute.For<IMotorMovementClient>();
        _deviceApi.MovementClient("").ReturnsForAnyArgs(movementClient);
        movementClient.CurrentpositionAsync().ReturnsForAnyArgs(new MotorPosition(false, true, 0));

        await sut.CheckDeviceHealth(1, _serviceScope, _context);

        _context.PhotoTourEvents.Count().Should().Be(4);
        _context.PhotoTourEvents.First().Type.Should().Be(PhotoTourEventType.Information);
        _context.PhotoTourEvents.Skip(1).First().Message.Should().Contain("Checking Camera");
        _context.PhotoTourEvents.Skip(3).Take(1).First().Type.Should().Be(PhotoTourEventType.Information);
        _context.PhotoTourEvents.Skip(3).Take(1).First().Message.Should().Contain("Restart needs atleast 2 consecutive failures");
    }

    [Fact]
    public async Task CheckDeviceHealth_NoDeviceHealth_ShouldErrorLog()
    {
        var deviceGuid = Guid.NewGuid().ToString();
        var switchDevice = Guid.NewGuid().ToString();
        var sut = CreateDeviceRestarter();
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        _context.AutomaticPhotoTours.ReturnsForAnyArgs(new QueryableList<AutomaticPhotoTour>()
        {
            new(){Id=1, DeviceId=Guid.Parse(deviceGuid)}
        });
        var devices = new List<DeviceHealthState>() {
            new(new(default, switchDevice, "testSwitcher", HealthState.NA), 0, ""),
        };
        _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
        var client = Substitute.For<ISwitchOutletsClient>();
        _deviceApi.SwitchOutletsClient("").ReturnsForAnyArgs(client);

        await sut.CheckDeviceHealth(1, _serviceScope, _context);

        _context.PhotoTourEvents.Count().Should().Be(2);
        _context.PhotoTourEvents.First().Type.Should().Be(PhotoTourEventType.Error);
        _context.PhotoTourEvents.First().Message.Should().Contain("Camera Device").And.Contain("not found. Trying Restart.");
    }

    [Fact]
    public async Task RestartDevice_NoSwitchableDeviceFound_ShouldWork()
    {
        var deviceGuid = Guid.NewGuid().ToString();
        var switchDevice = Guid.NewGuid().ToString();
        var sut = CreateDeviceRestarter();
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        var devices = new List<DeviceHealthState>() {
            new(new(default, switchDevice, "testSwitcher", HealthState.NA), 0, ""),
            new(new(default, deviceGuid, "faultyDevice", HealthState.ThermalCameraFunctional), 5, "")
        };
        _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
        var client = Substitute.For<ISwitchOutletsClient>();
        _deviceApi.SwitchOutletsClient("").ReturnsForAnyArgs(client);
        _context.DeviceSwitchAssociations.ReturnsForAnyArgs(new QueryableList<DeviceSwitchAssociation>()
        {
            new(){DeviceId=Guid.Parse(deviceGuid),OutletOffFkNavigation=new(){Code=1234},OutletOnFkNavigation=new(){ Code=5678} }
        });

        await sut.RestartDevice(deviceGuid, 1, "Test");
        await sut.RestartDevice(deviceGuid, 1, "Test");

        _eventBus.DidNotReceiveWithAnyArgs().UpdateDeviceHealths(default!);
        await client.DidNotReceiveWithAnyArgs().SwitchoutletAsync(1234);
        _context.PhotoTourEvents.Count().Should().Be(2);
        _context.PhotoTourEvents.Last().Type.Should().Be(PhotoTourEventType.Warning);
    }

    [Fact]
    public async Task RestartDevice_ShouldWork()
    {
        var deviceGuid = Guid.NewGuid().ToString();
        var switchDevice = Guid.NewGuid().ToString();
        var sut = CreateDeviceRestarter();
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        var devices = new List<DeviceHealthState>() {
            new(new(default, switchDevice, "testSwitcher", HealthState.CanSwitchOutlets), 0, ""),
            new(new(default, deviceGuid, "faultyDevice", HealthState.ThermalCameraFunctional), 5, "")
        };
        _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
        var client = Substitute.For<ISwitchOutletsClient>();
        _deviceApi.SwitchOutletsClient("").ReturnsForAnyArgs(client);
        _context.DeviceSwitchAssociations.ReturnsForAnyArgs(new QueryableList<DeviceSwitchAssociation>()
        {
            new(){DeviceId=Guid.Parse(deviceGuid),OutletOffFkNavigation=new(){Code=1234},OutletOnFkNavigation=new(){ Code=5678} }
        });

        await sut.RestartDevice(deviceGuid, 1, "Test");
        await sut.RestartDevice(deviceGuid, 1, "Test");

        var updateDeviceHealthCalls = (IEnumerable<DeviceHealthState>?)_eventBus.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(_eventBus.UpdateDeviceHealths)).GetArguments()?.First() ?? [];
        updateDeviceHealthCalls.Should().BeEquivalentTo(devices.Where(d => d.Health.DeviceId != deviceGuid));
        Received.InOrder(async () =>
        {
            await client.Received(1).SwitchoutletAsync(1234);
            await client.Received(1).SwitchoutletAsync(5678);
        });
        _context.PhotoTourEvents.Count().Should().Be(3);
    }

    [Fact]
    public async Task RestartDevice_MultipleSwitches_ShouldWork()
    {
        var deviceGuid = Guid.NewGuid().ToString();
        var switchDevice1 = Guid.NewGuid().ToString();
        var switchDevice2 = Guid.NewGuid().ToString();
        var sut = CreateDeviceRestarter();
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        var devices = new List<DeviceHealthState>() {
            new(new(default, switchDevice1, "testSwitcher1", HealthState.CanSwitchOutlets), 0, ""),
            new(new(default, switchDevice2, "testSwitcher2", HealthState.CanSwitchOutlets), 0, ""),
            new(new(default, deviceGuid, "faultyDevice", HealthState.ThermalCameraFunctional), 5, "")
        };
        _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
        var client = Substitute.For<ISwitchOutletsClient>();
        _deviceApi.SwitchOutletsClient("").ReturnsForAnyArgs(client);
        _context.DeviceSwitchAssociations.ReturnsForAnyArgs(new QueryableList<DeviceSwitchAssociation>()
        {
            new(){DeviceId=Guid.Parse(deviceGuid),OutletOffFkNavigation=new(){Code=1234},OutletOnFkNavigation=new(){ Code=5678} }
        });

        await sut.RestartDevice(deviceGuid, 1, "Test");
        await sut.RestartDevice(deviceGuid, 1, "Test");

        var updateDeviceHealthCalls = (IEnumerable<DeviceHealthState>?)_eventBus.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(_eventBus.UpdateDeviceHealths)).GetArguments()?.First() ?? [];
        updateDeviceHealthCalls.Should().BeEquivalentTo(devices.Where(d => d.Health.DeviceId != deviceGuid));
        Received.InOrder(async () =>
        {
            await client.Received(1).SwitchoutletAsync(1234);
            await client.Received(1).SwitchoutletAsync(1234);
            await client.Received(1).SwitchoutletAsync(5678);
            await client.Received(1).SwitchoutletAsync(5678);
        });
        _context.PhotoTourEvents.Count().Should().Be(5);
    }

    [Fact]
    public async Task RestartDevice_MultipleRestarts_ShouldRespectTimeout()
    {
        var deviceGuid = Guid.NewGuid().ToString();
        var switchDevice = Guid.NewGuid().ToString();
        var sut = CreateDeviceRestarter();
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs([new DeviceHealthState(new(default, switchDevice, "testSwitcher", HealthState.CanSwitchOutlets), 0, "")]);
        var client = Substitute.For<ISwitchOutletsClient>();
        _deviceApi.SwitchOutletsClient("").ReturnsForAnyArgs(client);
        _context.DeviceSwitchAssociations.ReturnsForAnyArgs(new QueryableList<DeviceSwitchAssociation>()
        {
            new(){DeviceId=Guid.Parse(deviceGuid),OutletOffFkNavigation=new(){Code=1234},OutletOnFkNavigation=new(){ Code=5678} }
        });

        await sut.RestartDevice(deviceGuid, 1, "Test");
        await sut.RestartDevice(deviceGuid, 1, "Test");
        await sut.RestartDevice(deviceGuid, 1, "Test");

        await client.ReceivedWithAnyArgs(2).SwitchoutletAsync(default);
        Received.InOrder(async () =>
        {
            await client.Received(1).SwitchoutletAsync(1234);
            await client.Received(1).SwitchoutletAsync(5678);
        });
    }

    [Fact]
    public async Task RestartDevice_InvalidGuid_ShouldBeLogged()
    {
        var sut = CreateDeviceRestarter();
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());

        await sut.RestartDevice("", 1, "Test");

        _context.PhotoTourEvents.Count().Should().Be(1);
        _context.PhotoTourEvents.First().Type.Should().Be(PhotoTourEventType.Error);
    }
}
