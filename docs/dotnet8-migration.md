# .NET 8 / C# Migration Record

## Status

This migration is complete. The active app now lives in [`SoundTracker.App/`](../SoundTracker.App), and the original Rust code has been preserved under [`archive/rust-legacy/`](../archive/rust-legacy).

## Why the repo moved off Rust

The original app was a small Windows tray utility built around direct Core Audio and Win32 interop in Rust. That kept the binary small, but it also concentrated low-level COM and shell behavior into a narrow code path that was harder to maintain. In the final Rust checkout, `cargo check` was already failing against the `windows = 0.58.0` surface, so the `.NET 8` move simplified maintenance instead of just changing languages for its own sake.

## Current .NET shape

The live repo layout is:

```text
archive/
  rust-legacy/
docs/
  dotnet8-migration.md
  work-plan.md
SoundTracker.App/
  Program.cs
  TrayApplicationContext.cs
  TrayIconRenderer.cs
  TooltipFormatter.cs
  RecentActivityForm.cs
  Audio/
  History/
  Processes/
SoundTracker.SmokeTests/
```

The current implementation is event-driven, not polling-based. Core Audio callbacks drive active-session updates, history capture, endpoint volume tracking, and tray refreshes.

## What the migration delivered

- `.NET 8` / `C# 12` WinForms tray shell
- Core Audio interop isolated under `SoundTracker.App/Audio`
- persisted audio history under `%LOCALAPPDATA%\SoundTracker\history\audio-activity.jsonl`
- Recent Activity UI plus tray `Active now` / `Recent` summaries
- theme-aware dynamically rendered tray icon with volume and mute state
- repo-local smoke coverage in `SoundTracker.SmokeTests/`

## Validation path

Use the repo-local build script:

```powershell
.\build.ps1
```

If a previous app or smoke run is still open:

```powershell
.\build.ps1 -StopRunningProcesses
```

This document is historical context for the migration itself. For current contributor guidance, use [`AGENTS.md`](../AGENTS.md) and [`docs/work-plan.md`](work-plan.md).
