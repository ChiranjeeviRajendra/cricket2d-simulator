# Performance audit and regression checks

The runtime targets 120 rendered frames per second while focused and 30 while unfocused. Losing focus also pauses the simulation. Physics remains at 120 ticks per second with eight substeps; collision accuracy, controls, animation interpolation and field dimensions are unchanged.

## Findings and changes

The main measured bottleneck was rebuilding the field on every draw. Parking bays, car faces, oval wedges and barricades repeatedly allocated arrays, sorted geometry and submitted hundreds of small drawing commands. The original wide view averaged 92.10 FPS on the test PC and allocated about 1.38 MB per frame. No leftover Godot/game processes were present at the start of the audit. The user reported that the PC recovered after closing the game; excessive runtime work is consistent with this, but the measurements cannot attribute every desktop slowdown to a single cause.

Changes:

- `FieldDrawing` builds world geometry once. Two retained meshes preserve background/foreground ordering and update only when camera scale or position changes. The existing projection and artwork are retained.
- `FieldMesh` reuses CPU lists and GPU vertex, colour and index buffers. Camera movement updates buffer regions without creating replacement native meshes. Empty geometry and backend buffer strides are handled explicitly. Unused index slots are degenerate triangles; buffers grow only if their capacity is exceeded.
- `GroundView` retains draw commands while setup or a paused pose is unchanged, skips hidden views, and invalidates the cache for phase, pose and preview changes. Godot already caches custom drawing commands until a redraw is requested. See [custom drawing](https://docs.godotengine.org/en/stable/tutorials/2d/custom_drawing_in_2d.html), [ArrayMesh](https://docs.godotengine.org/en/stable/classes/class_arraymesh.html) and [RenderingServer](https://docs.godotengine.org/en/stable/classes/class_renderingserver.html).
- Animation stance bounds are computed when a rig is constructed, replacing four LINQ scans on every substep. Indexed surface traversal avoids enumerator allocation. Reusable ellipse points, cached input action names and change-only HUD formatting remove more recurring allocations.
- Delivery previews run on one cancellable worker. Slider edits replace the pending request; stale results cannot overwrite newer settings. No Godot objects are accessed by the worker. Starting an actual delivery still uses the authoritative solver and the existing validation.
- Slider badges and focus styles are reused. Capture images and owned meshes are disposed explicitly. Imported textures remain owned by Godot's shared resource cache.
- The foreground cap is explicit, VSync is enabled, and background rendering is capped at 30 FPS in both gameplay and the batting preview. A lower-refresh display can limit rendering below 120; see [Engine.max_fps](https://docs.godotengine.org/en/stable/classes/class_engine.html#class-engine-property-max-fps).
- C# optimization is enabled for local builds as well as release builds, with warnings treated as errors. Debug symbols remain available, though optimized locals can be harder to inspect.
- Smoke/capture automation is separated from the interactive controller. Unused input-device reporting, obsolete text-input theme styles and duplicate ellipse helpers were removed. The launcher reuses existing versioned Godot packages instead of recopying them on every run.

The dependency audit found only the required Godot SDK/source-generator/editor assemblies and the local physics project. There were no unused third-party runtime libraries or material assets to remove. Editable sky sources and their generator remain useful development assets and are excluded from runtime import through `.gdignore`.

## Measurements

Test machine: Ryzen 7 7800X3D, RTX 4080 SUPER, approximately 32 GB RAM, NVIDIA 610.88, Godot 4.7.2 .NET, GL Compatibility renderer, Windows, 3840 × 2160 display at 120 Hz. Tests use a real rendered fullscreen window; they are not headless FPS measurements. The 1440 × 900 logical canvas is a layout coordinate system, with `canvas_items` rendering at the physical resolution rather than upscaling a 1440 × 900 bitmap. Normal gameplay preserves its 16:10 proportions: a 4K display has a 3456 × 2160 game area with side margins. The benchmark now fills the entire 3840 × 2160 target for a stricter GPU workload, without changing normal gameplay's proportions.

The baseline used the original renderer. The optimized run also exercises continuous shoulder/wrist input, slider edits and delivery resets. Each scenario warms up for two seconds and records six seconds. Values below are from `artifacts/performance-before.json` and `artifacts/performance-after.json`:

| Scenario | Before FPS | Optimized FPS | Before draw calls | Optimized draw calls | Before allocated bytes/frame | Optimized allocated bytes/frame |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Ready | 120.02 | 120.00 | 660 | 123 | 979,786 | 79 |
| Live delivery | 119.50 | 120.00 | 664 | 127 | 984,744 | 318 |
| Wide field | 92.10 | 120.00 | 1,031 | 128 | 1,377,444 | 106 |
| Moving camera | 106.51 | 120.00 | 803 | 128 | 1,168,788 | 239 |
| Dragging settings | — | 120.00 | — | 156 | — | 2,232 |
| Repeated deliveries | — | 120.00 | — | 124 | — | 136 |
| Main menu | 120.00 | 120.00 | 16 | 16 | 227 | 68 |

The optimized wide-view 99th-percentile frame interval was 8.42 ms, compared with 14.87 ms before. All seven optimized scenarios recorded zero managed garbage collections during their measurement windows. A separate three-cycle run lasted approximately 168 seconds: scenario averages ranged from 119.66 to 120.04 FPS; memory settled around 267–273 MiB working set after initial warm-up. That is a short soak test, not proof about indefinite sessions. Native objects, private memory and managed allocations are recorded separately to make later regressions visible.

`artifacts/performance-final.json` repeats all seven scenarios twice after UI resource reuse and explicit capture-image disposal. Average rates ranged from 119.996 to 120.005 FPS, with all 99th-percentile frame intervals below 8.47 ms. Working set stayed within 268.7–270.1 MiB in the second cycle; Godot object count stayed at 1,802 during its gameplay/settings phases and reached 1,804 on returning to the menu. All measurement windows recorded zero managed collections. Static-view scenery rebuild counts were zero; intentional camera motion updated the retained buffers.

The full-frame 4K verification is recorded separately in `artifacts/performance-4k.json`, with readback confirming **3840 × 2160** pixels. All seven scenarios averaged **119.66–120.02 FPS**; their 99th-percentile frame intervals were **8.40–8.56 ms**. The wide view remained at 120.00 FPS, 128 draw calls and 106 allocated bytes/frame. There were no managed collections during any measurement window. These results are close to the cap, with occasional missed frames rather than a mathematically perfect lock.

`RenderTarget` is measured from a one-time framebuffer readback **after** all timing samples; `ViewportTexture.GetSize()` alone can include a stretch transform. Screenshots are saved separately from timed runs because framebuffer readback and PNG encoding stall rendering and distort the displayed FPS.

FPS is calculated from wall-clock frame intervals. Allocation counters include all managed threads, including the preview worker and small benchmark bookkeeping costs. `CpuCorePercent` uses 100% to mean one fully occupied logical CPU, not the whole 16-thread CPU. Godot process/physics monitor timings are retained for diagnosis but are not GPU timings or a direct measure of CPU busy time. Desktop scheduling, background programs and driver settings can still cause individual missed frames; the 120 FPS cap is not a guarantee for every frame or every PC.

## Running the checks

```powershell
# Normal play at fullscreen 4K; F11 can also toggle fullscreen during play.
.\tools\Run.ps1 -FourK

# Build, render seven scenarios at 4K, write artifacts/performance.json, then exit.
.\tools\Run.ps1 -PerformanceTest

# Engine-independent behavior and allocation regression checks.
dotnet run --project Tests/Cricket.Physics.Tests.csproj

# Native menu, slider, focus-cap and gameplay smoke checks.
.\tools\Run.ps1 -Headless -SmokeTest
.\tools\Run.ps1 -Headless -BattingPreview -SmokeTest
```

When invoking Godot directly, append `-- --performance-test`. Optional user arguments are `--performance-cycles=3`, `--performance-seconds=6`, `--performance-output=artifacts/custom.json` and `--performance-uncapped`. The last option disables the cap and VSync for that diagnostic process only; normal play retains the power limits. Keep builds and other game instances closed during timing measurements. The benchmark never writes gameplay configuration and exits automatically.

Verification completed: clean build with zero warnings, all 47 core checks, gameplay smoke including focus pause and 30/120 FPS transitions, batting-preview controls/reloads, unused-import analysis, and visual inspection of normal/zoomed field and settings captures. The new checks cover cancellation/latest-result publication, worker disposal, bounded ball trails and near-zero steady-state physics allocation. Existing determinism, momentum, collision, foot-contact, bone-length and 30/60/144 FPS interpolation checks remain in place.
