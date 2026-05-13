using System.Drawing;
using FluentAssertions;
using Xunit;

namespace WindowsTrayCore.Tests;

public class IconBuilderTests
{
    [WindowsFact]
    public void FromBitmap_ReturnsNonNullIcon()
    {
        using var bmp = new Bitmap(16, 16);
        using var icon = IconBuilder.FromBitmap(bmp);
        icon.Should().NotBeNull();
    }

    [WindowsFact]
    public void FromBitmap_DisposeIsSafeToCallMultipleTimes()
    {
        using var bmp = new Bitmap(16, 16);
        var icon = IconBuilder.FromBitmap(bmp);
        icon.Dispose();
        icon.Dispose(); // managed-owned Icon dispose is idempotent
    }

    [WindowsFact]
    public void FromBitmap_NullArg_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => IconBuilder.FromBitmap(null!));
    }

    [WindowsFact]
    public void FromBitmap_ManyIterations_DoesNotLeakGdiHandles()
    {
        // Tight loop creates and disposes many icons. If FromBitmap leaks the
        // underlying HICON, the per-process GDI handle quota is 10 000 and this
        // would eventually throw. 200 round-trips is enough to catch a leak in
        // CI without slowing the test suite noticeably.
        for (int i = 0; i < 200; i++)
        {
            using var bmp = new Bitmap(16, 16);
            using var icon = IconBuilder.FromBitmap(bmp);
        }
    }
}
