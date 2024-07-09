using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Plantmonitor.Server.Tests.Features.AutomaticPhotoTourTests;

public class AutomaticPhotoTourWorkerTests
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IServiceScope _serviceScope = Substitute.For<IServiceScope>();
    private readonly IServiceProvider _provider = Substitute.For<IServiceProvider>();
    private readonly IDeviceConnectionEventBus _eventBus = Substitute.For<IDeviceConnectionEventBus>();
    private readonly IDeviceRestarter _restarter = Substitute.For<IDeviceRestarter>();
    private readonly IPictureDiskStreamer _pictureStreamer = Substitute.For<IPictureDiskStreamer>();
    private readonly IDataContext _context = Substitute.For<IDataContext>();
    private readonly IDeviceApiFactory _deviceApi = Substitute.For<IDeviceApiFactory>();

    public AutomaticPhotoTourWorkerTests()
    {
        _serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        _serviceScopeFactory.CreateScope().ReturnsForAnyArgs(_serviceScope);
        _serviceScope.ServiceProvider.ReturnsForAnyArgs(_provider);
        _provider.GetService(typeof(IDeviceConnectionEventBus)).Returns(_eventBus);
        _provider.GetService(typeof(IDataContext)).Returns(_context);
        _provider.GetService(typeof(IDeviceApiFactory)).Returns(_deviceApi);
        _provider.GetService(typeof(IPictureDiskStreamer)).Returns(_pictureStreamer);
        _provider.GetService(typeof(IDeviceRestarter)).Returns(_restarter);
        _context.CreatePhotoTourEventLogger(default)
            .ReturnsForAnyArgs((message, type) => _context.PhotoTourEvents.Add(new PhotoTourEvent() { Message = message, Type = type }));
    }

    private AutomaticPhotoTourWorker CreateAutomaticPhotoTourWorker()
    {
        return new AutomaticPhotoTourWorker(_serviceScopeFactory);
    }

    private void SchedulePhotoTrips(AutomaticPhotoTourWorker sut)
    {
        var sutMethod = sut.GetType().GetMethod(nameof(SchedulePhotoTrips), BindingFlags.NonPublic | BindingFlags.Instance);
        sutMethod!.Invoke(sut, []);
    }

    private async Task RunPhotoTrip(AutomaticPhotoTourWorker sut, long photoTourId)
    {
        var sutMethod = sut.GetType().GetMethod(nameof(RunPhotoTrip), BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)sutMethod!.Invoke(sut, [photoTourId])!;
    }

    [Fact]
    public void SchedulePhotoTrips_WithoutTrips_ShouldDoNothing()
    {
        var sut = CreateAutomaticPhotoTourWorker();

        _context.AutomaticPhotoTours.ReturnsForAnyArgs(new QueryableList<AutomaticPhotoTour>() { new() { Finished = true } });
        SchedulePhotoTrips(sut);

        _provider.ReceivedWithAnyArgs(1).GetService(default!);
    }

    [Fact]
    public async Task RunPhotoTrip_HealthyMultipleInParallel_ShouldCreateOneEmptyTrip()
    {
        var sut = CreateAutomaticPhotoTourWorker();

        _context.AutomaticPhotoTours.ReturnsForAnyArgs(new QueryableList<AutomaticPhotoTour>() { new() { Finished = true } });
        _context.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>());
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        _restarter.CheckDeviceHealth(default, default!, default!).ReturnsForAnyArgs((true, new()
        {
            Health = new DeviceHealth(null, Guid.NewGuid().ToString(), "device", HealthState.NoirCameraFunctional)
        }));
        await Task.WhenAll([RunPhotoTrip(sut, 1), RunPhotoTrip(sut, 1), RunPhotoTrip(sut, 1)]);

        _context.PhotoTourTrips.Count().Should().Be(1);
    }

    [Fact]
    public async Task RunPhotoTrip_UnhealthyMultipleInParallel_ShouldCreateOneEmptyTrip()
    {
        var sut = CreateAutomaticPhotoTourWorker();

        _context.AutomaticPhotoTours.ReturnsForAnyArgs(new QueryableList<AutomaticPhotoTour>() { new() { Finished = true } });
        _context.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>());
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        _restarter.CheckDeviceHealth(default, default!, default!).ReturnsForAnyArgs((false, new()
        {
            Health = new DeviceHealth(null, Guid.NewGuid().ToString(), "device", HealthState.NoirCameraFunctional)
        }));
        await Task.WhenAll([RunPhotoTrip(sut, 1), RunPhotoTrip(sut, 1), RunPhotoTrip(sut, 1)]);

        _context.PhotoTourTrips.Count().Should().Be(1);
    }
}
