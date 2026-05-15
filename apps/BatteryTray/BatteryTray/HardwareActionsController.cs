using System;
using System.Collections.Generic;
using System.Text;

namespace BatteryTray;

/// <summary>
/// Wraps powercfg.exe to read and write the power-button and lid-close
/// actions used by Windows. Reads are unelevated (powercfg /q works for
/// standard users on the active scheme). Writes require admin, so
/// <see cref="ApplyToAllPlans"/> spawns an elevated cmd.exe via the runas
/// verb that chains every required setacvalueindex/setdcvalueindex into
/// one UAC prompt per Save.
/// </summary>
public static class HardwareActionsController
{
    public static readonly Guid SubButtons    = new("4f971e89-eebd-4455-a8de-9e59040e7347");
    public static readonly Guid PButtonAction = new("7648efa3-dd9c-4e3e-b566-50f929386280");
    public static readonly Guid LidAction     = new("5ca83367-6e45-459f-a27b-476b1d01c936");

    /// <summary>
    /// Builds the argument string passed to cmd.exe /c. Chains every
    /// powercfg invocation with &amp;&amp; so the elevated process's exit
    /// code reflects the first powercfg failure rather than only the last.
    /// </summary>
    internal static string BuildCmdArgs(IReadOnlyList<Guid> planGuids, HardwareActionPolicy policy)
    {
        int acPower = (int)policy.PowerButton;
        int dcPower = (int)(policy.DifferOnBattery ? policy.PowerButtonOnBattery : policy.PowerButton);
        int acLid   = (int)policy.LidClose;
        int dcLid   = (int)(policy.DifferOnBattery ? policy.LidCloseOnBattery : policy.LidClose);

        var sb = new StringBuilder();
        sb.Append("/c \"");

        for (int i = 0; i < planGuids.Count; i++)
        {
            var p = planGuids[i];
            sb.Append("powercfg /setacvalueindex ").Append(p).Append(' ').Append(SubButtons).Append(' ').Append(PButtonAction).Append(' ').Append(acPower).Append(" && ");
            sb.Append("powercfg /setdcvalueindex ").Append(p).Append(' ').Append(SubButtons).Append(' ').Append(PButtonAction).Append(' ').Append(dcPower).Append(" && ");
            sb.Append("powercfg /setacvalueindex ").Append(p).Append(' ').Append(SubButtons).Append(' ').Append(LidAction).Append(' ').Append(acLid).Append(" && ");
            sb.Append("powercfg /setdcvalueindex ").Append(p).Append(' ').Append(SubButtons).Append(' ').Append(LidAction).Append(' ').Append(dcLid).Append(" && ");
        }

        sb.Append("powercfg /setactive SCHEME_CURRENT\"");
        return sb.ToString();
    }
}
