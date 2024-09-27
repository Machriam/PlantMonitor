using Castle.Core.Logging;
using FluentAssertions;
using NSubstitute;
using PlantMonitorControl.Features.AppsettingsConfiguration;
using PlantMonitorControl.Features.MotorMovement;
using System;

namespace PlantMonitorControl.Tests.Features.MotorMovement
{
    public class MotorPositionCalculatorTests
    {
        private IGpioInterop _controller = default!;

        private MotorPositionCalculator CreateMotorPositionCalculator()
        {
            var factory = Substitute.For<IGpioInteropFactory>();
            _controller = Substitute.For<IGpioInterop>();
            factory.Create().ReturnsForAnyArgs(_controller);
            return new MotorPositionCalculator(Substitute.For<IEnvironmentConfiguration>(), factory, Substitute.For<Microsoft.Extensions.Logging.ILogger<MotorPositionCalculator>>());
        }

        [Fact]
        public async Task EmergencyStop_ShouldWork()
        {
            var sut = CreateMotorPositionCalculator();
            sut.ZeroPosition();
            var initialPosition = sut.CurrentPosition();
            await sut.MoveMotor(3000, 10, 20, 5000, 2000, 1000);
            initialPosition.Engaged.Should().BeTrue();
            initialPosition.Position.Should().Be(0);
            sut.CurrentPosition().Engaged.Should().BeFalse();
            sut.CurrentPosition().Position.Should().Be(0);
            sut.ToggleMotorEngage(true);
            await sut.MoveMotor(3000, 10, 20, 5000, 2500, 0);
            sut.CurrentPosition().Engaged.Should().BeFalse();
            sut.CurrentPosition().Position.Should().Be(2501);
        }

        [Fact]
        public async Task StepForTime_Default_ShouldWork()
        {
            var sut = CreateMotorPositionCalculator();
            sut.ZeroPosition();
            sut.ResetHistory();
            sut.ToggleMotorEngage(true);
            var positionByTime = new List<(long Time, int Steps)>();
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(1);
                var step = Random.Shared.Next(-100, 100);
                var time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                sut.UpdatePosition(step, int.MaxValue, int.MinValue);
                positionByTime.Add(new(time, positionByTime.LastOrDefault().Steps + step));
            }
            foreach (var (time, steps) in positionByTime) sut.StepForTime(time).Should().Be(steps);
        }

        [Fact]
        public async Task StepForTime_OutOfBonds_ShouldWork()
        {
            var sut = CreateMotorPositionCalculator();
            sut.ZeroPosition();
            sut.ResetHistory();
            sut.ToggleMotorEngage(true);
            var positionByTime = new List<(long Time, int Steps)>();
            sut.StepForTime(long.MinValue).Should().Be(0);
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(5);
                var step = Random.Shared.Next(-100, 100);
                var time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                sut.UpdatePosition(step, int.MaxValue, int.MinValue);
                positionByTime.Add(new(time, positionByTime.LastOrDefault().Steps + step));
            }
            var testTime = ((positionByTime[3].Time - positionByTime[2].Time) / 2) + positionByTime[2].Time;
            sut.StepForTime(long.MinValue).Should().Be(0);
            sut.StepForTime(testTime).Should().Be(positionByTime[2].Steps);
            sut.StepForTime(long.MaxValue).Should().Be(positionByTime[^1].Steps);
        }
    }
}
