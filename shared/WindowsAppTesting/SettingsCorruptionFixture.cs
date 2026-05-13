namespace WindowsAppTesting;

/// <summary>
/// Writes intentionally malformed settings files to exercise corruption-recovery paths
/// in JsonSettingsStore.
/// </summary>
public static class SettingsCorruptionFixture
{
    public static void WriteCorrupt(string path, string content = "{ this is: not valid json }")
        => File.WriteAllText(path, content);

    public static void WriteTruncated(string path)
        => File.WriteAllText(path, "{\"version\":");

    public static void WriteEmpty(string path)
        => File.WriteAllText(path, "");

    public static void WriteNullRoot(string path)
        => File.WriteAllText(path, "null");
}
