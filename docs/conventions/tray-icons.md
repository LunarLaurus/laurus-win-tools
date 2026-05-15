# Tray Icons

The rule on icon generation for tray apps in this repo:

| Icon role | Approach | Reference |
|---|---|---|
| **Static brand glyph** | Build-time `.ico` via `tools/IconGen` | ClipTray, ProgramHider |
| **Stateful indicator** | Runtime GDI+ draw via `IconBuilder.FromBitmap` | BatteryTray percent, SoundTracker volume |

These are complementary, not alternatives. Every app needs a static `.ico` baked into the exe regardless of whether it also does runtime drawing.

## Why every app needs a static `.ico`

The exe's icon is consumed on six surfaces. Only one of them is runtime-swappable:

| Surface | Source | Set at |
|---|---|---|
| Tray glyph | `TrayIcon.Icon` | runtime |
| Taskbar / alt-tab | exe PE resource | **compile time** (`<ApplicationIcon>`) |
| Explorer file thumbnail | exe PE resource | **compile time** |
| About dialog | `Icon.ExtractAssociatedIcon(Application.ExecutablePath)` | reads PE resource |
| Run dialog / recent files | exe PE resource | **compile time** |
| File association | exe PE resource | **compile time** |

A runtime draw fixes only the first row. Skipping the static `.ico` means the other five fall back to the default Windows icon, leaving the app visually unbranded in the places users find it.

## Static brand glyphs: `tools/IconGen`

`tools/IconGen` is a console app that:

1. Draws the glyph in GDI+ at seven sizes (16, 24, 32, 48, 64, 128, 256) using proportions scaled from a 256px reference grid.
2. Encodes each frame as PNG.
3. Packages them into a multi-resolution `.ico` via manual `ICONDIR` + `ICONDIRENTRY` headers.

### Adding a new glyph

1. Open `tools/IconGen/Program.cs`.
2. Add a `case "your-name"` to the `Main` switch that points at a new `RenderYourName(int size)` method.
3. Implement the renderer using the existing `RenderClipboard` as a template (transparent canvas, `FillRoundedRect` helper, scale all dimensions from the `s = size / 256f` factor).
4. Wire it from the consuming app's `.csproj`:

   ```xml
   <Target Name="GenerateAppIcon"
           BeforeTargets="PrepareForBuild"
           Inputs="..\..\..\tools\IconGen\Program.cs;..\..\..\tools\IconGen\IconGen.csproj"
           Outputs="app.ico">
     <Exec Command="dotnet run --project &quot;$(MSBuildThisFileDirectory)..\..\..\tools\IconGen\IconGen.csproj&quot; --configuration Release -- --glyph your-name &quot;$(MSBuildThisFileDirectory)app.ico&quot;" />
   </Target>
   ```

   Inputs/Outputs make the target incremental: regenerates only when the generator source is newer than the produced `.ico`. Subsequent builds skip with zero overhead.

### Design rules for build-time glyphs

- **Single semantic shape that reads at 16px.** The tray-rendering size is unforgiving. If the shape is unrecognisable at 16px, it is wrong.
- **High-contrast two-tone palette.** Three tones is the practical maximum; fine detail at 16px is anti-aliasing noise.
- **No text glyphs on the icon body.** Letters at 16px are noise. Use the silhouette to convey identity.
- **Skip detail features on sizes below 32px.** The clipboard renderer omits the three content lines below 32px; the dominant silhouette carries the load at small sizes.
- **Transparent background.** Tray rendering composites over taskbar themes; opaque backgrounds clash on themed taskbars.

## Stateful indicators: `IconBuilder.FromBitmap`

When the icon must change per-state (battery %, mute, count badge), render it at runtime and swap `TrayIcon.Icon`. Two non-negotiables:

1. **Use `IconBuilder.FromBitmap`, not `Icon.FromHandle(bmp.GetHicon())`.** The naive path leaks an `HICON` on every refresh. See [IconBuilder.cs](../../shared/WindowsTrayCore/IconBuilder.cs) for the underlying mechanism (clones the bits into a managed-owned `Icon` and destroys the source handle).
2. **Dispose the previous icon before assigning the new one.** `TrayIcon.Icon = next; previous?.Dispose();` (in that order; the swap is what releases Windows's reference to the old HICON).

Render to a `Bitmap` sized by `TrayIconMetrics.RecommendedRenderSize` for DPI-correct results. See `BatteryTray.IconRenderer` for the reference implementation.

## Anti-patterns

| Anti-pattern | Why it bites |
|---|---|
| Copying another app's `.ico` as a placeholder | Apps become visually indistinguishable in the tray. ClipTray shipped Phase 30 with BatteryTray's icon byte-for-byte and only caught it on smoke-test. If you need a placeholder, use one that visibly says PLACEHOLDER. |
| Runtime drawing for a static brand glyph | Pure cost. Five of six icon surfaces ignore runtime draws. You still need the static `.ico`. |
| Hand-authored `.ico` files alongside `tools/IconGen` glyphs | Diverges over time; impossible to do a coordinated palette change across apps. Pick one path per app and stick with it. |
| `Bitmap.GetHicon()` without `DestroyIcon` | Long-running tray apps leak HICONs across every refresh until they hit the per-process 10,000-handle GDI quota. Use `IconBuilder.FromBitmap`. |

## See also

- [`shared/WindowsTrayCore/IconBuilder.cs`](../../shared/WindowsTrayCore/IconBuilder.cs) — safe `Bitmap` to `Icon` conversion for runtime drawing.
- [`shared/WindowsTrayCore/TrayIconMetrics.cs`](../../shared/WindowsTrayCore/TrayIconMetrics.cs) — DPI-aware render-size derivation.
- [`tools/IconGen/Program.cs`](../../tools/IconGen/Program.cs) — the multi-resolution `.ico` builder.
