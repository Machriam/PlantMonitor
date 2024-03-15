using FluentAssertions;
using NSubstitute;
using Plantmonitor.Server.Features.DeviceConfiguration;
using System;
using Xunit;

namespace Plantmonitor.Server.Tests.Features.DeviceConfiguration
{
    public class StringExtensionsTests
    {
        [Theory]
        [InlineData("192.168.0.100", "192.168.0.105",
            "192.168.0.100", "192.168.0.101", "192.168.0.102", "192.168.0.103", "192.168.0.104", "192.168.0.105")]
        public void CreateIPRange_ShouldWork(string from, string to, params string[] expected)
        {
            from.ToIpRange(to).Should().BeEquivalentTo(expected);
        }
    }
}
