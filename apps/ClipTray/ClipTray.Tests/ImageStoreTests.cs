using System.IO;
using FluentAssertions;
using WindowsAppTesting;
using Xunit;

namespace ClipTray.Tests;

public class ImageStoreTests
{
    [Fact]
    public void Write_NewHash_CreatesSidecar()
    {
        using var temp = new TempAppData("ClipTray");
        var store = new ImageStore(Path.Combine(temp.Path, "items"));

        var path = store.Write("abc123", new byte[] { 1, 2, 3 });

        File.Exists(path).Should().BeTrue();
        new FileInfo(path).Name.Should().Be("abc123.png");
    }

    [Fact]
    public void Write_ExistingHash_DoesNotRewrite()
    {
        using var temp = new TempAppData("ClipTray");
        var store = new ImageStore(Path.Combine(temp.Path, "items"));

        var first = store.Write("abc123", new byte[] { 1, 2, 3 });
        var firstMtime = File.GetLastWriteTimeUtc(first);

        System.Threading.Thread.Sleep(50);
        var second = store.Write("abc123", new byte[] { 9, 9, 9 });

        second.Should().Be(first);
        File.GetLastWriteTimeUtc(second).Should().Be(firstMtime);
        File.ReadAllBytes(second).Should().Equal(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public void Delete_RemovesSidecar()
    {
        using var temp = new TempAppData("ClipTray");
        var store = new ImageStore(Path.Combine(temp.Path, "items"));
        var path = store.Write("hash1", new byte[] { 1 });

        store.Delete("hash1");

        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void TotalBytes_ReturnsSumOfAllSidecars()
    {
        using var temp = new TempAppData("ClipTray");
        var store = new ImageStore(Path.Combine(temp.Path, "items"));
        store.Write("a", new byte[100]);
        store.Write("b", new byte[200]);
        store.Write("c", new byte[300]);

        store.TotalBytes().Should().Be(600);
    }

    [Fact]
    public void SweepOrphans_RemovesSidecarsNotInKnownHashes()
    {
        using var temp = new TempAppData("ClipTray");
        var dir = Path.Combine(temp.Path, "items");
        var store = new ImageStore(dir);
        store.Write("keep", new byte[] { 1 });
        store.Write("orphan", new byte[] { 2 });

        store.SweepOrphans(new[] { "keep" });

        File.Exists(Path.Combine(dir, "keep.png")).Should().BeTrue();
        File.Exists(Path.Combine(dir, "orphan.png")).Should().BeFalse();
    }
}
