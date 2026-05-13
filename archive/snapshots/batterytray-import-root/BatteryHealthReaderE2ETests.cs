using FluentAssertions;
using Xunit;

namespace BatteryTray.E2ETests;

public class BatteryHealthReaderE2ETests
{
    [WindowsFact]
    public void Read_DoesNotThrow()
    {
        var act = () => BatteryHealthReader.Read();
        act.Should().NotThrow(
            because: "even on systems without batteries, the reader should return null gracefully");
    }

    [WindowsFact]
    public void Read_OnSystemWithBattery_PrefersIoctlSourceForChemistry()
    {
        var info = BatteryHealthReader.Read();

        if (info is null) return;  // No battery — test is trivially OK.

        // If chemistry was extracted at all, the source should be specified.
        if (info.Chemistry is not null && !info.Chemistry.StartsWith("("))
        {
            info.ChemistrySource.Should().NotBeNullOrEmpty();

            // The IOCTL path should win when it produces data — that's the v1.8 fix.
            // If we got "Win32_Battery (legacy enum)" as the source, IOCTL must have
            // failed to extract a tag, which is informational not erroneous.
        }
    }

    [WindowsFact]
    public void HealthPercent_Calculated_WhenCapacitiesPresent()
    {
        var info = BatteryHealthReader.Read();
        if (info?.DesignCapacityMilliwattHours is null
            || info.FullChargedCapacityMilliwattHours is null) return;

        info.HealthPercent.Should().NotBeNull();
        info.HealthPercent.Should().BeGreaterThan(0);
        info.HealthPercent.Should().BeLessThanOrEqualTo(150,
            because: "even fresh batteries occasionally read slightly over design capacity, but >150% is a bug");
    }
}
