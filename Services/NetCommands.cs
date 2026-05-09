using System.Diagnostics;
using System.Text.RegularExpressions;
using NetProfileSwitcher.Models;

namespace NetProfileSwitcher.Services;

public static class NetCommands
{
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
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(10_000);
        return (p.ExitCode, stdout + stderr);
    }

    public static string GetCurrentSsid()
    {
        var (_, output) = Run("netsh", "wlan show interfaces");
        var match = Regex.Match(output, @"^\s*SSID\s*:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    public static List<string> GetAdapters()
    {
        var (_, output) = Run("netsh", "interface show interface");
        var list = new List<string>();
        foreach (var line in output.Split('\n').Skip(3))
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
                list.Add(string.Join(" ", parts.Skip(3)).Trim());
        }
        if (list.Count == 0) list.Add("Wi-Fi");
        return list;
    }

    public static (bool ok, string msg) Apply(string adapter, NetworkProfile p)
    {
        var errors = new List<string>();

        if (p.UseDhcp)
        {
            var r1 = Run("netsh", $"interface ip set address \"{adapter}\" dhcp");
            if (r1.code != 0) errors.Add(r1.output);

            if (!string.IsNullOrWhiteSpace(p.Dns1))
            {
                var r2 = Run("netsh", $"interface ip set dns \"{adapter}\" static {p.Dns1} primary");
                if (r2.code != 0) errors.Add(r2.output);
                if (!string.IsNullOrWhiteSpace(p.Dns2))
                {
                    var r3 = Run("netsh", $"interface ip add dns \"{adapter}\" {p.Dns2} index=2");
                    if (r3.code != 0) errors.Add(r3.output);
                }
            }
            else
            {
                var r2 = Run("netsh", $"interface ip set dns \"{adapter}\" dhcp");
                if (r2.code != 0) errors.Add(r2.output);
            }
        }
        else
        {
            var r1 = Run("netsh",
                $"interface ip set address \"{adapter}\" static {p.Ip} {p.Subnet} {p.Gateway}");
            if (r1.code != 0) errors.Add(r1.output);

            if (!string.IsNullOrWhiteSpace(p.Dns1))
            {
                var r2 = Run("netsh", $"interface ip set dns \"{adapter}\" static {p.Dns1} primary");
                if (r2.code != 0) errors.Add(r2.output);
            }
            if (!string.IsNullOrWhiteSpace(p.Dns2))
            {
                var r3 = Run("netsh", $"interface ip add dns \"{adapter}\" {p.Dns2} index=2");
                if (r3.code != 0) errors.Add(r3.output);
            }
        }

        return errors.Count == 0
            ? (true, $"Profile \"{p.Name}\" applied.")
            : (false, string.Join("\n", errors));
    }
}
