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
    private readonly IDeviceApiFactory _deviceApi = Substitute.For<IDeviceApiFactory>();

    public DeviceRestarterTests()
    {
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScopeFactory.CreateScope().ReturnsForAnyArgs(_serviceScope);
        _serviceScope.ServiceProvider.ReturnsForAnyArgs(_provider);
        _provider.GetService(typeof(IDeviceConnectionEventBus)).Returns(_eventBus);
        _provider.GetService(typeof(IDataContext)).Returns(_context);
        _provider.GetService(typeof(IDeviceApiFactory)).Returns(_deviceApi);
        _context.CreatePhotoTourEventLogger(default).ReturnsForAnyArgs((message, type) => _context.PhotoTourEvents.Add(new PhotoTourEvent() { Message = message, Type = type }));
    }

    private DeviceRestarter CreateDeviceRestarter()
    {
        return new DeviceRestarter(_serviceScopeFactory);
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

        await sut.RestartDevice(deviceGuid, 1);

        _eventBus.DidNotReceiveWithAnyArgs().UpdateDeviceHealths(default!);
        await client.DidNotReceiveWithAnyArgs().SwitchoutletAsync(1234);
        _context.PhotoTourEvents.Count().Should().Be(1);
        _context.PhotoTourEvents.First().Type.Should().Be(PhotoTourEventType.Warning);
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

        await sut.RestartDevice(deviceGuid, 1);

        var updateDeviceHealthCalls = (IEnumerable<DeviceHealthState>?)_eventBus.ReceivedCalls()
            .First(c => c.GetMethodInfo().Name == nameof(_eventBus.UpdateDeviceHealths)).GetArguments()?.First() ?? [];
        updateDeviceHealthCalls.Should().BeEquivalentTo(devices.Where(d => d.Health.DeviceId != deviceGuid));
        Received.InOrder(async () =>
        {
            await client.Received(1).SwitchoutletAsync(1234);
            await client.Received(1).SwitchoutletAsync(5678);
        });
        _context.PhotoTourEvents.Count().Should().Be(0);
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

        await sut.RestartDevice(deviceGuid, 1);

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
        _context.PhotoTourEvents.Count().Should().Be(0);
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

        await sut.RestartDevice(deviceGuid, 1);
        await sut.RestartDevice(deviceGuid, 1);
        await sut.RestartDevice(deviceGuid, 1);

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

        await sut.RestartDevice("", 1);

        _context.PhotoTourEvents.Count().Should().Be(1);
        _context.PhotoTourEvents.First().Type.Should().Be(PhotoTourEventType.Error);
    }
}
