using FluentAssertions;
using NSubstitute;
using PlantMonitorControl.Features.MotorMovement;
using System;
using Xunit;

namespace PlantMonitorControl.Tests.Features.MotorMovement
{
    public class MotorPositionCalculatorTests
    {
        public MotorPositionCalculatorTests()
        {
        }

        private MotorPositionCalculator CreateMotorPositionCalculator()
        {
            return new MotorPositionCalculator();
        }

        [Fact]
        public async Task StepForTime_Default_ShouldWork()
        {
            var motorPositionCalculator = CreateMotorPositionCalculator();
            motorPositionCalculator.ResetHistory();
            var positionByTime = new List<(long Time, int Steps)>();
            for (var i = 0; i < 20; i++)
            {
                await Task.Delay(1);
                var step = Random.Shared.Next(-100, 100);
                var time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                motorPositionCalculator.UpdatePosition(step);
                positionByTime.Add(new(time, positionByTime.LastOrDefault().Steps + step));
            }
            foreach (var (time, steps) in positionByTime) motorPositionCalculator.StepForTime(time).Should().Be(steps);
        }

        [Fact]
        public async Task StepForTime_OutOfBonds_ShouldWork()
        {
            var motorPositionCalculator = CreateMotorPositionCalculator();
            motorPositionCalculator.ResetHistory();
            var positionByTime = new List<(long Time, int Steps)>();
            motorPositionCalculator.StepForTime(long.MinValue).Should().Be(0);
            for (var i = 0; i < 5; i++)
            {
                await Task.Delay(5);
                var step = Random.Shared.Next(-100, 100);
                var time = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeMilliseconds();
                motorPositionCalculator.UpdatePosition(step);
                positionByTime.Add(new(time, positionByTime.LastOrDefault().Steps + step));
            }
            var testTime = ((positionByTime[3].Time - positionByTime[2].Time) / 2) + positionByTime[2].Time;
            motorPositionCalculator.StepForTime(long.MinValue).Should().Be(0);
            motorPositionCalculator.StepForTime(testTime).Should().Be(positionByTime[2].Steps);
            motorPositionCalculator.StepForTime(long.MaxValue).Should().Be(positionByTime[^1].Steps);
        }
    }
}
