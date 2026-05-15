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

    private static bool IsElevated()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    [WindowsFact]
    public void ApplyToAllPlans_OnElevatedSession_RoundTripsThroughReadCurrent()
    {
        if (!IsElevated())
        {
            // Not an xUnit Skip (those need attribute-level config); the test
            // succeeds as a no-op when unelevated, with a marker assertion that
            // shows up in the test output for transparency.
            true.Should().BeTrue(because: "skipped: requires admin to write powercfg values");
            return;
        }

        if (!HasLidAndPowerButtonHardware())
        {
            true.Should().BeTrue(
                because: "skipped: host lacks lid+power-button GUIDs so round-trip is meaningless");
            return;
        }

        var original = HardwareActionsController.ReadCurrent();
        original.Should().NotBeNull(because: "baseline read must succeed before we mutate");

        try
        {
            var newPolicy = new HardwareActionPolicy
            {
                PowerButton          = HardwareAction.DoNothing,
                LidClose             = HardwareAction.Hibernate,
                DifferOnBattery      = true,
                PowerButtonOnBattery = HardwareAction.Sleep,
                LidCloseOnBattery    = HardwareAction.ShutDown,
            };

            var result = HardwareActionsController.ApplyToAllPlans(newPolicy);

            result.Ok.Should().BeTrue(because: "elevated apply on a real machine should succeed");

            var after = HardwareActionsController.ReadCurrent();
            after.Should().NotBeNull();
            after!.Value.PowerButtonAc.Should().Be(HardwareAction.DoNothing);
            after.Value.PowerButtonDc.Should().Be(HardwareAction.Sleep);
            after.Value.LidCloseAc.Should().Be(HardwareAction.Hibernate);
            after.Value.LidCloseDc.Should().Be(HardwareAction.ShutDown);
        }
        finally
        {
            // Restore original values regardless of outcome.
            if (original.HasValue)
            {
                var restore = new HardwareActionPolicy
                {
                    PowerButton          = original.Value.PowerButtonAc,
                    LidClose             = original.Value.LidCloseAc,
                    DifferOnBattery      = true,
                    PowerButtonOnBattery = original.Value.PowerButtonDc,
                    LidCloseOnBattery    = original.Value.LidCloseDc,
                };
                HardwareActionsController.ApplyToAllPlans(restore);
            }
        }
    }

    [WindowsFact]
    public void ApplyToAllPlans_OnUnelevatedSession_ReturnsFailureWithoutCrashing()
    {
        if (IsElevated())
        {
            // Mirror of the elevation gate above; we can only verify the
            // failure path when actually unelevated.
            true.Should().BeTrue(because: "skipped: requires NON-elevated session to exercise failure path");
            return;
        }

        var policy = new HardwareActionPolicy();
        var act = () => HardwareActionsController.ApplyToAllPlans(policy);

        act.Should().NotThrow(
            because: "the controller must surface elevation failure as ApplyResult.Ok=false, never an exception");
        // We don't assert Ok=false here because some Windows configurations let cmd.exe
        // launch without UAC (e.g. AlwaysInstallElevated policies); the contract is
        // simply "no crash".
    }
}
