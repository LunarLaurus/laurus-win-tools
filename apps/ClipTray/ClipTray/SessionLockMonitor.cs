using System.Windows.Forms;

namespace ClipTray;

internal sealed class SessionLockMonitor : IDisposable
{
    private readonly MessageWindow _window;
    private bool _registered;
    private bool _disposed;

    public SessionLockMonitor()
    {
        _window = new MessageWindow(this);
        _window.CreateHandle(new CreateParams());
        _registered = NativeMethods.WTSRegisterSessionNotification(
            _window.Handle, NativeMethods.NOTIFY_FOR_THIS_SESSION);
    }

    public bool IsLocked { get; private set; }
    public event EventHandler? LockStateChanged;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_registered) NativeMethods.WTSUnRegisterSessionNotification(_window.Handle);
        _window.DestroyHandle();
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly SessionLockMonitor _owner;
        public MessageWindow(SessionLockMonitor owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_WTSSESSION_CHANGE)
            {
                var was = _owner.IsLocked;
                if ((int)m.WParam == NativeMethods.WTS_SESSION_LOCK)        _owner.IsLocked = true;
                else if ((int)m.WParam == NativeMethods.WTS_SESSION_UNLOCK) _owner.IsLocked = false;
                if (_owner.IsLocked != was)
                    _owner.LockStateChanged?.Invoke(_owner, EventArgs.Empty);
            }
            base.WndProc(ref m);
        }
    }
}
