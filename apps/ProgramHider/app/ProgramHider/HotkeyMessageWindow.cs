using System.Windows.Forms;

namespace ProgramHider;

// Hidden message-only window used as the target for WM_HOTKEY delivery.
internal sealed class HotkeyMessageWindow : NativeWindow, IDisposable
{
    private readonly Action<int> _onHotkey;

    public HotkeyMessageWindow(Action<int> onHotkey)
    {
        _onHotkey = onHotkey;
        CreateHandle(new CreateParams
        {
            Caption = "ProgramHiderMessageWindow"
        });
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == NativeMethods.WM_HOTKEY)
        {
            _onHotkey(message.WParam.ToInt32());
        }

        base.WndProc(ref message);
    }

    public void Dispose()
    {
        DestroyHandle();
    }
}
