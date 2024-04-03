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
        [InlineData(80, 10, 2000, 0, 1994)]
        [InlineData(80, 10, 2000, 20, 1495)]
        [InlineData(80, 10, 2000, 30, 429)]
        [InlineData(80, 10, 2000, 39, 69)]
        [InlineData(80, 10, 2000, 40, 69)]
        [InlineData(80, 10, 2000, 41, 69)]
        [InlineData(80, 10, 2000, 49, 355)]
        [InlineData(80, 10, 2000, 50, 429)]
        [InlineData(80, 10, 2000, 51, 514)]
        [InlineData(80, 10, 2000, 80, 1994)]
        [InlineData(200, 10, 2000, 0, 1994)]
        [InlineData(200, 10, 2000, 1, 1992)]
        [InlineData(200, 10, 2000, 49, 15)]
        [InlineData(200, 10, 2000, 50, 15)]
        [InlineData(200, 10, 2000, 51, 15)]
        [InlineData(200, 10, 2000, 100, 15)]
        [InlineData(200, 10, 2000, 149, 15)]
        [InlineData(200, 10, 2000, 150, 15)]
        [InlineData(200, 10, 2000, 151, 15)]
        [InlineData(200, 10, 2000, 199, 1992)]
        [InlineData(20, 10, 2000, 0, 1994, 5)]
        [InlineData(20, 10, 2000, 5, 57, 5)]
        [InlineData(20, 10, 2000, 10, 57, 5)]
        [InlineData(20, 10, 2000, 15, 57, 5)]
        [InlineData(20, 10, 2000, 19, 1940, 5)]
        [InlineData(20, 10, 2000, 0, 1994, 1000)]
        [InlineData(20, 10, 2000, 10, 1993, 1000)]
        [InlineData(20, 10, 2000, 19, 1993, 1000)]
        public void CreateRampFunction_StateUnderTest_ExpectedBehavior(int steps, int minTime, int maxTime, int input, int expected, int rampLength = 50)
        {
            var rampFunction = steps.CreateLogisticRampFunction(minTime, maxTime, rampLength);
            rampFunction(input).Should().Be(expected);
        }
    }
}