using System.Runtime.InteropServices;
using Xunit;

namespace BatteryTray.E2ETests;

/// <summary>
/// xUnit fact that skips when not running on Windows. The whole project targets
/// Windows but a non-Windows CI runner (Linux dotnet test) shouldn't fail —
/// it should skip these gracefully.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "E2E tests require Windows";
        }
    }
}

public sealed class WindowsTheoryAttribute : TheoryAttribute
{
    public WindowsTheoryAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "E2E tests require Windows";
        }
    }
}
