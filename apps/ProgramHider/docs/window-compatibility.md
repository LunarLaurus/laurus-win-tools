# Window Compatibility

This document describes what Program Hider can hide, what requires elevation, and what is intentionally or fundamentally unsupported.

## Supported

Program Hider is built around normal top-level desktop windows. In practice, that means:

- visible top-level application windows
- windows with a non-empty title
- windows that are not already hidden by Program Hider
- normal app windows such as browsers, editors, terminals, and many Win32 desktop apps

The current filter logic is implemented in [WindowCatalog.cs](D:/code/program-hider/app/ProgramHider/WindowCatalog.cs).

## Supported With Program Hider Elevated

Some windows can only be manipulated when Program Hider is also running elevated:

- Administrator PowerShell / Windows Terminal / console sessions
- elevated editors, tools, and installers
- other apps started with `Run as administrator`

Why:

- Windows integrity levels block a normal user process from controlling some elevated windows
- Program Hider can relaunch itself as administrator and retry the failed hide path

## Intentionally Unsupported By Current Design

These are excluded on purpose because they are poor tray-hide targets or they break the app model:

- the Windows taskbar (`Shell_TrayWnd`)
- the desktop/program manager (`Progman`)
- owned windows and many secondary modal dialogs
- tool windows (`WS_EX_TOOLWINDOW`)
- child windows rather than top-level windows
- windows that are already hidden or no longer alive

These exclusions are part of the current `WindowCatalog.IsManageableWindow(...)` policy.

## Not Reliably Supportable

Some classes of windows should not be treated as generally hideable, even with more engineering:

- UAC consent / secure desktop prompts
- the lock screen, sign-in UI, and `Ctrl+Alt+Del` security surfaces
- session-isolated windows belonging to another logon session
- highly protected system UI or security products using stronger protections than a normal desktop app
- windows that do not respond to normal Win32 show/hide semantics

Why:

- they may live on a different desktop or in a protected session
- they may be guarded by UIPI, secure desktop rules, or process protection
- hiding them would be unsafe, fragile, or both

## Ambiguous Or Partial Cases

These may work inconsistently depending on the specific app:

- custom-drawn launchers and shell surfaces
- apps that recreate their main window during minimize/restore
- apps with multiple owned helper windows
- some UWP / packaged app surfaces
- apps that aggressively fight foreground or placement changes

When these fail, Program Hider may still enumerate the window but hide/restore behavior can be incomplete or inconsistent.

## Practical Rule Of Thumb

- If it is a normal top-level desktop app window, Program Hider should usually handle it.
- If it is an elevated app window, Program Hider should also be elevated.
- If it is a shell, secure, system, or cross-session surface, assume it is unsupported.
