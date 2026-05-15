using System;
using System.Linq;
using FluentAssertions;
using WindowsAppTesting;
using Xunit;

namespace ClipTray.Tests;

public class ClipboardHistoryTests
{
    private static HistoryItem TextItem(string hash, string text, bool pinned = false, bool sensitive = false) =>
        new(hash, HistoryKind.Text, text, ImagePath: null,
            CapturedUtc: DateTime.UtcNow, SourceProcessName: null,
            IsPinned: pinned, IsSensitive: sensitive);

    [Fact]
    public void Add_NewItem_PrependsToList()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta"));

        h.Items.Should().HaveCount(2);
        h.Items[0].Hash.Should().Be("b");
        h.Items[1].Hash.Should().Be("a");
    }

    [Fact]
    public void Add_DuplicateHash_MovesExistingToFront()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta"));
        h.Add(TextItem("a", "alpha"));

        h.Items.Should().HaveCount(2);
        h.Items[0].Hash.Should().Be("a");
        h.Items[1].Hash.Should().Be("b");
    }

    [Fact]
    public void Add_OverCap_EvictsOldestNonPinned()
    {
        var h = new ClipboardHistory(textCap: 3, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta", pinned: true));
        h.Add(TextItem("c", "gamma"));
        h.Add(TextItem("d", "delta"));

        h.Items.Should().HaveCount(3);
        h.Items.Select(i => i.Hash).Should().BeEquivalentTo(new[] { "d", "c", "b" });
    }

    [Fact]
    public void Pin_FlagsItemAsPinned()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.SetPinned("a", true);

        h.Items[0].IsPinned.Should().BeTrue();
    }

    [Fact]
    public void Delete_RemovesItem()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta"));
        h.Delete("a");

        h.Items.Should().HaveCount(1);
        h.Items[0].Hash.Should().Be("b");
    }

    [Fact]
    public void Clear_WithPreservePinnedTrue_KeepsPinned()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta", pinned: true));
        h.Clear(preservePinned: true);

        h.Items.Should().HaveCount(1);
        h.Items[0].Hash.Should().Be("b");
    }

    [Fact]
    public void Clear_WithPreservePinnedFalse_RemovesEverything()
    {
        var h = new ClipboardHistory(textCap: 10, imageCap: 5);
        h.Add(TextItem("a", "alpha"));
        h.Add(TextItem("b", "beta", pinned: true));
        h.Clear(preservePinned: false);

        h.Items.Should().BeEmpty();
    }

    [Fact]
    public void TextAndImage_HaveSeparateCaps()
    {
        var h = new ClipboardHistory(textCap: 2, imageCap: 1);

        h.Add(TextItem("t1", "t1"));
        h.Add(TextItem("t2", "t2"));
        h.Add(new HistoryItem("i1", HistoryKind.Image, null, "items/i1.png",
            DateTime.UtcNow, null, IsPinned: false, IsSensitive: false));

        h.Items.Where(i => i.Kind == HistoryKind.Text).Should().HaveCount(2);
        h.Items.Where(i => i.Kind == HistoryKind.Image).Should().HaveCount(1);

        h.Add(TextItem("t3", "t3"));
        h.Items.Where(i => i.Kind == HistoryKind.Text).Select(i => i.Hash)
         .Should().BeEquivalentTo(new[] { "t3", "t2" });

        h.Items.Where(i => i.Kind == HistoryKind.Image).Should().HaveCount(1);
    }

    [Fact]
    public void SaveLoad_RoundTripsAllItems()
    {
        using var temp = new TempAppData("ClipTray");
        var indexPath = System.IO.Path.Combine(temp.Path, "index.json");

        var w = new ClipboardHistory(textCap: 10, imageCap: 5);
        w.Add(TextItem("a", "alpha", pinned: true));
        w.Add(TextItem("b", "beta", sensitive: true));
        w.Save(indexPath);

        var r = ClipboardHistory.Load(indexPath, textCap: 10, imageCap: 5);
        r.Items.Should().HaveCount(2);
        r.Items[0].Hash.Should().Be("b");
        r.Items[0].IsSensitive.Should().BeTrue();
        r.Items[1].Hash.Should().Be("a");
        r.Items[1].IsPinned.Should().BeTrue();
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmpty()
    {
        using var temp = new TempAppData("ClipTray");
        var indexPath = System.IO.Path.Combine(temp.Path, "nonexistent.json");

        var h = ClipboardHistory.Load(indexPath, textCap: 10, imageCap: 5);
        h.Items.Should().BeEmpty();
    }

    [Fact]
    public void Load_CorruptFile_ReturnsEmptyAndQuarantines()
    {
        using var temp = new TempAppData("ClipTray");
        var indexPath = System.IO.Path.Combine(temp.Path, "index.json");
        System.IO.File.WriteAllText(indexPath, "{not valid json");

        var h = ClipboardHistory.Load(indexPath, textCap: 10, imageCap: 5);

        h.Items.Should().BeEmpty();
        System.IO.Directory.GetFiles(temp.Path, "*.corrupt-*.json")
            .Should().NotBeEmpty();
    }
}
