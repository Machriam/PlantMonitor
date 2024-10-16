using FluentAssertions;
using NSubstitute;
using Plantmonitor.Shared.Extensions;
using System;
using Xunit;

namespace Plantmonitor.Server.Tests.Extensions;

public class EnumerableExtensionsTests
{
    [Fact]
    public void OrderByNumericString_ShouldWork()
    {
        var list = new[] { "Test1", "Test3", "Test999", "ATest2", "est1", "est99", "Atest1" };
        var selectCounter = 0;
        var result = list.OrderByNumericString(x => { selectCounter++; return x; });
        result.Should()
              .BeEquivalentTo(["Atest1", "ATest2", "est1", "est99", "Test1", "Test3", "Test999"]);
        selectCounter.Should().Be(list.Length);
    }
}
