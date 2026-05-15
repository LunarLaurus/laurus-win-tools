using System.Collections.Generic;
using System.Linq;

namespace ClipTray;

public sealed class ClipboardHistory
{
    private readonly List<HistoryItem> _items = new();
    private readonly int _textCap;
    private readonly int _imageCap;

    public ClipboardHistory(int textCap, int imageCap)
    {
        _textCap = textCap;
        _imageCap = imageCap;
    }

    public IReadOnlyList<HistoryItem> Items => _items;

    public void Add(HistoryItem item)
    {
        var existingIdx = _items.FindIndex(i => i.Hash == item.Hash);
        if (existingIdx >= 0)
        {
            var existing = _items[existingIdx] with { CapturedUtc = item.CapturedUtc };
            _items.RemoveAt(existingIdx);
            _items.Insert(0, existing);
            return;
        }

        _items.Insert(0, item);
        EnforceCap(item.Kind);
    }

    public void SetPinned(string hash, bool pinned)
    {
        var idx = _items.FindIndex(i => i.Hash == hash);
        if (idx < 0) return;
        _items[idx] = _items[idx] with { IsPinned = pinned };
    }

    public void Delete(string hash)
    {
        _items.RemoveAll(i => i.Hash == hash);
    }

    public void Clear(bool preservePinned)
    {
        if (preservePinned)
            _items.RemoveAll(i => !i.IsPinned);
        else
            _items.Clear();
    }

    private void EnforceCap(HistoryKind kind)
    {
        int cap = kind == HistoryKind.Text ? _textCap : _imageCap;

        while (_items.Count(i => i.Kind == kind) > cap)
        {
            var victim = _items.AsEnumerable().Reverse()
                .FirstOrDefault(i => i.Kind == kind && !i.IsPinned);
            if (victim is null) break;
            _items.Remove(victim);
        }
    }

    public void Save(string indexPath)
    {
        var index = new HistoryIndex { Items = _items.ToList() };
        var json = System.Text.Json.JsonSerializer.Serialize(index, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });

        var tmp = indexPath + ".tmp";
        System.IO.File.WriteAllText(tmp, json);
        if (System.IO.File.Exists(indexPath)) System.IO.File.Replace(tmp, indexPath, destinationBackupFileName: null);
        else System.IO.File.Move(tmp, indexPath);
    }

    public static ClipboardHistory Load(string indexPath, int textCap, int imageCap)
    {
        var h = new ClipboardHistory(textCap, imageCap);
        if (!System.IO.File.Exists(indexPath)) return h;

        try
        {
            var json = System.IO.File.ReadAllText(indexPath);
            var index = System.Text.Json.JsonSerializer.Deserialize<HistoryIndex>(json);
            if (index?.Items is { } items)
            {
                h._items.AddRange(items);
            }
            return h;
        }
        catch
        {
            var dir = System.IO.Path.GetDirectoryName(indexPath) ?? ".";
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var quarantine = System.IO.Path.Combine(dir, $"index.corrupt-{stamp}.json");
            try { System.IO.File.Move(indexPath, quarantine); } catch { }
            return new ClipboardHistory(textCap, imageCap);
        }
    }
}
