using System.Runtime.InteropServices;
using Xunit;

namespace ClipTray.E2ETests;

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
