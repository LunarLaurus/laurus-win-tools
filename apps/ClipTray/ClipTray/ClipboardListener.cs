using System.Windows.Forms;

namespace ClipTray;

/// <summary>
/// Wraps Win32 AddClipboardFormatListener. Owns a hidden NativeWindow that
/// receives WM_CLIPBOARDUPDATE; raises <see cref="ClipboardChanged"/> on
/// the application message-pump thread.
/// </summary>
internal sealed class ClipboardListener : IDisposable
{
    private readonly MessageWindow _window;
    private bool _registered;
    private bool _disposed;

    public ClipboardListener()
    {
        _window = new MessageWindow(this);
        _window.CreateHandle(new CreateParams());
        _registered = NativeMethods.AddClipboardFormatListener(_window.Handle);
    }

    public event EventHandler? ClipboardChanged;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_registered)
        {
            NativeMethods.RemoveClipboardFormatListener(_window.Handle);
            _registered = false;
        }
        _window.DestroyHandle();
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly ClipboardListener _owner;
        public MessageWindow(ClipboardListener owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
            {
                _owner.ClipboardChanged?.Invoke(_owner, EventArgs.Empty);
                return;
            }
            base.WndProc(ref m);
        }
    }
}
