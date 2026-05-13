using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Channels;

namespace WindowsAppCore;

/// <summary>
/// Appends JSONL lines to a daily-rotating log file using a background drain thread.
/// Thread-safe: Write() may be called from any thread; all I/O is on the drain thread.
/// </summary>
public sealed class JsonLineWriter : IDisposable
{
    private readonly string _directory;
    private readonly string _filePrefix;
    private readonly long _maxSizeBytes;
    private readonly int _retentionDays;
    private readonly Channel<string> _channel;
    private readonly Thread _drainThread;
    private StreamWriter? _writer;
    private long _currentFileBytes;
    private string _currentDate = string.Empty;
    private bool _disposed;

    public JsonLineWriter(
        string directory,
        string filePrefix,
        long maxSizeBytes = 50L * 1024 * 1024,
        int retentionDays = 30)
    {
        _directory = directory;
        _filePrefix = filePrefix;
        _maxSizeBytes = maxSizeBytes;
        _retentionDays = retentionDays;
        _channel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });

        Directory.CreateDirectory(directory);
        PruneOldFiles();

        _drainThread = new Thread(DrainLoop)
        {
            IsBackground = true,
            Name = $"JsonLineWriter-{filePrefix}"
        };
        _drainThread.Start();
    }

    public string CurrentPath =>
        Path.Combine(_directory, $"{_filePrefix}-{DateTime.UtcNow:yyyyMMdd}.jsonl");

    public void Write(string line)
    {
        if (!_disposed)
            _channel.Writer.TryWrite(line);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _channel.Writer.Complete();
        _drainThread.Join(TimeSpan.FromSeconds(5));
        _writer?.Dispose();
        _writer = null;
    }

    private void DrainLoop()
    {
        var reader = _channel.Reader;
        try
        {
            while (true)
            {
                // Block up to 500 ms waiting for data (or until channel is completed)
                using var cts = new CancellationTokenSource(500);
                try
                {
                    bool available = reader.WaitToReadAsync(cts.Token).AsTask()
                        .GetAwaiter().GetResult();
                    if (!available) break; // channel completed with no items remaining
                }
                catch (OperationCanceledException)
                {
                    // 500 ms elapsed — fall through to flush whatever is queued
                }

                Drain(reader, 50);
            }
        }
        catch { }

        // Final drain after channel is marked complete
        Drain(reader, int.MaxValue);
        try { _writer?.Flush(); } catch { }
    }

    private void Drain(ChannelReader<string> reader, int maxLines)
    {
        int count = 0;
        while (count < maxLines && reader.TryRead(out var line))
        {
            try
            {
                EnsureFile();
                _writer!.WriteLine(line);
                _currentFileBytes += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
                count++;
            }
            catch { }
        }
        if (count > 0)
        {
            try { _writer?.Flush(); } catch { }
        }
    }

    private void EnsureFile()
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");

        // Daily rollover
        if (date != _currentDate)
        {
            CloseWriter();
            _currentDate = date;
            _currentFileBytes = 0;
        }

        // Size cap: roll current file to a numbered copy
        if (_writer != null && _currentFileBytes >= _maxSizeBytes)
        {
            CloseWriter();
            _currentFileBytes = 0;
            var current = Path.Combine(_directory, $"{_filePrefix}-{date}.jsonl");
            if (File.Exists(current))
                File.Move(current, FindNextRollPath(date), overwrite: false);
        }

        // Open for append if needed
        if (_writer == null)
        {
            var path = Path.Combine(_directory, $"{_filePrefix}-{date}.jsonl");
            var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            _currentFileBytes = stream.Length;
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };
            _currentDate = date;
        }
    }

    private void CloseWriter()
    {
        try { _writer?.Flush(); } catch { }
        try { _writer?.Dispose(); } catch { }
        _writer = null;
    }

    private string FindNextRollPath(string date)
    {
        for (int i = 1; i < 10000; i++)
        {
            var path = Path.Combine(_directory, $"{_filePrefix}-{date}-{i}.jsonl");
            if (!File.Exists(path)) return path;
        }
        return Path.Combine(_directory, $"{_filePrefix}-{date}-overflow.jsonl");
    }

    private void PruneOldFiles()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
            foreach (var file in Directory.GetFiles(_directory, $"{_filePrefix}-*.jsonl"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                    try { File.Delete(file); } catch { }
            }
        }
        catch { }
    }
}
