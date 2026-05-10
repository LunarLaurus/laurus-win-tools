using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ProgramHider;

internal enum ElevationAttemptResult
{
    NotNeeded,
    Relaunched,
    Cancelled,
    Failed
}

internal static class ElevationService
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint TokenQuery = 0x0008;
    private const int TokenElevation = 20;
    private const int ErrorCancelled = 1223;

    public static bool IsCurrentProcessElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool IsProcessElevated(uint processId)
    {
        if (processId == 0)
        {
            return false;
        }

        var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (processHandle == 0)
        {
            return false;
        }

        try
        {
            if (!OpenProcessToken(processHandle, TokenQuery, out var tokenHandle))
            {
                return false;
            }

            try
            {
                var elevation = default(TokenElevationInfo);
                var size = Marshal.SizeOf<TokenElevationInfo>();
                return GetTokenInformation(tokenHandle, TokenElevation, ref elevation, size, out _) &&
                       elevation.TokenIsElevated != 0;
            }
            finally
            {
                CloseHandle(tokenHandle);
            }
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    public static ElevationAttemptResult TryRestartElevated(StartupOptions startupOptions, nint? pendingHideHandle)
    {
        if (IsCurrentProcessElevated())
        {
            return ElevationAttemptResult.NotNeeded;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return ElevationAttemptResult.Failed;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = BuildRestartArguments(startupOptions, pendingHideHandle),
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = AppContext.BaseDirectory
        };

        try
        {
            Process.Start(startInfo);
            return ElevationAttemptResult.Relaunched;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == ErrorCancelled)
        {
            return ElevationAttemptResult.Cancelled;
        }
        catch
        {
            return ElevationAttemptResult.Failed;
        }
    }

    internal static string BuildRestartArguments(StartupOptions startupOptions, nint? pendingHideHandle)
    {
        var parts = new List<string>();
        var effectiveDelaySeconds = Math.Max(startupOptions.DelaySeconds, 2);
        if (startupOptions.IsStartupLaunch)
        {
            parts.Add("--startup");
        }

        if (startupOptions.SafeMode)
        {
            parts.Add("--safe-mode");
        }

        if (effectiveDelaySeconds > 0)
        {
            parts.Add($"--delay={effectiveDelaySeconds}");
        }

        if (pendingHideHandle.HasValue && pendingHideHandle.Value != 0)
        {
            parts.Add($"--rehide=0x{pendingHideHandle.Value.ToInt64():X}");
        }

        return string.Join(" ", parts);
    }

    internal static bool TryParseHandle(string rawValue, out nint handle)
    {
        handle = 0;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var value = rawValue.Trim();
        NumberStyles style;
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
            style = NumberStyles.AllowHexSpecifier;
        }
        else
        {
            style = NumberStyles.Integer;
        }

        if (!long.TryParse(value, style, CultureInfo.InvariantCulture, out var parsedValue) || parsedValue == 0)
        {
            return false;
        }

        handle = (nint)parsedValue;
        return true;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(nint processHandle, uint desiredAccess, out nint tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        nint tokenHandle,
        int tokenInformationClass,
        ref TokenElevationInfo tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenElevationInfo
    {
        public int TokenIsElevated;
    }
}
