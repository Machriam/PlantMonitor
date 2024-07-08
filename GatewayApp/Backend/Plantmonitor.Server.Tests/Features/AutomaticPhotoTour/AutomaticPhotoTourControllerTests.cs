using FluentAssertions;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using Plantmonitor.DataModel.DataModel;
using Plantmonitor.Server.Features.AutomaticPhotoTour;
using Plantmonitor.Server.Features.DeviceConfiguration;
using Plantmonitor.Server.Features.DeviceControl;

namespace Plantmonitor.Server.Tests.Features.AutomaticPhotoTour12
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
        public void StopPhotoTour_ShouldWork()
        {
            var result = new QueryableList<AutomaticPhotoTour>() { new() { Id = 1, Finished = false } };
            var events = new QueryableList<PhotoTourEvent>();
            _context.AutomaticPhotoTours.ReturnsForAnyArgs(result);
            _context.PhotoTourEvents.ReturnsForAnyArgs(events);
            var sut = CreateAutomaticPhotoTourController();
            sut.StopPhotoTour(1);
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
            await sut.StartAutomaticTour(new AutomaticPhotoTourController.AutomaticTourStartInfo(1, 1, [], "comment", "name", DeviceId));
            photoTours.Count.Should().Be(1);
            photoTours.First().DeviceId.Should().Be(DeviceId);
            photoTours.First().Comment.Should().Be("comment");
            photoTours.First().TemperatureMeasurements.Should().HaveCount(0);
            photoTours.First().Finished.Should().BeFalse();
        }
    }
}
