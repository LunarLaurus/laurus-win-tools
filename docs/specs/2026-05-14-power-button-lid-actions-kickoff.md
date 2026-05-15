# Kickoff Brief — BatteryTray Hardware Actions Feature

**Purpose:** Self-contained prompt for a fresh Claude Code session to execute the configurable-power-button-and-lid-close feature. The new session has no conversation history; this brief is everything it needs.

**How to use this file:**

1. Start a new Claude Code session in `D:\code\windows-apps\`. Use elevated permissions if Task 8's admin-gated E2E test must pass during the run.
2. Either:
   - Paste the **Prompt Block** below directly as the first message, or
   - Type `read docs/specs/2026-05-14-power-button-lid-actions-kickoff.md and execute` and let the session resolve the rest from the file.
3. The executing session will run through the 11-task plan, committing per task with detailed per-project-perspective messages, and surface any blocker (UAC declined, test failure, ambiguous code) for your decision rather than improvising.

**Elevation note:** Most of the plan runs unelevated. The exceptions are (1) Task 8's `ApplyToAllPlans_OnElevatedSession_RoundTripsThroughReadCurrent` E2E test, which requires admin and will skip cleanly on a standard session; and (2) Task 11's manual smoke step, which exercises the real UAC prompt from the Settings dialog. If you start the executing session from an elevated PowerShell, both will be covered. If from a standard shell, the elevated E2E test will be skipped (test still passes as a no-op) and you can do the manual smoke separately.

**Expected duration:** 60–90 minutes of executor time, dominated by file writes and `dotnet test` runs. No external dependencies, no network calls, no package installs.

---

## Prompt Block

> You are picking up work in this repository: a private Windows monorepo of four utility apps (BatteryTray, NetProfileSwitcher, ProgramHider, SoundTracker) sharing three libraries (WindowsAppCore, WindowsTrayCore, WindowsAppTesting). Before doing anything else, read these files in order:
>
> 1. `CLAUDE.md` (project rules — note especially: no AI authorship anywhere in commits/code/docs, every discrete unit of work gets its own commit, WORKLOG.md updated on every commit, TDD discipline, real system interactions preferred)
> 2. `WORKLOG.md` — look for the `## 2026-05-14` entry labelled "Phase 27 design" (you are about to do Phase 27 implementation)
> 3. `docs/specs/2026-05-14-power-button-lid-actions.md` (the authoritative design spec)
> 4. `docs/specs/2026-05-14-power-button-lid-actions-plan.md` (the TDD-ordered 11-task implementation plan)
>
> Your job: execute the plan. The plan is the source of truth for ordering, code content, commands, and commit messages. Treat it as the executable artifact, not as a starting point you may rewrite. Where the plan gives you exact code in a step, write that code verbatim (modulo trivial whitespace / line-ending adjustments). Where the plan gives you a command and expected output, run the command and verify the output before moving on.
>
> Discipline:
>
> - One commit per task as the plan defines them. Use the commit message from the plan's `git commit -m "..."` step — do not reword unless you spot an error.
> - **Never** include AI-authorship strings: no "Co-Authored-By: Claude...", no "Generated with Claude Code", no "🤖", no mention of Anthropic in commits, code, or documentation.
> - **Never** create or commit files matching `claude-*`, `codex-*`, `*.claude`, `.claude/`, `CLAUDE.md`, `AGENTS.md`, `claude.md`, or any AI-tool-named file. These are gitignored.
> - **Never** use em-dashes or double-hyphens in any prose you write into spec docs or commit messages. Use periods, semicolons, colons, or parentheses instead.
> - TDD: in every task where the plan writes a test first, write the test first and run it to confirm it fails for the stated reason before implementing.
> - Tooling: PowerShell is the project shell. Use `Bash` tool for POSIX-style commands or `PowerShell` for cmdlets. `dotnet build`, `dotnet test`, and `git` work the same in both.
> - Naval voice (space navy, not maritime) is fine in prose you write into commit messages, but keep it tight. Code stays neutral.
>
> Stop conditions — surface to the user rather than improvise:
>
> - Any test fails in a way the plan did not anticipate
> - A step's expected command output does not match the actual output (and the deviation is not obviously benign whitespace)
> - The plan references a file path that does not exist or a method signature that has drifted in the codebase since the plan was written
> - Anything ambiguous, especially around the Save handler in `SettingsForm.cs` (Task 10 step 4 — the plan correctly notes the handler's existing name may vary)
> - You hit a `Win32Exception` with a code other than 1223 during Task 8's E2E elevated apply — this means something genuinely surprising happened; do not catch-and-continue
>
> Completion criteria — only declare "done" when ALL of these hold:
>
> 1. All 11 tasks committed, with the WORKLOG.md entry from Task 11 step 5 committed in the final commit
> 2. `dotnet build` succeeds with 0 warnings, 0 errors across every project
> 3. Every non-E2E test project passes (`BatteryTray.Tests`, `WindowsAppCore.Tests`, `WindowsTrayCore.Tests`, `NetProfileSwitcher.Tests`, `SoundTracker.Tests`, `ProgramHider.Tests`)
> 4. `BatteryTray.E2ETests` either passes (admin shell) or passes-with-no-op (standard shell, elevated test self-skips)
> 5. `git status` shows a clean working tree
> 6. `git log` shows commits per task with the plan's wording
>
> Begin by reading the four files listed above, then proceed to Task 1.

---

## Reference — Files Produced This Session

These already exist in the repo at the start of the executing session:

| File | Commit | Purpose |
|---|---|---|
| `docs/specs/2026-05-14-power-button-lid-actions.md` | `bbea0ab` | Design spec — locked decisions, architecture, data flow, failure modes, file manifest, caveats |
| `docs/specs/2026-05-14-power-button-lid-actions-plan.md` | `4e2e7a2` | TDD plan — 11 tasks, complete code in every step, exact commands and expected outputs |
| `docs/specs/2026-05-14-power-button-lid-actions-kickoff.md` | (this commit) | This brief |
| `WORKLOG.md` | `bbea0ab` | Updated with the Phase 27 design entry; Task 11 of the plan adds the Phase 27 implementation entry |

## Reference — Constraints Pulled Forward From CLAUDE.md

The executing session will read CLAUDE.md itself, but for quick verification:

- **No AI authorship anywhere.** No co-authored-by lines, no Anthropic/Claude mentions in commits, code, comments, or docs.
- **Every commit detailed.** Project-perspective wording, what changed and why. The plan already provides these — do not strip detail.
- **WORKLOG.md updated on every commit.** The plan handles this in Task 11's final commit covering the whole Phase 27 series.
- **TDD throughout.** Tests precede implementation in every behaviour-carrying task.
- **Real system interactions preferred.** The E2E test in Task 8 hits real powercfg, real plans, real values — saving the originals and restoring in finally.
- **`WindowsFact` / `WindowsTheory` for hardware-dependent tests.** Already used by Task 7 / Task 8 E2E tests via the existing `BatteryTray.E2ETests.WindowsFactAttribute`.
- **Match existing conventions.** The plan follows the existing migration pattern, the existing controller pattern (`PowerPlanController`), the existing test patterns (`AppSettingsMigrationTests`, `BatteryMonitorE2ETests`).

## Reference — Verification Commands the Executor Will Run

The plan inlines the relevant commands per task. For a single end-of-run verification, the executor runs:

```powershell
# 1. Full build of every project
dotnet build

# 2. Full test suite (non-E2E)
dotnet test apps\BatteryTray\BatteryTray.Tests\BatteryTray.Tests.csproj
dotnet test shared\WindowsAppCore.Tests\WindowsAppCore.Tests.csproj
dotnet test shared\WindowsTrayCore.Tests\WindowsTrayCore.Tests.csproj
dotnet test apps\NetProfileSwitcher.Tests\NetProfileSwitcher.Tests.csproj
dotnet test apps\SoundTracker\SoundTracker.Tests\SoundTracker.Tests.csproj
dotnet test apps\ProgramHider\tests\ProgramHider.Tests\ProgramHider.Tests.csproj

# 3. E2E (admin shell strongly preferred to cover Task 8's apply round-trip)
dotnet test apps\BatteryTray\BatteryTray.E2ETests\BatteryTray.E2ETests.csproj

# 4. Clean state check
git status
git log --oneline -15
```

## Reference — Manual Smoke After Execution

The plan's Task 11 step 4 lists the manual smoke sequence. Reproduced here verbatim for quick reference at the keyboard:

1. Launch `apps\BatteryTray\BatteryTray\bin\Debug\net8.0-windows10.0.19041.0\BatteryTray.exe`
2. Tray right-click → Settings → Hardware actions tab
3. Note current values
4. Change Power button to "Do nothing", Lid close to "Sleep"
5. Click Save → UAC prompt → consent
6. Confirm dialog closes cleanly
7. Re-open Settings → Hardware actions → values match what you set
8. Close laptop lid → system sleeps
9. Wake; press power button → nothing happens (DoNothing in effect)
10. Re-open Settings → Hardware actions → set Power button back to "Sleep"
11. Click Save → UAC → consent
12. Verify by checking Control Panel → Power Options → "Choose what the power button does" matches
