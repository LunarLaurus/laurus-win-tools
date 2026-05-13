using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace BatteryTray;

public enum StartupResult { Success, Failed, UserCancelled }

/// <summary>
/// Manages auto-start at logon by way of a Scheduled Task with RunLevel=HighestAvailable.
/// The task is pre-authorized at create time, so subsequent logons silently launch
/// BatteryTray with elevation — no UAC prompt at every login.
///
/// Why not HKCU\...\Run? Because that key cannot launch elevated processes:
/// Windows refuses to auto-elevate from autorun for security reasons. Task Scheduler
/// is the only sanctioned mechanism for "start elevated at logon, no prompt".
/// </summary>
public static class StartupManager
{
    private const string TaskName = "BatteryTray";

    // Legacy v1.x location — cleaned up automatically when migrating users hit Save.
    private const string LegacyRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string LegacyRunValue = "BatteryTray";

    public static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static bool GetRunAtStartup()
    {
        try
        {
            var psi = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("/Query");
            psi.ArgumentList.Add("/TN");
            psi.ArgumentList.Add(TaskName);

            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Toggles auto-start. If the current process isn't elevated, this re-launches
    /// itself with the "runas" verb to trigger a single UAC prompt. Subsequent
    /// logons run silently with elevation.
    /// </summary>
    public static StartupResult SetRunAtStartup(bool enabled)
    {
        // Always sweep the legacy v1.x Run key, regardless of which direction we're toggling.
        TryRemoveLegacyRunKey();

        // No-op fast path: state already matches.
        if (GetRunAtStartup() == enabled) return StartupResult.Success;

        if (IsElevated())
        {
            // We're already elevated — apply directly.
            var ok = enabled ? CreateTask(WindowsIdentity.GetCurrent().User?.Value) : DeleteTask();
            return ok ? StartupResult.Success : StartupResult.Failed;
        }

        // Capture the *current* user's SID before elevating. If a non-admin enters someone
        // else's admin credentials at the UAC prompt, the elevated child would otherwise
        // create a task that runs at the *admin's* logon, not ours.
        var currentUserSid = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty;

        var verb = enabled ? "--install-task" : "--uninstall-task";

        var psi = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath ?? Application.ExecutablePath,
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments = $"{verb} \"{currentUserSid}\"",
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return StartupResult.Failed;
            p.WaitForExit(15000);
            return p.ExitCode == 0 ? StartupResult.Success : StartupResult.Failed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED: user clicked No on the UAC consent prompt.
            return StartupResult.UserCancelled;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Self-elevation failed: {ex}");
            return StartupResult.Failed;
        }
    }

    // ---------- Helper actions invoked by the elevated child process ----------

    internal static bool RunInstallAction(string? userSid)
    {
        try
        {
            return CreateTask(userSid);
        }
        catch (Exception ex)
        {
            LogFailure("install", ex);
            return false;
        }
    }

    internal static bool RunUninstallAction()
    {
        try
        {
            return DeleteTask();
        }
        catch (Exception ex)
        {
            LogFailure("uninstall", ex);
            return false;
        }
    }

    // ---------- Internals ----------

    private static bool CreateTask(string? userSid)
    {
        var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
        if (string.IsNullOrEmpty(exePath)) return false;

        // Resolve a usable SID. Falls back to the elevated identity if none was passed —
        // imperfect but better than failing.
        if (string.IsNullOrEmpty(userSid))
        {
            userSid = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty;
        }

        var xml = BuildTaskXml(exePath, userSid);
        var xmlPath = Path.Combine(Path.GetTempPath(), $"BatteryTray-{Guid.NewGuid():N}.xml");

        try
        {
            // schtasks /XML expects UTF-16 LE with BOM to match the XML declaration.
            File.WriteAllText(xmlPath, xml, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));

            var psi = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("/Create");
            psi.ArgumentList.Add("/TN");
            psi.ArgumentList.Add(TaskName);
            psi.ArgumentList.Add("/XML");
            psi.ArgumentList.Add(xmlPath);
            psi.ArgumentList.Add("/F");

            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(10000);

            if (p.ExitCode != 0)
            {
                LogFailure("schtasks /Create", p);
            }
            return p.ExitCode == 0;
        }
        finally
        {
            try { File.Delete(xmlPath); } catch { /* best-effort */ }
        }
    }

    private static bool DeleteTask()
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("/Delete");
        psi.ArgumentList.Add("/TN");
        psi.ArgumentList.Add(TaskName);
        psi.ArgumentList.Add("/F");

        using var p = Process.Start(psi);
        if (p is null) return false;
        p.WaitForExit(10000);

        // schtasks /Delete on a non-existent task returns non-zero; we treat that as success
        // for our purposes (idempotent disable).
        if (p.ExitCode != 0)
        {
            LogFailure("schtasks /Delete (non-fatal)", p);
        }
        return true;
    }

    private static string BuildTaskXml(string exePath, string userSid)
    {
        var safePath = SecurityElement.Escape(exePath) ?? exePath;
        var safeSid  = SecurityElement.Escape(userSid)  ?? userSid;

        // Notes on the settings:
        //   RunLevel HighestAvailable — runs with full admin token if user is admin
        //   DisallowStartIfOnBatteries=false — actually run on battery (default false)
        //   StopIfGoingOnBatteries=false — keep running when unplugged (default true!)
        //   ExecutionTimeLimit=PT0S — no time limit (default is 72 hours, which would kill us)
        //   MultipleInstancesPolicy=IgnoreNew — task scheduler won't double-launch us
        //   LogonType=InteractiveToken — runs in user's interactive desktop session (so the tray icon shows)
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>BatteryTray — auto-start configurable battery indicator</Description>
                <URI>\{TaskName}</URI>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{safeSid}</UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{safeSid}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>true</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{safePath}</Command>
                </Exec>
              </Actions>
            </Task>
            """;
    }

    private static void TryRemoveLegacyRunKey()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(LegacyRunKey, writable: true);
            if (key?.GetValue(LegacyRunValue) != null)
            {
                key.DeleteValue(LegacyRunValue, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Non-fatal.
        }
    }

    private static void LogFailure(string stage, Exception ex)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "BatteryTray-startup.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:O}] {stage} failed: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}");
        }
        catch { /* swallow */ }
    }

    private static void LogFailure(string stage, Process p)
    {
        try
        {
            var stderr = p.StandardError.ReadToEnd();
            var stdout = p.StandardOutput.ReadToEnd();
            var path = Path.Combine(Path.GetTempPath(), "BatteryTray-startup.log");
            File.AppendAllText(path,
                $"[{DateTime.Now:O}] {stage} exit={p.ExitCode}{Environment.NewLine}" +
                $"  stdout: {stdout.Trim()}{Environment.NewLine}" +
                $"  stderr: {stderr.Trim()}{Environment.NewLine}");
        }
        catch { /* swallow */ }
    }
}
