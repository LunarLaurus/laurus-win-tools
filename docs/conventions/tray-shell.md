# Tray Shell

`TrayShell` is a compositional helper that owns the `NotifyIcon` lifecycle and shared tray plumbing. It is not a base class — apps hold it as a field.

## What TrayShell Owns

- `NotifyIcon` construction and visibility lifecycle
- Tray icon disposal via `TrayIconManager` (including GDI `HICON` cleanup)
- Tooltip length enforcement (63-char WinAPI limit)
- `TrayTheme.Changed` subscription and automatic icon refresh trigger
- Optional `SingleInstanceActivation` listener hookup
- Notifier backend exposure via `IUserNotifier`

## What the App Owns

- Menu item creation and rebuilding
- Left-click vs right-click semantics
- Forms and window presentation
- Timers and domain refresh loops
- System hooks, network hooks, hotkeys
- Startup configuration UX
- App-specific status derivation and dirty-key logic

## API

```csharp
public sealed class TrayShell : IDisposable
{
    public NotifyIcon       NotifyIcon { get; }
    public ContextMenuStrip Menu       { get; }
    public TrayIconManager  Icons      { get; }
    public IUserNotifier    Notifier   { get; }

    public event CancelEventHandler?  MenuOpening;
    public event MouseEventHandler?   IconMouseClick;
    public event MouseEventHandler?   IconMouseDoubleClick;
    public event EventHandler?        ActivationRequested;   // from SingleInstanceActivation

    public TrayShell(
        UiDispatcher            ui,
        ContextMenuStrip        menu,
        ITrayIconProvider       iconProvider,
        TrayTheme               theme,
        IUserNotifier           notifier,
        SingleInstanceActivation? activation = null);

    public void SetTooltip(string text);   // truncates to 63 chars internally
    public void ShowMenuAtCursor();
    public void BeginShutdown();
}
```

## Host Independence

Three apps use `ApplicationContext` as their tray host; NetProfileSwitcher uses a `Form`. `TrayShell` works as a field in either. It does not inherit from and does not require `ApplicationContext`.

## Per-App Mapping

| App | Host type | Shell sits in | Notes |
|---|---|---|---|
| BatteryTray | `ApplicationContext` | `BatteryTrayContext` field | Shell takes over icon + activation; app keeps refresh, notification, and power logic |
| SoundTracker | `ApplicationContext` | `TrayApplicationContext` field | Shell owns icon plumbing; app keeps audio monitoring and left-click timer |
| ProgramHider | `ApplicationContext` | `ProgramHiderContext` field | Shell owns generic tray infra; app keeps all window, hotkey, and rule logic |
| NetProfileSwitcher | `Form` | `MainForm` field | Shell works as a field inside the form; minimize-to-tray stays app-owned |

## Lifecycle

1. App builds domain objects and `ContextMenuStrip`
2. App constructs `TrayShell`
3. App subscribes to shell events (`MenuOpening`, `IconMouseClick`, `ActivationRequested`, etc.)
4. App performs initial menu / status / icon refresh
5. On domain state change: app calls `Icons.RequestRefresh()` and `SetTooltip(...)`
6. On shutdown: app calls `BeginShutdown()`, tears down its own resources, disposes `TrayShell`

## Click Policy

Click routing is app-owned. `TrayShell` surfaces `IconMouseClick` and `IconMouseDoubleClick` events but imposes no policy on which button does what. Each app wires these handlers according to its own UX.

`ShowMenuAtCursor()` is provided as a utility for the common case of showing the context menu at the cursor position — apps call it from their click handler when appropriate.
