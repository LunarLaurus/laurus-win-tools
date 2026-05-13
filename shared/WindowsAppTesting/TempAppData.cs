namespace WindowsAppTesting;

/// <summary>
/// Redirects AppPaths to a temporary directory for the duration of a test.
/// Sets the {APPNAME}_DATA environment variable override and cleans up on dispose.
/// </summary>
public sealed class TempAppData : IDisposable
{
    private readonly string _envKey;
    private bool _disposed;

    public string Path { get; }

    public TempAppData(string appName)
    {
        _envKey = appName.ToUpperInvariant().Replace('-', '_').Replace(' ', '_') + "_DATA";
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{appName}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
        Environment.SetEnvironmentVariable(_envKey, Path);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Environment.SetEnvironmentVariable(_envKey, null);
        try { Directory.Delete(Path, recursive: true); } catch { }
    }
}
