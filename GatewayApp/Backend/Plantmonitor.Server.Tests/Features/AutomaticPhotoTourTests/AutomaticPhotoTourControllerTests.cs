using FluentAssertions;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;
using static Plantmonitor.Server.Features.AutomaticPhotoTour.AutomaticPhotoTourController;

namespace Plantmonitor.Server.Tests.Features.AutomaticPhotoTourTests
{
    public class AutomaticPhotoTourControllerTests
    {
        private readonly IDataContext _context;
        private readonly IDeviceConnectionEventBus _eventBus;
        private readonly IDeviceApiFactory _deviceApi;

        public AutomaticPhotoTourControllerTests()
        {
            _context = Substitute.For<IDataContext>();
            _eventBus = Substitute.For<IDeviceConnectionEventBus>();
            _deviceApi = Substitute.For<IDeviceApiFactory>();
        }

        private AutomaticPhotoTourController CreateAutomaticPhotoTourController()
        {
            return new AutomaticPhotoTourController(_context, _eventBus, _deviceApi);
        }

        [Fact]
        public async Task StopPhotoTour_ShouldWork()
        {
            var result = new QueryableList<AutomaticPhotoTour>() { new() { Id = 1, Finished = false } };
            var events = new QueryableList<PhotoTourEvent>();
            _context.AutomaticPhotoTours.ReturnsForAnyArgs(result);
            _context.PhotoTourEvents.ReturnsForAnyArgs(events);
            var sut = CreateAutomaticPhotoTourController();

            await sut.PausePhotoTour(1, true, 5);

            _context.Received(1).SaveChanges();
            result.First().Finished.Should().BeTrue();
            events.Count.Should().Be(1);
            events.First().Message.Should().Be("Photo tour finished");
        }

        [Fact]
        public async Task StartAutomaticTour_NoTemperatureDevices_ShouldWork()
        {
            var sut = CreateAutomaticPhotoTourController();
            const string DeviceId = "ee0d41ca-10f0-4807-b8f3-55546adc1278";
            var devices = new List<DeviceHealthState>() { new(new(null, DeviceId, "test", HealthState.NoirCameraFunctional), 0, "") };
            var photoTours = new QueryableList<AutomaticPhotoTour>();
            _context.AutomaticPhotoTours.ReturnsForAnyArgs(photoTours);
            _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
            _context.DeviceMovements.ReturnsForAnyArgs(new QueryableList<DeviceMovement>() { new() { Id = 1 } });

            await sut.StartAutomaticTour(new AutomaticTourStartInfo(1, 1, [], "comment", "name", DeviceId, true));

            photoTours.Count.Should().Be(1);
            photoTours.First().DeviceId.Should().Be(DeviceId);
            photoTours.First().Comment.Should().Be("comment");
            photoTours.First().TemperatureMeasurements.Should().HaveCount(0);
            photoTours.First().Finished.Should().BeFalse();
            _context.Received(1).SaveChanges();
        }

        [Fact]
        public async Task StartAutomaticTour_WithTemperatureDevicesNotAvailable_ShouldThrow()
        {
            var sut = CreateAutomaticPhotoTourController();
            const string DeviceId = "ee0d41ca-10f0-4807-b8f3-55546adc1278";
            const string Temp1 = "ef0d41ca-10f0-4807-b8f3-55546adc1278";
            const string Temp2 = "ed0d41ca-10f0-4807-b8f3-55546adc1278";
            var devices = new List<DeviceHealthState>() { new(new(null, DeviceId, "test", HealthState.NoirCameraFunctional), 0, "") };
            var photoTours = new QueryableList<AutomaticPhotoTour>();
            _context.AutomaticPhotoTours.ReturnsForAnyArgs(photoTours);
            _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
            _context.DeviceMovements.ReturnsForAnyArgs(new QueryableList<DeviceMovement>() { new() { Id = 1 } });

            Func<Task> action = () => sut.StartAutomaticTour(new AutomaticTourStartInfo(1, 1, [new(Temp1, "Temp1"), new(Temp2, "Temp2")], "comment", "name", DeviceId, true));
            await action.Should().ThrowAsync<Exception>().WithMessage("*Not all requested temperature measurement devices are available*");
        }

