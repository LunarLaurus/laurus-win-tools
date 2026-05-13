using System.ComponentModel;
using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace WindowsAppCore;

/// <summary>
/// Registers an app as a scheduled task so it can launch with elevation at logon.
/// HKCU\Run cannot auto-elevate; schtasks with HighestAvailable is the correct path.
/// </summary>
public sealed class ScheduledTaskStartupRegistration : IStartupRegistration
{
    private const string InstallArg = "--schtask-install";
    private const string UninstallArg = "--schtask-uninstall";

    private readonly string _taskName;
    private readonly string _exePath;
    private readonly string _taskDescription;
    private readonly string? _arguments;

    public ScheduledTaskStartupRegistration(
        string taskName,
        string exePath,
        string taskDescription = "",
        string? arguments = null)
    {
        _taskName = taskName;
        _exePath = exePath;
        _taskDescription = taskDescription;
        _arguments = arguments;
    }

    /// <summary>
    /// Call at the very top of Main() before any other startup logic. Returns the
    /// process exit code if the invocation is an elevated-helper call, else null.
    /// </summary>
    public int? TryHandleHelperArgs(string[] args)
    {
        if (args.Length == 0) return null;

        if (args[0] == InstallArg)
        {
            var sid = args.Length > 1 ? args[1] : null;
            return CreateTask(sid) ? 0 : 1;
        }

        if (args[0] == UninstallArg)
            return DeleteTask() ? 0 : 1;

        return null;
    }

    public bool IsRegistered
    {
        get
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
                psi.ArgumentList.Add(_taskName);

                using var p = Process.Start(psi);
                if (p is null) return false;
                p.WaitForExit(5000);
                return p.ExitCode == 0;
            }
            catch { return false; }
        }
    }

    public StartupRegistrationResult Register()
    {
        if (IsRegistered) return StartupRegistrationResult.Success;

        if (ElevationHelper.IsElevated())
        {
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            return CreateTask(sid) ? StartupRegistrationResult.Success : StartupRegistrationResult.Failed;
        }

        // Capture current user's SID before elevation. If the user enters a
        // different admin's credentials at the UAC prompt, the task would run
        // at that admin's logon instead — we want the current user.
        var currentUserSid = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty;
        return RunElevated($"{InstallArg} \"{currentUserSid}\"");
    }

    public StartupRegistrationResult Unregister()
    {
        if (!IsRegistered) return StartupRegistrationResult.Success;

        if (ElevationHelper.IsElevated())
            return DeleteTask() ? StartupRegistrationResult.Success : StartupRegistrationResult.Failed;

        return RunElevated(UninstallArg);
    }

    private StartupRegistrationResult RunElevated(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? _exePath,
            UseShellExecute = true,
            Verb = "runas",
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments = args,
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return StartupRegistrationResult.Failed;
            p.WaitForExit(15000);
            return p.ExitCode == 0 ? StartupRegistrationResult.Success : StartupRegistrationResult.Failed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return StartupRegistrationResult.UserCancelled;
        }
        catch { return StartupRegistrationResult.Failed; }
    }

    private bool CreateTask(string? userSid)
    {
        if (string.IsNullOrEmpty(userSid))
            userSid = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty;

        var xml = BuildTaskXml(userSid);
        var xmlPath = Path.Combine(Path.GetTempPath(), $"{_taskName}-{Guid.NewGuid():N}.xml");

        try
        {
            // schtasks /XML expects UTF-16 LE with BOM.
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
            psi.ArgumentList.Add(_taskName);
            psi.ArgumentList.Add("/XML");
            psi.ArgumentList.Add(xmlPath);
            psi.ArgumentList.Add("/F");

            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(10000);
            return p.ExitCode == 0;
        }
        finally
        {
            try { File.Delete(xmlPath); } catch { }
        }
    }

    private bool DeleteTask()
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
        psi.ArgumentList.Add(_taskName);
        psi.ArgumentList.Add("/F");

        using var p = Process.Start(psi);
        if (p is null) return false;
        p.WaitForExit(10000);
        // /Delete on a non-existent task returns non-zero; treat as success (idempotent).
        return true;
    }

    private string BuildTaskXml(string userSid)
    {
        var safePath = SecurityElement.Escape(_exePath) ?? _exePath;
        var safeSid  = SecurityElement.Escape(userSid)  ?? userSid;
        var safeDesc = SecurityElement.Escape(_taskDescription) ?? string.Empty;
        var argsElem = string.IsNullOrEmpty(_arguments)
            ? string.Empty
            : $"  <Arguments>{SecurityElement.Escape(_arguments)}</Arguments>\n              ";

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.4" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>{safeDesc}</Description>
                <URI>\{_taskName}</URI>
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
                  {argsElem}</Exec>
              </Actions>
            </Task>
            """;
    }
}
