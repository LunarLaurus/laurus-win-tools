# SoundTracker Work Plan

## Current Product State

SoundTracker is already a historical audio event tracker for Windows 10. The app captures session start and stop activity plus default-device changes, persists recent history to `%LOCALAPPDATA%\SoundTracker\history\audio-activity.jsonl`, and presents both `Active now` and `Recent` state through the tray menu and the Recent Activity window.

The tray shell also tracks endpoint volume and mute state, renders a theme-aware custom tray icon, supports multiline tooltip summaries, opens the Windows mixer on single left click, and opens history on double click.

## Current Verification State

- `.\build.ps1` serializes builds and smoke runs to avoid app/test overlap.
- The smoke suite uses real playback, real Core Audio callbacks, real JSONL writes, and real Recent Activity window rendering.
- Current automated coverage includes lifecycle, endpoint volume, multiline tooltip content, live callback delivery, persisted history reload, and tray summary state.

## Remaining Work

### 1. Tray Replacement Polish

- Keep refining tooltip readability within the shell text limit.
- Improve tray icon clarity across light and dark taskbar themes and different DPI scales.
- Verify shell interaction edge cases after Explorer restarts or default-device churn.

### 2. History UX

- Add stronger filtering or grouping in Recent Activity when many events accumulate.
- Decide whether retention should stay JSONL-only or move to a richer local store later.
- Consider exposing last-heard time and duration more prominently in the history view.

### 3. Verification Gaps

- Add automated coverage for dynamic icon rendering invariants where practical.
- Keep manual checks for true Explorer tray behavior, mixer launch behavior, and disruptive hardware/device-switch scenarios that are not safe to simulate in routine smoke runs.
