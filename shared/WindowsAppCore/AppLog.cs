using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsAppCore;

/// <summary>
/// Structured JSONL logger. Normal log path — do not use for fatal/pre-startup events;
/// use <see cref="CrashSink"/> for those.
/// </summary>
public sealed class AppLog : IDisposable
{
    private static readonly JsonSerializerOptions SerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _appName;
    private readonly string _version;
    private readonly IClock _clock;
    private readonly JsonLineWriter _writer;

    /// <summary>Production constructor — log dir resolved via <see cref="AppPaths"/>.</summary>
    public AppLog(string appName, string appVersion)
        : this(appName, appVersion, AppPaths.LogDir(appName)) { }

    /// <summary>Testing constructor — caller supplies an explicit log directory and optional fake clock.</summary>
    internal AppLog(string appName, string appVersion, string logDirectory, IClock? clock = null)
    {
        _appName = appName;
        _version = appVersion;
        _clock = clock ?? SystemClock.Instance;
        _writer = new JsonLineWriter(logDirectory, appName.ToLowerInvariant(), clock: clock);
    }

    public string LogPath => _writer.CurrentPath;

    public void Info(string evt, object? data = null) => Write("info", evt, data);
    public void Warn(string evt, object? data = null) => Write("warn", evt, data);

    public void Error(string evt, Exception? ex = null, object? data = null)
    {
        object? errorData = ex == null ? data
            : data == null ? (object)new { type = ex.GetType().Name, message = ex.Message, stack = ex.StackTrace }
            : new { type = ex.GetType().Name, message = ex.Message, stack = ex.StackTrace, extra = data };
        Write("error", evt, errorData);
    }

    public void Dispose() => _writer.Dispose();

    private void Write(string level, string evt, object? data)
    {
        var entry = new LogEntry(
            Ts: _clock.UtcNow.ToString("O"),
            App: _appName,
            V: _version,
            Evt: evt,
            Level: level,
            Data: data);
        _writer.Write(JsonSerializer.Serialize(entry, SerOptions));
    }

    private sealed record LogEntry(string Ts, string App, string V, string Evt, string Level, object? Data);
}
