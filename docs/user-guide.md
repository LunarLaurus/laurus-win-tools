# Program Hider User Guide

## What Program Hider Does

Program Hider is a Windows tray utility that hides open application windows from the taskbar and keeps them in a tray-managed restore list. It is designed for normal desktop apps, terminals, browsers, editors, and similar top-level windows.

## First Launch

When Program Hider starts, it places an icon in the Windows notification area.

From the tray menu you can:

- hide the current active window
- pick a visible top-level window from `Hide window`
- restore one or more hidden windows
- inspect the active window and create a rule from it
- open settings
- restart Program Hider as administrator

## Basic Usage

### Hide the active window

- focus the window you want to hide
- press the configured hotkey, default `Ctrl+Shift+H`

You can also use the tray menu entry `Hide active window`.

### Hide a specific window

- open the tray menu
- expand `Hide window`
- choose the target window from the list

### Restore windows

- use the tray menu restore entries for quick one-off restores
- use `Restore browser...` to search, filter, and restore several windows at once
- use `Restore all` to bring everything back

## Rules

Rules let Program Hider react automatically when a matching window is minimized.

Rules can match by:

- process name
- window title substring
- window class name

Rules can apply these behaviors:

- auto-hide on minimize
- require PIN/password on restore
- suppress notifications

Use `Inspect active window...` when you want to capture the exact title, process, and class of the current foreground window.

## Settings

Settings are stored in:

`%APPDATA%\ProgramHider\settings.json`

Key settings include:

- hotkey configuration
- launch on Windows startup
- startup delay
- restore without focus steal
- optional restore PIN/password
- separate bulk-restore PIN/password
- unlock timeout
- auto-hide rules

## Elevation And Administrator Windows

If you try to hide an elevated application, such as an Administrator PowerShell window, a normal Program Hider instance may not have permission to control it.

When that happens, Program Hider can:

- prompt to restart itself as administrator
- relaunch with UAC
- retry hiding the same target window automatically

If you routinely hide elevated apps, it is reasonable to start Program Hider elevated from the beginning.

## Logs

Structured logs are written to:

`%APPDATA%\ProgramHider\logs`

These are useful for diagnosing hide failures, hotkey actions, rule behavior, and elevation retry events.

## Limits

Program Hider does not aim to hide every possible Windows surface. Shell-critical, secure-desktop, and protected system UI are intentionally out of scope.

See [window-compatibility.md](D:/code/program-hider/docs/window-compatibility.md) for the support matrix.
