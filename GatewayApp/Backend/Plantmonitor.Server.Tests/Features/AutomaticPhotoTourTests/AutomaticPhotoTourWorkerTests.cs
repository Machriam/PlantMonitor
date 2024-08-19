using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Extensions;
using NSubstitute;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;
using Plantmonitor.Shared.Extensions;
using Plantmonitor.Shared.Features.ImageStreaming;
using System.Reflection;
using static Plantmonitor.Server.Features.AutomaticPhotoTour.DeviceRestarter;

namespace Plantmonitor.Server.Tests.Features.AutomaticPhotoTourTests;

public class AutomaticPhotoTourWorkerTests
{
    private readonly int _ffcTimeout = 50;
    private readonly int _positionCheckTimeout = 10;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IServiceScope _serviceScope = Substitute.For<IServiceScope>();
    private readonly IServiceProvider _provider = Substitute.For<IServiceProvider>();
    private readonly IDeviceConnectionEventBus _eventBus = Substitute.For<IDeviceConnectionEventBus>();
    private readonly IDeviceRestarter _restarter = Substitute.For<IDeviceRestarter>();
    private readonly IPictureDiskStreamer _pictureStreamer = Substitute.For<IPictureDiskStreamer>();
    private readonly IMotorMovementClient _motorClient = Substitute.For<IMotorMovementClient>();
    private readonly IIrImageTakingClient _irClient = Substitute.For<IIrImageTakingClient>();
    private readonly IVisImageTakingClient _visClient = Substitute.For<IVisImageTakingClient>();
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
        _provider.GetService(typeof(IIrImageTakingClient)).Returns(_irClient);
        _provider.GetService(typeof(IVisImageTakingClient)).Returns(_visClient);
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

    private async Task<(string IrFolder, string VisFolder)> TakePhotos(AutomaticPhotoTourWorker sut, long photoTourId,
        IDataContext context, IPictureDiskStreamer streamer, IDeviceApiFactory deviceApi, DeviceHealthState deviceHealth)
    {
        var sutMethod = sut.GetType().GetMethod(nameof(TakePhotos), BindingFlags.NonPublic | BindingFlags.Instance);
        return await (Task<(string IrFolder, string VisFolder)>)sutMethod!.Invoke(sut, [photoTourId, context, streamer, streamer, deviceApi, deviceHealth])!;
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
        _restarter.CheckDeviceHealth(default, default!, default!).ReturnsForAnyArgs(new DeviceHealthResult(true, new()
        {
            Health = new DeviceHealth(null, Guid.NewGuid().ToString(), "device", HealthState.NoirCameraFunctional)
        }, true));
        await Task.WhenAll([RunPhotoTrip(sut, 1), RunPhotoTrip(sut, 1), RunPhotoTrip(sut, 1)]);

        _context.PhotoTourTrips.Count().Should().Be(1);
    }

