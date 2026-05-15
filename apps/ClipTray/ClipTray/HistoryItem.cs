namespace ClipTray;

public enum HistoryKind { Text, Image }

public sealed record HistoryItem(
    string Hash,
    HistoryKind Kind,
    string? Text,
    string? ImagePath,
    DateTime CapturedUtc,
    string? SourceProcessName,
    bool IsPinned,
    bool IsSensitive);
