# SoundTracker Work Plan

## Goal

Turn SoundTracker from a live-session tooltip into a real historical audio event tracker for Windows 10. The app should answer what produced audio, when it started, when it stopped, and what happened recently even if the user was not watching live.

## Current State

- Core Audio session and endpoint callbacks are wired and smoke-tested.
- Tray shell, diagnostics logging, and serialized build/test workflow exist.
- The app still only presents current active sessions in the tray tooltip/menu.
- There is no event history model, no persistence, and no recent-activity UI.

## Work Plan

### 1. Event Model

- Define a durable event record with at least: timestamp, process name, process id when available, session instance id, event type (`started`, `stopped`, `device changed`), and optional duration.
- Normalize system sounds and duplicate process sessions deliberately instead of letting them collapse accidentally.

### 2. Event Capture

- Extend `AudioSessionMonitor` so it emits history-worthy start/stop events, not just current-state changes.
- Track active sessions long enough to compute stop times and durations.
- Guard against noisy callback storms and duplicate events from the same session.

### 3. Persistence

- Add an append-friendly store in `%LOCALAPPDATA%\SoundTracker\`.
- Start with JSONL unless there is a clear reason to jump straight to SQLite.
- Load recent history on startup and apply a retention policy.

### 4. Presentation

- Keep the tray tooltip as a compact summary only.
- Add a proper recent-activity window from the tray menu showing latest events and last-heard times.
- Make "what played recently" the primary user-visible experience.

### 5. Verification

- Extend smoke coverage for event creation, persistence, reload, and history ordering.
- Add a real integration probe that validates history entries are written during live playback.
- Preserve diagnostics logging, but treat it as debugging support rather than the product output.
