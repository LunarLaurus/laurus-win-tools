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
        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();
        p.WaitForExit(10_000);
        return (p.ExitCode, stdoutTask.Result + stderrTask.Result);
    }

    public static string GetCurrentSsid()
    {
        var (_, output) = Run("netsh", "wlan show interfaces");
        var match = Regex.Match(output, @"^\s*SSID\s*:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    public static AdapterInfo? GetAdapterInfo(string adapter)
    {
        var (code, output) = Run("netsh", $"interface ip show config \"{adapter}\"");
        if (code != 0 || string.IsNullOrWhiteSpace(output)) return null;

        bool isDhcp = Regex.IsMatch(output, @"DHCP enabled\s*:\s*Yes", RegexOptions.IgnoreCase);

        var ipMatch     = Regex.Match(output, @"IP Address\s*:\s*(\d[\d.]+)",           RegexOptions.IgnoreCase);
        var subnetMatch = Regex.Match(output, @"\(mask (\d[\d.]+)\)",                   RegexOptions.IgnoreCase);
        var gwMatch     = Regex.Match(output, @"Default Gateway\s*:\s*(\d[\d.]+)",      RegexOptions.IgnoreCase);

        // DNS IPs appear on the label line and may continue on indented-only lines below it
        var dnsIps = new List<string>();
        bool inDns = false;
        foreach (var line in output.Split('\n'))
        {
            if (Regex.IsMatch(line, @"DNS [Ss]ervers|Statically Configured DNS", RegexOptions.IgnoreCase))
            {
                inDns = true;
                var ip = Regex.Match(line, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}");
                if (ip.Success) dnsIps.Add(ip.Value);
            }
            else if (inDns)
            {
                // Continuation lines are blank or heavily indented with only an IP
                var ip = Regex.Match(line.Trim(), @"^(\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})$");
                if (ip.Success) dnsIps.Add(ip.Groups[1].Value);
                else if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("  ")) inDns = false;
            }
        }

        return new AdapterInfo(
            IsDhcp:  isDhcp,
            Ip:      ipMatch.Success     ? ipMatch.Groups[1].Value     : "",
            Subnet:  subnetMatch.Success ? subnetMatch.Groups[1].Value : "",
            Gateway: gwMatch.Success     ? gwMatch.Groups[1].Value     : "",
            Dns1:    dnsIps.Count > 0   ? dnsIps[0]                  : "",
            Dns2:    dnsIps.Count > 1   ? dnsIps[1]                  : ""
        );
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
            // Skip set address dhcp if already in DHCP mode — avoids triggering a
            // lease renewal that can asynchronously push DNS from the router and
            // race with the static DNS we set below.
            var current = GetAdapterInfo(adapter);
            if (current == null || !current.IsDhcp)
            {
                var r1 = Run("netsh", $"interface ip set address \"{adapter}\" dhcp");
                if (r1.code != 0) errors.Add(r1.output);
            }

            if (!string.IsNullOrWhiteSpace(p.Dns1))
            {
                // validate=no skips reachability check so this succeeds even when
                // the route to the DNS server isn't established yet.
                var r2 = Run("netsh", $"interface ip set dns \"{adapter}\" static {p.Dns1} primary validate=no");
                if (r2.code != 0) errors.Add(r2.output);
                if (!string.IsNullOrWhiteSpace(p.Dns2))
                {
                    var r3 = Run("netsh", $"interface ip add dns \"{adapter}\" {p.Dns2} index=2 validate=no");
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
                var r2 = Run("netsh", $"interface ip set dns \"{adapter}\" static {p.Dns1} primary validate=no");
                if (r2.code != 0) errors.Add(r2.output);
            }
            if (!string.IsNullOrWhiteSpace(p.Dns2))
            {
                var r3 = Run("netsh", $"interface ip add dns \"{adapter}\" {p.Dns2} index=2 validate=no");
                if (r3.code != 0) errors.Add(r3.output);
            }
        }

        return errors.Count == 0
            ? (true, $"Profile \"{p.Name}\" applied.")
            : (false, string.Join("\n", errors));
    }
}
