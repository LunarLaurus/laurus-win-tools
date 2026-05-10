# Testing

## Unit And Integration Harness

Run the repo-local test host:

```powershell
D:\tooling\dotnet\dotnet.exe run --project tests\ProgramHider.TestHost\ProgramHider.TestHost.csproj -c Release
```

Current coverage includes:

- rule matching
- merged rule behavior
- settings normalization and legacy-rule migration
- rule-protected PIN preservation
- startup option parsing
- window lookup with process filtering
- hide/restore/prune behavior against a fake window platform

## Live Smoke Test

Build the solution first:

```powershell
D:\tooling\dotnet\dotnet.exe build ProgramHider.sln -c Release
```

Then run the isolated smoke test:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\smoke-test-sample-window.ps1
```

The smoke test:

- launches `tests/ProgramHider.SmokeWindow`
- waits for the sample window to appear
- verifies lookup through `ProgramHider.TestHost`
- hides the sample window
- verifies it is no longer visible
- restores the sample window
- verifies it is visible again

## Notes

- The PowerShell custom-title target was dropped because it was not a reliable probe on this machine.
- The sample smoke window keeps live verification self-contained and avoids touching system or user windows.
