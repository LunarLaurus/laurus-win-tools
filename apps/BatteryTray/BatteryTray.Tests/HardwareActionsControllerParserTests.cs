using System.IO;
using FluentAssertions;
using Xunit;

namespace BatteryTray.Tests;

public class HardwareActionsControllerParserTests
{
    private static string ReadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public void Parse_BalancedPlanFixture_ReturnsExpectedSnapshot()
    {
        var fixture = ReadFixture("powercfg-sub-buttons-balanced.txt");

        var snapshot = HardwareActionsController.ParseSubButtonsQuery(fixture);

        snapshot.Should().NotBeNull();
        snapshot!.Value.LidCloseAc.Should().Be(HardwareAction.Sleep);
        snapshot.Value.LidCloseDc.Should().Be(HardwareAction.Sleep);
        snapshot.Value.PowerButtonAc.Should().Be(HardwareAction.Sleep);
        snapshot.Value.PowerButtonDc.Should().Be(HardwareAction.ShutDown);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        HardwareActionsController.ParseSubButtonsQuery("").Should().BeNull();
    }

    [Fact]
    public void Parse_MissingLidSection_ReturnsNull()
    {
        const string partial = """
            Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
              Subgroup GUID: 4f971e89-eebd-4455-a8de-9e59040e7347  (Power buttons and lid)
                Power Setting GUID: 7648efa3-dd9c-4e3e-b566-50f929386280  (Power button action)
                  Current AC Power Setting Index: 0x00000001
                  Current DC Power Setting Index: 0x00000001
            """;

        HardwareActionsController.ParseSubButtonsQuery(partial).Should().BeNull(
            because: "missing lid close indices is unparseable; we return null rather than guess");
    }

    [Fact]
    public void Parse_MissingPowerButtonSection_ReturnsNull()
    {
        const string partial = """
            Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)
              Subgroup GUID: 4f971e89-eebd-4455-a8de-9e59040e7347  (Power buttons and lid)
                Power Setting GUID: 5ca83367-6e45-459f-a27b-476b1d01c936  (Lid close action)
                  Current AC Power Setting Index: 0x00000001
                  Current DC Power Setting Index: 0x00000001
            """;

        HardwareActionsController.ParseSubButtonsQuery(partial).Should().BeNull();
    }

    [Fact]
    public void Parse_ExtraWhitespace_StillSucceeds()
    {
        var fixture = ReadFixture("powercfg-sub-buttons-balanced.txt");
        var withExtraWhitespace = fixture.Replace("Current AC", "  Current  AC");

        var snapshot = HardwareActionsController.ParseSubButtonsQuery(withExtraWhitespace);

        snapshot.Should().NotBeNull(
            because: "the parser must tolerate the variable indentation powercfg may emit");
    }
}
