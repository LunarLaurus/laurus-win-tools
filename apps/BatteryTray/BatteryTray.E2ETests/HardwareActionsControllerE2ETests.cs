using System;
using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace BatteryTray.E2ETests;

/// <summary>
/// Hits real powercfg.exe. Read path runs unelevated; ApplyToAllPlans
/// path is admin-gated and skipped when the test process is unelevated.
///
/// The host's hardware decides what powercfg surfaces under SUB_BUTTONS:
/// laptops expose the lid-close and power-button action GUIDs; desktops
/// typically expose only the Start menu power button (UIBUTTON_ACTION).
/// Tests probe for the relevant GUIDs and assert "populated snapshot" only
/// when the hardware is actually present.
/// </summary>
public class HardwareActionsControllerE2ETests
{
    // The two GUIDs ReadCurrent's parser needs to populate a snapshot.
    private const string LidActionGuid     = "5ca83367-6e45-459f-a27b-476b1d01c936";
    private const string PButtonActionGuid = "7648efa3-dd9c-4e3e-b566-50f929386280";
    private const string SubButtonsGuid    = "4f971e89-eebd-4455-a8de-9e59040e7347";

    private static bool HasLidAndPowerButtonHardware()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = "/q SCHEME_CURRENT " + SubButtonsGuid,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p is null) return false;
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            if (p.ExitCode != 0) return false;
            return stdout.Contains(LidActionGuid, StringComparison.OrdinalIgnoreCase)
                && stdout.Contains(PButtonActionGuid, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    [WindowsFact]
    public void ReadCurrent_OnRealMachine_ReturnsPopulatedOrNullCleanly()
    {
        var act = () => HardwareActionsController.ReadCurrent();
        act.Should().NotThrow(
            because: "ReadCurrent must always return null on failure, never throw");

        var snapshot = act();
        if (HasLidAndPowerButtonHardware())
        {
            snapshot.Should().NotBeNull(
                because: "hardware exposes both GUIDs; parser must succeed");
        }
        // else: null is the documented behaviour on hardware that lacks
        // lid + power-button entries (typically desktop-class hosts).
    }
}
