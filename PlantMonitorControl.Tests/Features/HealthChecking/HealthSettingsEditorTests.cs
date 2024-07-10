using FluentAssertions;
using NSubstitute;
using PlantMonitorControl.Features.HealthChecking;
using System;
using System.ComponentModel.Design;
using Xunit;

namespace PlantMonitorControl.Tests.Features.HealthChecking;

public class HealthSettingsEditorTests
{
    public HealthSettingsEditorTests()
    {
    }

    private HealthSettingsEditor CreateHealthSettingsEditor()
    {
        return new HealthSettingsEditor();
    }

    [Fact]
    public void GetHealth_ShouldWork()
    {
        var sut = CreateHealthSettingsEditor();
    }

    [Fact]
    public void UpdateHealthState_ShouldWork()
    {
        var sut = CreateHealthSettingsEditor();
        var emptyFlags = Enum.GetValues<HealthState>().Select(f => (Flag: f, Active: false));
        var newHealthState = sut.UpdateHealthState(emptyFlags.ToArray());
        newHealthState.State.Should().NotHaveFlag(HealthState.CanSwitchOutlets);
        newHealthState = sut.UpdateHealthState([(HealthState.CanSwitchOutlets, true)]);
        newHealthState.State.Should().HaveFlag(HealthState.CanSwitchOutlets);
        foreach (var flag in emptyFlags.Where(f => f.Flag != HealthState.CanSwitchOutlets && f.Flag > 0))
        {
            newHealthState.State.Should().NotHaveFlag(flag.Flag);
        }
    }

    [Fact]
    public void UpdateHealthState_NegativeFlags_ShouldBeCorrected()
    {
        var sut = CreateHealthSettingsEditor();
        var emptyFlags = Enum.GetValues<HealthState>().Select(f => (Flag: ~f, Active: true));
        var newHealthState = sut.UpdateHealthState(emptyFlags.ToArray());
        ((int)newHealthState.State).Should().Be(-1);
        newHealthState = sut.UpdateHealthState([(HealthState.CanSwitchOutlets, true)]);
        ((int)newHealthState.State).Should().BeGreaterThan(0);
        newHealthState.State.Should().HaveFlag(HealthState.CanSwitchOutlets);
        foreach (var flag in emptyFlags.Where(f => f.Flag != HealthState.CanSwitchOutlets && f.Flag > 0))
        {
            newHealthState.State.Should().NotHaveFlag(flag.Flag);
        }
    }
}