    [Fact]
    public async Task TakePhotos_DefaultUseCase_ShouldWork()
    {
        var sut = CreateAutomaticPhotoTourWorker();
        sut.GetType().GetField(nameof(_ffcTimeout), BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(sut, _ffcTimeout);
        sut.GetType().GetField(nameof(_positionCheckTimeout), BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(sut, _positionCheckTimeout);

        _context.AutomaticPhotoTours.ReturnsForAnyArgs(new QueryableList<AutomaticPhotoTour>() { new() { Finished = true } });
        _context.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>());
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        var deviceHealth = new DeviceHealthState() { Health = new DeviceHealth(null, Guid.NewGuid().ToString(), "device", HealthState.NoirCameraFunctional) };
        _context.DeviceMovements.ReturnsForAnyArgs(new QueryableList<DeviceMovement>() {
            new() { DeviceId= Guid.Parse( deviceHealth.Health.DeviceId!),MovementPlan=new(){
                StepPoints=[new MovementPoint(200,100,100,"1"),new MovementPoint(-400,100,100,"2")]
            } } });
        _deviceApi.MovementClient("").ReturnsForAnyArgs(_motorClient);
        _deviceApi.IrImageTakingClient("").ReturnsForAnyArgs(_irClient);
        _deviceApi.VisImageTakingClient("").ReturnsForAnyArgs(_visClient);
        _motorClient.CurrentpositionAsync().ReturnsForAnyArgs(new MotorPosition(false, true, 1000));
        _restarter.CheckDeviceHealth(default, default!, default!).ReturnsForAnyArgs(new DeviceHealthResult(false, deviceHealth, true));
        _pictureStreamer.StreamingFinished().ReturnsForAnyArgs(true);

        var visFolder = "";
        var irFolder = "";
        async Task PhotoTaking()
        {
            (irFolder, visFolder) = await TakePhotos(sut, 1, _context, _pictureStreamer, _deviceApi, deviceHealth);
        }
        PhotoTaking().RunInBackground(ex => throw ex);
        await Task.Delay(100);
        await _motorClient.Received().MovemotorAsync(-1000, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), (int)(200 * 1.05f), (int)(-200 * 1.05f));
        await _motorClient.Received().MovemotorAsync(200, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), (int)(200 * 1.05f), (int)(-200 * 1.05f));
        await _motorClient.ReceivedWithAnyArgs(2).MovemotorAsync(default, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), default, default);
        await _irClient.DidNotReceive().RunffcAsync();
        var calls = _pictureStreamer.ReceivedCalls();
        var irCall = calls.First(c => ((CameraTypeInfo)c.GetArguments()[2]!).SignalRMethod == CameraType.IR.GetAttributeOfType<CameraTypeInfo>().SignalRMethod);
        var visCall = calls.First(c => ((CameraTypeInfo)c.GetArguments()[2]!).SignalRMethod == CameraType.Vis.GetAttributeOfType<CameraTypeInfo>().SignalRMethod);
        ((Action<string>)irCall.GetArguments()[4]!).Invoke("irfolder");
        ((Action<string>)visCall.GetArguments()[4]!).Invoke("visfolder");
        await ((Func<CameraStreamFormatter, Task>)irCall.GetArguments()[5]!).Invoke(new CameraStreamFormatter() { Steps = 200 });
        await ((Func<CameraStreamFormatter, Task>)visCall.GetArguments()[5]!).Invoke(new CameraStreamFormatter() { Steps = 200 });
        await Task.Delay(2 * _positionCheckTimeout);
        await _irClient.Received(1).RunffcAsync();
        await _motorClient.ReceivedWithAnyArgs(2).MovemotorAsync(default, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), default, default);
        await Task.Delay(2 * _ffcTimeout);
        await _motorClient.ReceivedWithAnyArgs(3).MovemotorAsync(default, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), default, default);
        await _motorClient.Received().MovemotorAsync(-400, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<int>(), (int)(200 * 1.05f), (int)(-200 * 1.05f));
        await ((Func<CameraStreamFormatter, Task>)irCall.GetArguments()[5]!).Invoke(new CameraStreamFormatter() { Steps = -200 });
        await ((Func<CameraStreamFormatter, Task>)visCall.GetArguments()[5]!).Invoke(new CameraStreamFormatter() { Steps = -200 });
        await _visClient.DidNotReceive().KillcameraAsync();
        await _irClient.DidNotReceive().KillcameraAsync();
        visFolder.Should().BeEmpty();
        irFolder.Should().BeEmpty();
        await Task.Delay(2 * _positionCheckTimeout);
        await _irClient.Received(2).RunffcAsync();
        await Task.Delay(2 * _ffcTimeout);
        await Task.Delay(2 * _positionCheckTimeout);
        await _irClient.Received(1).KillcameraAsync();
        await _visClient.Received(1).KillcameraAsync();

        _context.PhotoTourEvents.Count().Should().Be(8);
        visFolder.Should().Be("visfolder");
        irFolder.Should().Be("irfolder");
    }

    [Fact]
    public async Task TakePhotos_WithoutMovementPlan_ShouldLogError()
    {
        var sut = CreateAutomaticPhotoTourWorker();

        _context.AutomaticPhotoTours.ReturnsForAnyArgs(new QueryableList<AutomaticPhotoTour>() { new() { Finished = true } });
        _context.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>());
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        var deviceHealth = new DeviceHealthState() { Health = new DeviceHealth(null, Guid.NewGuid().ToString(), "device", HealthState.NoirCameraFunctional) };
        _context.DeviceMovements.ReturnsForAnyArgs(new QueryableList<DeviceMovement>() { });

        await TakePhotos(sut, 1, _context, _pictureStreamer, _deviceApi, deviceHealth);
        _context.PhotoTourEvents.Count().Should().Be(1);
        _context.PhotoTourEvents.First().Type.Should().Be(PhotoTourEventType.Error);
        _context.PhotoTourEvents.First().Message.Should().Contain("No Movementplan found.");
    }

    [Fact]
    public async Task RunPhotoTrip_UnhealthyMultipleInParallel_ShouldCreateOneEmptyTrip()
    {
        var sut = CreateAutomaticPhotoTourWorker();

        _context.AutomaticPhotoTours.ReturnsForAnyArgs(new QueryableList<AutomaticPhotoTour>() { new() { Finished = true } });
        _context.PhotoTourTrips.ReturnsForAnyArgs(new QueryableList<PhotoTourTrip>());
        _context.PhotoTourEvents.ReturnsForAnyArgs(new QueryableList<PhotoTourEvent>());
        _restarter.CheckDeviceHealth(default, default!, default!).ReturnsForAnyArgs(new DeviceHealthResult(false, new()
        {
            Health = new DeviceHealth(null, Guid.NewGuid().ToString(), "device", HealthState.NoirCameraFunctional)
        }, true));
        await Task.WhenAll([RunPhotoTrip(sut, 1), RunPhotoTrip(sut, 1), RunPhotoTrip(sut, 1)]);

        _context.PhotoTourTrips.Count().Should().Be(1);
    }
}
