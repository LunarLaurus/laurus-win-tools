using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class HardwareActionPolicyTests
{
    [Fact]
    public void Defaults_AreSleepAndSleepAndDifferOnBatteryFalse()
    {
        var policy = new HardwareActionPolicy();

        policy.PowerButton.Should().Be(HardwareAction.Sleep);
        policy.LidClose.Should().Be(HardwareAction.Sleep);
        policy.DifferOnBattery.Should().BeFalse();
        policy.PowerButtonOnBattery.Should().Be(HardwareAction.Sleep);
        policy.LidCloseOnBattery.Should().Be(HardwareAction.Hibernate);
    }

    [Fact]
    public void JsonRoundtrip_PreservesAllFields()
    {
        var original = new HardwareActionPolicy
        {
            PowerButton          = HardwareAction.Hibernate,
            LidClose             = HardwareAction.DoNothing,
            DifferOnBattery      = true,
            PowerButtonOnBattery = HardwareAction.ShutDown,
            LidCloseOnBattery    = HardwareAction.TurnOffDisplay,
        };

        var json = JsonSerializer.Serialize(original);
        var roundtripped = JsonSerializer.Deserialize<HardwareActionPolicy>(json);

        roundtripped.Should().NotBeNull();
        roundtripped!.PowerButton.Should().Be(HardwareAction.Hibernate);
        roundtripped.LidClose.Should().Be(HardwareAction.DoNothing);
        roundtripped.DifferOnBattery.Should().BeTrue();
        roundtripped.PowerButtonOnBattery.Should().Be(HardwareAction.ShutDown);
        roundtripped.LidCloseOnBattery.Should().Be(HardwareAction.TurnOffDisplay);
    }
}
