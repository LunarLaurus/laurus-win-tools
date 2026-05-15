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

    /// <summary>
    /// Parses output of `powercfg /q SCHEME_CURRENT SUB_BUTTONS`. Walks
    /// line-by-line, holding the current setting GUID as state, and assigns
    /// AC/DC ints to the matching field of a snapshot. Returns the snapshot
    /// iff all four fields were populated; returns null if any are missing
    /// (taken as "unparseable" rather than risking partial values).
    ///
    /// Anchors on invariant GUIDs and the literal substrings
    /// "Current AC Power Setting Index:" and "Current DC Power Setting Index:".
    /// </summary>
    internal static HardwareActionsSnapshot? ParseSubButtonsQuery(string powerCfgOutput)
    {
        if (string.IsNullOrWhiteSpace(powerCfgOutput)) return null;

        int? pbAc = null, pbDc = null, lidAc = null, lidDc = null;
        Guid? currentSettingGuid = null;

        foreach (var rawLine in powerCfgOutput.Split('\n'))
        {
            // Collapse internal whitespace runs to a single space so callers that
            // emit double-spacing ("Current  AC Power Setting Index:") still match.
            var line = System.Text.RegularExpressions.Regex.Replace(rawLine.Trim(), @"\s+", " ");
            if (line.Length == 0) continue;

            // Track which setting block we're in by detecting the GUID lines.
            if (line.StartsWith("Power Setting GUID:", StringComparison.OrdinalIgnoreCase))
            {
                // Format: "Power Setting GUID: <guid>  (Friendly Name)"
                var afterColon = line.Substring("Power Setting GUID:".Length).TrimStart();
                var spaceIdx = afterColon.IndexOf(' ');
                var guidStr = spaceIdx > 0 ? afterColon[..spaceIdx] : afterColon;
                currentSettingGuid = Guid.TryParse(guidStr, out var g) ? g : null;
                continue;
            }

            if (currentSettingGuid is null) continue;

            // Value lines look like: "Current AC Power Setting Index: 0x00000001"
            var isAc = line.StartsWith("Current AC Power Setting Index:", StringComparison.OrdinalIgnoreCase);
            var isDc = !isAc && line.StartsWith("Current DC Power Setting Index:", StringComparison.OrdinalIgnoreCase);
            if (!isAc && !isDc) continue;

            var colonIdx = line.IndexOf(':');
            if (colonIdx < 0) continue;

            var valuePart = line[(colonIdx + 1)..].Trim();
            // Accept both 0xNN and decimal forms.
            int? value = null;
            if (valuePart.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(valuePart[2..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hex))
                    value = hex;
            }
            else if (int.TryParse(valuePart, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var dec))
            {
                value = dec;
            }

            if (value is null) continue;

            if (currentSettingGuid == PButtonAction)
            {
                if (isAc) pbAc = value;
                else      pbDc = value;
            }
            else if (currentSettingGuid == LidAction)
            {
                if (isAc) lidAc = value;
                else      lidDc = value;
            }
            // Other setting GUIDs (e.g. sleep button) are intentionally ignored.
        }

        if (pbAc is null || pbDc is null || lidAc is null || lidDc is null)
            return null;

        return new HardwareActionsSnapshot(
            PowerButtonAc: (HardwareAction)pbAc.Value,
            PowerButtonDc: (HardwareAction)pbDc.Value,
            LidCloseAc:    (HardwareAction)lidAc.Value,
            LidCloseDc:    (HardwareAction)lidDc.Value);
    }
}
