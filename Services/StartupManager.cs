using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace NetProfileSwitcher.Services;

public static class StartupManager
{
    private const string TaskName = "NetProfileSwitcher";

    public static bool IsRegistered()
    {
        var (code, _) = Run("schtasks", $"/query /tn \"{TaskName}\"");
        return code == 0;
    }

    public static (bool ok, string msg) Register(string exePath)
    {
        string xml = BuildTaskXml(exePath);
        string tmpXml = Path.Combine(Path.GetTempPath(), "NetProfileSwitcher-task.xml");
        File.WriteAllText(tmpXml, xml, Encoding.Unicode);

        var (code, output) = Run("schtasks", $"/create /tn \"{TaskName}\" /xml \"{tmpXml}\" /f");
        File.Delete(tmpXml);
        return code == 0
            ? (true, "Startup task registered.")
            : (false, output.Trim());
    }

    public static (bool ok, string msg) Unregister()
    {
        var (code, output) = Run("schtasks", $"/delete /tn \"{TaskName}\" /f");
        return code == 0
            ? (true, "Startup task removed.")
            : (false, output.Trim());
    }

    private static (int code, string output) Run(string exe, string args)
    {
        var psi = new ProcessStartInfo(exe, args)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(10_000);
        return (p.ExitCode, output);
    }

    private static string BuildTaskXml(string exePath)
    {
        string user = WindowsIdentity.GetCurrent().Name;
        string escapedPath = SecurityElement.Escape(exePath)!;
        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <Triggers>
                <LogonTrigger><Enabled>true</Enabled></LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{user}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{escapedPath}</Command>
                </Exec>
              </Actions>
            </Task>
            """;
    }
}
