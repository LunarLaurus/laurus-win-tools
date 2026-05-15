using System.Collections.Generic;
using System.Windows.Forms;

namespace WindowsTrayCore;

[Flags]
public enum HotkeyModifiers
{
    None    = 0,
    Alt     = 1,
    Control = 2,
    Shift   = 4,
    Win     = 8,
}

/// <summary>
/// Global hotkey registration wrapper. Owns a hidden NativeWindow that
/// receives WM_HOTKEY; raises <see cref="Pressed"/> with the registered id
/// on the application message-pump thread.
/// </summary>
public sealed class HotkeyRegistration : IDisposable
{
    private readonly MessageWindow _window;
    private readonly HashSet<int> _registered = new();
    private bool _disposed;

    public HotkeyRegistration()
    {
        _window = new MessageWindow(this);
        _window.CreateHandle(new CreateParams());
    }

    public event EventHandler<int>? Pressed;

    public bool Register(int id, HotkeyModifiers modifiers, Keys key)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HotkeyRegistration));
        if (!_registered.Add(id)) return false;

        if (Native.TrayNativeMethods.RegisterHotKey(_window.Handle, id, (uint)modifiers, (uint)key))
            return true;

        _registered.Remove(id);
        return false;
    }

    public bool Unregister(int id)
    {
        if (_disposed) return false;
        if (!_registered.Remove(id)) return false;
        return Native.TrayNativeMethods.UnregisterHotKey(_window.Handle, id);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var id in _registered)
            Native.TrayNativeMethods.UnregisterHotKey(_window.Handle, id);
        _registered.Clear();
        _window.DestroyHandle();
    }

    private sealed class MessageWindow : NativeWindow
    {
        private readonly HotkeyRegistration _owner;
        public MessageWindow(HotkeyRegistration owner) => _owner = owner;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.TrayNativeMethods.WM_HOTKEY)
            {
                _owner.Pressed?.Invoke(_owner, (int)m.WParam);
                return;
            }
            base.WndProc(ref m);
        }
    }
}
