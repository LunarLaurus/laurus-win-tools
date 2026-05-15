using System.Collections.Generic;

namespace ClipTray;

internal sealed class HistoryIndex
{
    public int SchemaVersion { get; set; } = 1;
    public List<HistoryItem> Items { get; set; } = new();
}
