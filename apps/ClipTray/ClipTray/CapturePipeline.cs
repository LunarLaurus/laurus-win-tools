using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace ClipTray;

internal sealed class CapturePipeline
{
    private readonly AppSettings _settings;
    private readonly ClipboardHistory _history;
    private readonly ImageStore _images;
    private readonly SessionLockMonitor _lock;
    private readonly string _indexPath;

    public CapturePipeline(
        AppSettings settings,
        ClipboardHistory history,
        ImageStore images,
        SessionLockMonitor sessionLock,
        string indexPath)
    {
        _settings = settings;
        _history = history;
        _images = images;
        _lock = sessionLock;
        _indexPath = indexPath;
    }

    public void OnClipboardChanged()
    {
        // Privacy filter, evaluated in spec order.
        if (_settings.PauseCapture) return;
        if (_settings.PauseOnLockScreen && _lock.IsLocked) return;
        if (HasSkipSyncMarker()) return;
        if (IsForegroundProcessBlocked()) return;

        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText();
                if (string.IsNullOrEmpty(text)) return;
                CaptureText(text);
            }
            else if (Clipboard.ContainsImage())
            {
                CaptureImage();
            }
        }
        catch
        {
            // Another process may be holding the clipboard; drop this update.
            // The next legitimate copy fires another WM_CLIPBOARDUPDATE.
        }
    }

    private static bool HasSkipSyncMarker()
    {
        try
        {
            if (Clipboard.ContainsData("CanIncludeInClipboardHistory"))
            {
                if (Clipboard.GetData("CanIncludeInClipboardHistory") is byte[] b && b.Length >= 1 && b[0] == 0)
                    return true;
            }
            if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing"))
                return true;
        }
        catch { }
        return false;
    }

    private bool IsForegroundProcessBlocked()
    {
        var name = ForegroundProcessProbe.GetCurrentName();
        if (name is null) return false;
        return _settings.ForegroundBlocklist.Any(n =>
            string.Equals(n, name, System.StringComparison.OrdinalIgnoreCase));
    }

    private void CaptureText(string text)
    {
        var canonical = text.Replace("\r\n", "\n");
        var hash = Sha256(Encoding.UTF8.GetBytes(canonical));
        bool sensitive = _settings.PasswordHeuristicEnabled
            && PasswordHeuristic.LooksLikeSecret(text,
                _settings.PasswordHeuristicMinLength,
                _settings.PasswordHeuristicMaxLength);

        _history.Add(new HistoryItem(
            Hash: hash,
            Kind: HistoryKind.Text,
            Text: text,
            ImagePath: null,
            CapturedUtc: System.DateTime.UtcNow,
            SourceProcessName: ForegroundProcessProbe.GetCurrentName(),
            IsPinned: false,
            IsSensitive: sensitive));
        _history.Save(_indexPath);
    }

    private void CaptureImage()
    {
        using var img = Clipboard.GetImage();
        if (img is null) return;
        using var ms = new MemoryStream();
        img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        var bytes = ms.ToArray();
        var hash = Sha256(bytes);
        var path = _images.Write(hash, bytes);

        _history.Add(new HistoryItem(
            Hash: hash,
            Kind: HistoryKind.Image,
            Text: null,
            ImagePath: path,
            CapturedUtc: System.DateTime.UtcNow,
            SourceProcessName: ForegroundProcessProbe.GetCurrentName(),
            IsPinned: false,
            IsSensitive: false));
        _history.Save(_indexPath);
    }

    private static string Sha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return System.Convert.ToHexString(hash).ToLowerInvariant();
    }
}
