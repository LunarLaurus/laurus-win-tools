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

    /// <summary>
    /// Reads the current power-button and lid-close actions from the active
    /// power scheme. Unelevated; powercfg /q works for standard users.
    /// Returns null on any failure (timeout, non-zero exit, parser failure);
    /// callers should treat null as "feature unavailable" and disable the UI
    /// with a friendly message.
    /// </summary>
    public static HardwareActionsSnapshot? ReadCurrent()
    {
        var stdout = RunPowerCfg("/q SCHEME_CURRENT " + SubButtons);
        if (stdout is null) return null;

        var snapshot = ParseSubButtonsQuery(stdout);
        if (snapshot is null)
        {
            AppLogIfAvailable("hwactions.parse.failed", LogLevel.Warn, new { outputLength = stdout.Length });
        }
        return snapshot;
    }

    private static string? RunPowerCfg(string args)
    {
        try
        {
            using var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (p is null) return null;
            var stdout = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            return p.ExitCode == 0 ? stdout : null;
        }
        catch (Exception ex)
        {
            CrashLogger.Write("hwactions.read", ex);
            return null;
        }
    }

    /// <summary>
    /// Result of an <see cref="ApplyToAllPlans"/> call.
    /// </summary>
    public readonly record struct ApplyResult(bool Ok, string? FailureReason);

    /// <summary>
    /// Writes a HardwareActionPolicy to every installed power plan via a single
    /// elevated cmd.exe invocation chained over powercfg. One UAC prompt per
    /// call, regardless of plan count.
    ///
    /// Returns ApplyResult with Ok=true on success, or Ok=false and a human
    /// readable FailureReason for each failure mode (elevation declined,
    /// powercfg non-zero exit, timeout, unexpected exception, no plans).
    /// </summary>
    public static ApplyResult ApplyToAllPlans(HardwareActionPolicy policy)
    {
        var plans = PowerPlanController.List();
        if (plans.Count == 0)
        {
            AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = "no plans" });
            return new ApplyResult(false, "No power plans installed");
        }

        var planGuids = new Guid[plans.Count];
        for (int i = 0; i < plans.Count; i++) planGuids[i] = plans[i].Guid;

        var args = BuildCmdArgs(planGuids, policy);

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = args,
                Verb = "runas",                                       // triggers UAC
                UseShellExecute = true,                                // required for Verb
                CreateNoWindow = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            };

            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null)
            {
                AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = "Process.Start returned null" });
                return new ApplyResult(false, "Could not start elevated process");
            }

            if (!p.WaitForExit(30_000))
            {
                try { p.Kill(); } catch { /* best-effort */ }
                AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = "timeout" });
                return new ApplyResult(false, "powercfg timed out");
            }

            sw.Stop();
            if (p.ExitCode != 0)
            {
                AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = "non-zero exit", exitCode = p.ExitCode });
                return new ApplyResult(false, $"powercfg exited with code {p.ExitCode}");
            }

            AppLogIfAvailable("hwactions.applied", LogLevel.Info, new
            {
                policy,
                planCount = plans.Count,
                durationMs = sw.ElapsedMilliseconds,
            });
            return new ApplyResult(true, null);
        }
        catch (System.ComponentModel.Win32Exception wx) when (wx.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED: user clicked No on the UAC prompt.
            AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = "elevation declined" });
            return new ApplyResult(false, "Elevation declined");
        }
        catch (Exception ex)
        {
            CrashLogger.Write("hwactions.apply", ex);
            AppLogIfAvailable("hwactions.apply.failed", LogLevel.Warn, new { reason = ex.GetType().Name });
            return new ApplyResult(false, ex.Message);
        }
    }

    /// <summary>
    /// Forwards a "hwactions.read" Info event into the wired AppLog (or no-op
    /// if LogSink is unset, e.g. in tests). Called from SettingsForm after the
    /// drift-hint decision so the payload can include hintShown.
    /// </summary>
    internal static void EmitReadEvent(object data) =>
        AppLogIfAvailable("hwactions.read", LogLevel.Info, data);

    // AppLog is created in Program.cs and not statically accessible here.
    // Program wires LogSink at startup; if unset (e.g. in tests), the call is a no-op.
    internal static Action<string, LogLevel, object?>? LogSink;

    internal enum LogLevel { Info, Warn }

    private static void AppLogIfAvailable(string evt, LogLevel level, object? data)
    {
        try { LogSink?.Invoke(evt, level, data); } catch { /* never let logging break the controller */ }
    }
}