        [Fact]
        public async Task StartAutomaticTour_WithTemperatureDevicesAvailableButNotCallable_ShouldThrow()
        {
            var sut = CreateAutomaticPhotoTourController();
            const string DeviceId = "ee0d41ca-10f0-4807-b8f3-55546adc1278";
            const string Temp1 = "ef0d41ca-10f0-4807-b8f3-55546adc1278";
            const string Temp2 = "ed0d41ca-10f0-4807-b8f3-55546adc1278";
            var devices = new List<DeviceHealthState>() {
                new(new(null, DeviceId, "test", HealthState.NoirCameraFunctional), 0, ""),
                new(new(null, Temp1, "tempDev1", HealthState.NA), 0, ""),
                new(new(null, Temp2, "tempDev2", HealthState.CanSwitchOutlets), 0, ""),
            };
            var photoTours = new QueryableList<AutomaticPhotoTour>();
            _context.AutomaticPhotoTours.ReturnsForAnyArgs(photoTours);
            _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
            _context.DeviceMovements.ReturnsForAnyArgs(new QueryableList<DeviceMovement>() { new() { Id = 1 } });

            Func<Task> action = () => sut.StartAutomaticTour(new AutomaticTourStartInfo(1, 1, [new(Temp1, "Temp1"), new(Temp2, "Temp2")], "comment", "name", DeviceId, true));
            await action.Should().ThrowAsync<Exception>().WithMessage("*tempDev1 has no temperature sensor*tempDev2 has no temperature sensor*");
        }

        [Fact]
        public async Task StartAutomaticTour_WithTemperatureDevicesAvailableAndCallable_ShouldWork()
        {
            var sut = CreateAutomaticPhotoTourController();
            const string DeviceId = "ee0d41ca-10f0-4807-b8f3-55546adc1278";
            const string Temp1 = "ef0d41ca-10f0-4807-b8f3-55546adc1278";
            const string Temp2 = "ed0d41ca-10f0-4807-b8f3-55546adc1278";
            var devices = new List<DeviceHealthState>() {
                new(new(null, DeviceId, "test", HealthState.NoirCameraFunctional | HealthState.ThermalCameraFunctional), 0, ""),
                new(new(null, Temp1, "tempDev1", HealthState.NA), 0, ""),
                new(new(null, Temp2, "tempDev2", HealthState.CanSwitchOutlets), 0, ""),
            };
            var photoTours = new QueryableList<AutomaticPhotoTour>();
            var temperatureClient = Substitute.For<ITemperatureClient>();
            temperatureClient.DevicesAsync().ReturnsForAnyArgs(["0x4a"]);
            _deviceApi.TemperatureClient("").ReturnsForAnyArgs(temperatureClient);
            _context.AutomaticPhotoTours.ReturnsForAnyArgs(photoTours);
            _eventBus.GetDeviceHealthInformation().ReturnsForAnyArgs(devices);
            _context.DeviceMovements.ReturnsForAnyArgs(new QueryableList<DeviceMovement>() { new() { Id = 1 } });

            await sut.StartAutomaticTour(new AutomaticTourStartInfo(1, 1, [new(Temp1, "Temp1"), new(Temp2, "Temp2")], "comment", "name", DeviceId, true));

            photoTours.Count.Should().Be(1);
            photoTours.First().DeviceId.Should().Be(DeviceId);
            photoTours.First().Comment.Should().Be("comment");
            photoTours.First().TemperatureMeasurements.Should().HaveCount(3);
            photoTours.First().Finished.Should().BeFalse();
            _context.Received(1).SaveChanges();
        }
    }
}
