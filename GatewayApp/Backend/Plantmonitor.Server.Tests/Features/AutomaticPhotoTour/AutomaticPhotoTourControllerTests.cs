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
        private readonly IDataContext _subDataContext;
        private readonly IDeviceConnectionEventBus _subDeviceConnectionEventBus;
        private readonly IDeviceApiFactory _subDeviceApiFactory;

        public AutomaticPhotoTourControllerTests()
        {
            _subDataContext = Substitute.For<IDataContext>();
            _subDeviceConnectionEventBus = Substitute.For<IDeviceConnectionEventBus>();
            _subDeviceApiFactory = Substitute.For<IDeviceApiFactory>();
        }

        private AutomaticPhotoTourController CreateAutomaticPhotoTourController()
        {
            return new AutomaticPhotoTourController(_subDataContext, _subDeviceConnectionEventBus, _subDeviceApiFactory);
        }

        [Fact]
        public void StopPhotoTour_StateUnderTest_ExpectedBehavior()
        {
            var result = new QueryableList<AutomaticPhotoTour>() { new() { Id = 1, Finished = false } };
            var events = new QueryableList<PhotoTourEvent>();
            _subDataContext.AutomaticPhotoTours.ReturnsForAnyArgs(result);
            _subDataContext.PhotoTourEvents.ReturnsForAnyArgs(events);
            var sut = CreateAutomaticPhotoTourController();
            sut.StopPhotoTour(1);
            _subDataContext.Received(1).SaveChanges();
            result.First().Finished.Should().BeTrue();
            events.Count.Should().Be(1);
            events.First().Message.Should().Be("Photo tour finished");
        }

        [Fact]
        public async Task StartAutomaticTour_StateUnderTest_ExpectedBehavior()
        {
            var sut = CreateAutomaticPhotoTourController();
            await sut.StartAutomaticTour(new AutomaticPhotoTourController.AutomaticTourStartInfo());
        }
    }
}
