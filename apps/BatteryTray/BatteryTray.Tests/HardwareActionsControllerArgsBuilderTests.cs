using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class HardwareActionsControllerArgsBuilderTests
{
    private static readonly Guid PlanA = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid PlanB = new("a1841308-3541-4fab-bc81-f71556f20b4a");
    private static readonly Guid PlanC = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

    [Fact]
    public void BuildCmdArgs_NoPlans_StillEndsWithSetactive()
    {
        var policy = new HardwareActionPolicy();

        var args = HardwareActionsController.BuildCmdArgs(Array.Empty<Guid>(), policy);

        args.Should().Be("/c \"powercfg /setactive SCHEME_CURRENT\"",
            because: "with zero plans we still refresh the active scheme so the call is a clean no-op rather than a malformed cmd line");
    }

    [Fact]
    public void BuildCmdArgs_OnePlan_DifferOnBatteryFalse_WritesAcEqualsDc()
    {
        var policy = new HardwareActionPolicy
        {
            PowerButton     = HardwareAction.Sleep,       // index 1
            LidClose        = HardwareAction.Hibernate,   // index 2
            DifferOnBattery = false,
            // OnBattery fields should be ignored when DifferOnBattery=false
            PowerButtonOnBattery = HardwareAction.ShutDown,
            LidCloseOnBattery    = HardwareAction.DoNothing,
        };

        var args = HardwareActionsController.BuildCmdArgs(new[] { PlanA }, policy);

        args.Should().Be(
            "/c \"powercfg /setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 1 && " +
            "powercfg /setdcvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 1 && " +
            "powercfg /setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 2 && " +
            "powercfg /setdcvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 2 && " +
            "powercfg /setactive SCHEME_CURRENT\"");
    }

    [Fact]
    public void BuildCmdArgs_OnePlan_DifferOnBatteryTrue_WritesDistinctAcDc()
    {
        var policy = new HardwareActionPolicy
        {
            PowerButton          = HardwareAction.Sleep,       // AC index 1
            LidClose             = HardwareAction.Sleep,       // AC index 1
            DifferOnBattery      = true,
            PowerButtonOnBattery = HardwareAction.Hibernate,   // DC index 2
            LidCloseOnBattery    = HardwareAction.ShutDown,    // DC index 3
        };

        var args = HardwareActionsController.BuildCmdArgs(new[] { PlanA }, policy);

        args.Should().Contain("/setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 1");
        args.Should().Contain("/setdcvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 2");
        args.Should().Contain("/setacvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 1");
        args.Should().Contain("/setdcvalueindex 381b4222-f694-41f0-9685-ff5bb260df2e 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 3");
        args.Should().EndWith("powercfg /setactive SCHEME_CURRENT\"");
    }

    [Fact]
    public void BuildCmdArgs_ThreePlans_EmitsTwelveValueWritesPlusSetactive()
    {
        var policy = new HardwareActionPolicy();

        var args = HardwareActionsController.BuildCmdArgs(new[] { PlanA, PlanB, PlanC }, policy);

        // 3 plans * (2 actions * 2 power-states) = 12 value writes + 1 setactive
        var setAcCount = System.Text.RegularExpressions.Regex.Matches(args, "/setacvalueindex").Count;
        var setDcCount = System.Text.RegularExpressions.Regex.Matches(args, "/setdcvalueindex").Count;
        var setactiveCount = System.Text.RegularExpressions.Regex.Matches(args, "/setactive ").Count;

        setAcCount.Should().Be(6,    because: "3 plans * 2 actions (power button + lid)");
        setDcCount.Should().Be(6,    because: "3 plans * 2 actions (power button + lid)");
        setactiveCount.Should().Be(1, because: "one trailing /setactive refreshes the live state");
    }

    [Theory]
    [InlineData(HardwareAction.DoNothing,      0)]
    [InlineData(HardwareAction.Sleep,          1)]
    [InlineData(HardwareAction.Hibernate,      2)]
    [InlineData(HardwareAction.ShutDown,       3)]
    [InlineData(HardwareAction.TurnOffDisplay, 4)]
    public void BuildCmdArgs_EmitsExpectedIntegerIndexForEachAction(HardwareAction action, int expectedIndex)
    {
        var policy = new HardwareActionPolicy
        {
            PowerButton = action,
            LidClose    = action,
        };

        var args = HardwareActionsController.BuildCmdArgs(new[] { PlanA }, policy);

        args.Should().Contain($"7648efa3-dd9c-4e3e-b566-50f929386280 {expectedIndex}",
            because: $"PowerButton={action} must serialise to AC index {expectedIndex}");
        args.Should().Contain($"5ca83367-6e45-459f-a27b-476b1d01c936 {expectedIndex}",
            because: $"LidClose={action} must serialise to AC index {expectedIndex}");
    }
}
