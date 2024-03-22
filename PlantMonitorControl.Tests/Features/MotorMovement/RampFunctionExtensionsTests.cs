using FluentAssertions;
using NSubstitute;
using PlantMonitorControl.Features.MotorMovement;
using System;
using Xunit;

namespace PlantMonitorControl.Tests.Features.MotorMovement
{
    public class RampFunctionExtensionsTests
    {
        [Theory]
        [InlineData(100, 1, 10, 20, 6)]
        [InlineData(100, 1, 10, 0, 10)]
        [InlineData(100, 1, 10, 50, 1)]
        [InlineData(100, 1, 10, 80, 6)]
        [InlineData(100, 1, 10, 99, 10)]
        [InlineData(200, 1, 10, 149, 1)]
        [InlineData(200, 1, 10, 100, 1)]
        [InlineData(200, 1, 10, 51, 1)]
        [InlineData(40, 0, 40, 0, 40)]
        [InlineData(40, 0, 40, 1, 39)]
        [InlineData(40, 0, 40, 10, 32)]
        [InlineData(40, 0, 40, 19, 24)]
        [InlineData(40, 0, 40, 20, 24)]
        [InlineData(40, 0, 40, 39, 40)]
        public void CreateRampFunction_StateUnderTest_ExpectedBehavior(int steps, int minTime, int maxTime, int input, int expected)
        {
            var rampFunction = steps.CreateRampFunction(minTime, maxTime);
            rampFunction(input).Should().Be(expected);
        }
    }
}