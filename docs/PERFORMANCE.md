# Performance audit and regression checks

The runtime targets at least 120 rendered frames per second, without an engine FPS cap while focused. Background rendering is limited to 30 FPS. Losing focus also pauses the simulation. Physics remains at 120 ticks per second with eight substeps; collision accuracy, controls, animation interpolation and field dimensions are unchanged.

Earlier audit sections below retain their original test conditions, including the former 120 FPS cap and fixed 16:10 gameplay canvas. The batsman rework records the rendering policy; the widescreen section records the current layout and its measurements.

## Widescreen layout — 2026-11-12

`project.godot` now uses `canvas_items` with `expand`, following Godot's [multiple-resolution guidance](https://docs.godotengine.org/en/stable/tutorials/rendering/multiple_resolutions.html). The 1440 × 900 design minimum expands to 1600 × 900 on a 16:9 screen. Both gameplay and benchmarks fill the native 3840 × 2160 target with uniform scaling; the benchmark's separate aspect override was removed.

`FieldCamera` and `FieldLayout` use the actual logical width and height. `GroundView` applies resize events, while `FieldDrawing`, its seating pass and `FieldMesh` update background coverage and culling bounds. The sky fills the screen with proportional cropping. The HUD hint stays anchored to the bottom and the batting lab background fills its viewport. Existing scenery buffers remain cached until framing or viewport dimensions change; no resize polling or new per-frame collections were added. The full-width field draws more pixels than the previous normal-play area, so this is a display fix, not a claimed performance improvement.

Validation: Debug and ExportRelease builds completed with zero warnings/errors; all 73 Release checks passed, including resized shots, both wickets, equal X/Y scales and scenery/physics alignment. Native gameplay smoke checks passed. Its scrolling assertion now permits tall viewports where all controls fit and still checks that the selected slider is visible. Rendered captures verified 1920 × 1080, 2560 × 1080, 1280 × 1024, 1440 × 900, native 4K fullscreen and F11 restoration. `artifacts/layout.log` records output size, logical canvas and scale; `artifacts/layout-*.png` contains the captures.

`artifacts/performance-widescreen.json` records ExportRelease assemblies in the local Godot 4.7.2 .NET runner on the reference Ryzen 7 7800X3D / RTX 4080 SUPER PC, NVIDIA 610.88, Compatibility rendering, verified 3840 × 2160 output, VSync enabled and engine cap zero. Each of eight scenarios warmed up for two seconds and sampled for three seconds. Average FPS ranged from 119.997 to 120.334; p99 frame intervals ranged from 8.403 to 8.433 ms. Live batting's 1% low was 109.91 FPS; other scenarios ranged from 117.41 to 118.68 FPS. Static views recorded zero scenery rebuilds. One collection in each managed generation occurred during settings dragging; other sampling windows recorded none. Working set ranged from 267.8 to 287.9 MiB. No runtime errors were logged. This short run does not establish sustained 4K/120 performance, a perfect frame lock, or a before/after speed improvement; packaged-export and prolonged-session validation remain outstanding.

## Findings and changes

The main measured bottleneck was rebuilding the field on every draw. Parking bays, car faces, oval wedges and barricades repeatedly allocated arrays, sorted geometry and submitted hundreds of small drawing commands. The original wide view averaged 92.10 FPS on the test PC and allocated about 1.38 MB per frame. No leftover Godot/game processes were present at the start of the audit. The user reported that the PC recovered after closing the game; excessive runtime work is consistent with this, but the measurements cannot attribute every desktop slowdown to a single cause.

Changes:

- `FieldDrawing` builds world geometry once. Two retained meshes preserve background/foreground ordering and update only when camera scale or position changes. The existing projection and artwork are retained.
- `FieldMesh` reuses CPU lists and GPU vertex, colour and index buffers. Camera movement updates buffer regions without creating replacement native meshes. Empty geometry and backend buffer strides are handled explicitly. Unused index slots are degenerate triangles; buffers grow only if their capacity is exceeded.
- `GroundView` retains draw commands while setup or a paused pose is unchanged, skips hidden views, and invalidates the cache for phase, pose and preview changes. Godot already caches custom drawing commands until a redraw is requested. See [custom drawing](https://docs.godotengine.org/en/stable/tutorials/2d/custom_drawing_in_2d.html), [ArrayMesh](https://docs.godotengine.org/en/stable/classes/class_arraymesh.html) and [RenderingServer](https://docs.godotengine.org/en/stable/classes/class_renderingserver.html).
- Animation stance bounds are computed when a rig is constructed, replacing four LINQ scans on every substep. Indexed surface traversal avoids enumerator allocation. Reusable ellipse points, cached input action names and change-only HUD formatting remove more recurring allocations.
- Delivery previews run on one cancellable worker. Slider edits replace the pending request; stale results cannot overwrite newer settings. No Godot objects are accessed by the worker. Practice starts the exact solver-produced plan without repeating the solve on the game thread; the direct settings-based simulation API remains available.
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

# Build, render eight scenarios at 4K, write artifacts/performance.json, then exit.
.\tools\Run.ps1 -PerformanceTest

# Engine-independent behavior and allocation regression checks.
dotnet run --project Tests/Cricket.Physics.Tests.csproj

# Native menu, slider, focus-cap and gameplay smoke checks.
.\tools\Run.ps1 -Headless -SmokeTest
.\tools\Run.ps1 -Headless -BattingPreview -SmokeTest
```

When invoking Godot directly, append `-- --performance-test`. Optional user arguments are `--performance-cycles=3`, `--performance-seconds=6`, `--performance-output=artifacts/custom.json` and `--performance-uncapped`. The last option disables the cap and VSync for that diagnostic process only; normal play retains the power limits. Keep builds and other game instances closed during timing measurements. The benchmark never writes gameplay configuration and exits automatically.

Verification completed: clean build with zero warnings, all 47 core checks, gameplay smoke including focus pause and 30/120 FPS transitions, batting-preview controls/reloads, unused-import analysis, and visual inspection of normal/zoomed field and settings captures. The new checks cover cancellation/latest-result publication, worker disposal, bounded ball trails and near-zero steady-state physics allocation. Existing determinism, momentum, collision, foot-contact, bone-length and 30/60/144 FPS interpolation checks remain in place.

## Stadium seating regression check — 2026-11-04

Parking was replaced by 28 concrete seating tiers, 16 access staircases and a deterministic spectator crowd. An initial many-polygon crowd exceeded the moving-camera budget. The final renderer uses an Inkscape-exported 1024 × 1280 atlas (about 147 KiB on disk), one quad per visible spectator and the existing two scenery meshes. Packed colour/UV attributes upload directly from reusable buffers. Terrace curves use 160 segments; the boundary/wall curves retain 320. There are no per-person nodes, timers or colliders. Fixed-camera views still perform no scenery rebuilds.

The final short local benchmark (`artifacts/performance-seating.json`) used the reference hardware and renderer above, native 3840 × 2160 fullscreen, VSync and the existing 120 FPS limit, two seconds of warm-up and three seconds sampled per scenario:

| Scenario | Average FPS | Frame interval p99 | Median draw calls |
| --- | ---: | ---: | ---: |
| Ready | 119.999 | 8.434 ms | 123 |
| Live delivery | 119.998 | 9.452 ms | 127 |
| Wide field | 119.998 | 8.403 ms | 128 |
| Moving camera | 119.998 | 8.391 ms | 128 |
| Dragging settings | 119.998 | 8.397 ms | 156 |
| Repeated deliveries | 119.994 | 8.404 ms | 124 |
| Main menu | 119.997 | 8.696 ms | 16 |

All seven sampling windows recorded zero managed garbage collections. Moving-camera allocation was 268 bytes/frame including benchmark bookkeeping; working set ranged from 255–283 MiB across scenarios. Camera-motion CPU usage was 63.5% of one logical core. These short averages do not promise a perfect frame lock or long-session stability. Captures run separately and their displayed FPS includes intentional capture waits/readback stalls.

Debug (optimized) and `ExportRelease` builds completed with zero warnings/errors; all 52 Release core checks and the native gameplay/menu smoke checks passed. Captures of normal, both side zooms and the full stadium were inspected. The timed run used the optimized local game build, not a packaged export; export templates/presets are not installed/configured here, so exported-build hardware validation remains outstanding.

## Randomized practice regression check — 2026-11-04

Practice generation reuses the single cancellable worker. Each pending ball has a stable seed; rapid changes cancel obsolete work rather than queueing jobs. Bowling consumes the prepared plan directly, removing a second solver run from the input callback. Guide and focus changes do not prepare another plan. The benchmark adds `random-practice`, cycling all five categories and release heights every 0.25 s; `settings-drag` now explicitly enables both manual sliders.

`artifacts/performance-practice.json` records the local **ExportRelease game/core assemblies** running under Godot .NET on the reference hardware above (RTX 4080 SUPER), native 3840 × 2160 fullscreen, Compatibility renderer, VSync and the existing 120 FPS cap. Each scenario has two seconds of warm-up and three seconds of sampling. The current user field configuration has a 15 m outer wall and reduced seating rows, so comparisons with the earlier stadium configuration do not isolate code changes.

| Scenario | Average FPS | Frame interval p99 | Median draw calls |
| --- | ---: | ---: | ---: |
| Ready | 119.998 | 8.397 ms | 123 |
| Live delivery | 119.999 | 8.436 ms | 127 |
| Wide field | 119.998 | 8.408 ms | 128 |
| Moving camera | 119.998 | 8.402 ms | 128 |
| Dragging manual settings | 119.999 | 8.399 ms | 178 |
| Repeated deliveries | 119.998 | 8.400 ms | 124 |
| Main menu | 119.998 | 8.467 ms | 16 |
| Rapid random-practice changes | 119.999 | 8.480 ms | 163 |

All eight sampling windows recorded zero managed garbage collections. Working set stayed approximately 250–281 MiB. Random-practice changes allocated 339 bytes per rendered frame across the managed process, including background preparation and benchmark bookkeeping. Manual-slider dragging allocated 4,494 bytes/frame while changing values every frame. These measurements show no sustained frame-rate regression in this short run; p99 intervals exceed the ideal 8.333 ms slightly, so they do not establish a perfect lock or long-session stability.

Both build configurations completed without warnings/errors, all 56 Release core checks passed, and native UI smoke checks covered category selection, dropdown cancellation, checkbox activation, hidden-slider focus, scrolling, errors/readiness, Bowl, menu retention and guide/trail independence. The dropdown additionally checks D-pad/left-stick navigation and controller A/B activation/cancellation. Existing physics, collision, audio-selection, determinism and 30/60/144 FPS interpolation checks remain in that suite. Captures of the bowler dropdown, default/manual/scrolled settings and guides on/off were inspected. No physical controller was connected; controller inputs were synthesized. Packaged-export validation remains outstanding because export templates/presets are absent; the timed run used release assemblies in the local runner.

## Initial batsman rework — 2026-11-05 (superseded character)

The character now uses an 18-bone skin (11,143 source vertices, 10,736 polygons, nine shared materials) in one adaptive 256/512/1024-pixel transparent viewport. Unchanged poses reuse the previous texture. No full-body rigid bodies or extra collision solvers were introduced. The focused engine cap is now zero; VSync is retained by default. Background focus handling remains paused/30 FPS.

Measurements used the reference Ryzen 7 7800X3D / RTX 4080 SUPER PC, NVIDIA 610.88, Godot 4.7.2 .NET Compatibility renderer and a confirmed 3840 × 2160 render target. Both runs used **ExportRelease assemblies in the local Godot runner**, not a packaged export. They include continuous stroke, bottom-hand input and alternating front/back trigger pressure during the animated scenarios.

- `artifacts/performance-batsman-initial-vsync.json`: two cycles, two-second warm-up plus six-second sample per scenario, approximately 128 seconds overall.
- `artifacts/performance-batsman-initial-uncapped.json`: separate diagnostic with VSync disabled, two-second warm-up plus three-second sample per scenario.
- `artifacts/performance-batsman-before.json`: earlier renderer baseline, three-second samples, engine cap 120 and VSync enabled. This is not an uncapped baseline.

| Scenario | VSync cycle 2 FPS | VSync p99 ms | Uncapped FPS | Uncapped 1% low FPS | Uncapped p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ready | 117.67 | 14.496 | 2076.00 | 912.10 | 0.908 |
| Live batting | 120.00 | 8.606 | 443.69 | 298.61 | 2.802 |
| Wide view | 120.00 | 8.406 | 844.72 | 637.58 | 1.478 |
| Moving camera | 120.00 | 8.414 | 379.21 | 242.97 | 3.553 |
| Settings drag | 120.00 | 8.442 | 1211.32 | 590.29 | 1.193 |
| Delivery resets | 120.00 | 8.438 | 2175.52 | 1162.37 | 0.682 |
| Menu | 120.17 | 8.479 | 3123.96 | 1371.80 | 0.632 |
| Random practice | 120.00 | 8.424 | 1695.33 | 599.66 | 0.895 |

The second VSync ready sample had an isolated timing disturbance: 1% low was 39.85 FPS despite a median of 8.335 ms. Its cause was not established; the results do **not** demonstrate a perfect 120 FPS lock. Uncapped results show substantial throughput headroom, but do not prove the cause of a VSync hitch. The 1% low metric is the reciprocal of the average interval of the slowest 1% of frames, not simply 1000 divided by p99.

Ready draw calls fell from 123 to 64–65; animated views recorded 77–78 versus the baseline's 127–128. No measured window triggered a managed garbage collection. VSync working set ranged from 254 to 289 MiB (cycle 2: 279–289 MiB); native resource count remained 25. Object count and private memory are retained in the JSON for longer-term comparisons. These short runs do not establish indefinite memory stability.

Steady wide-view allocation was 79–103 bytes/frame including benchmark bookkeeping; moving-camera allocation was 227–238 bytes/frame. The first VSync live sample recorded 9,319 bytes/frame versus 396 in cycle 2, so it should not be described as allocation-free gameplay. The changed swing also changes contacts and camera travel: first live scenery rebuilds were 720 versus 50 in the older baseline. The runs therefore do not isolate the renderer's CPU cost. Uncapped live physics p95 was 1.414 ms. GPU execution time was not measured separately.

Reproduce with:

```powershell
.\tools\Benchmark-Batsman.ps1 -Seconds 6 -Cycles 2
.\tools\Benchmark-Batsman.ps1 -Seconds 3 -Cycles 1 -Uncapped
```

The script builds Debug and ExportRelease, temporarily supplies the release assemblies to the local runner, records their hash, and restores the original Debug bytes in a `finally` block. Do not run another game/build concurrently. The uncapped option is diagnostic only and never changes project presentation settings.

Validation: 51/51 core checks passed in Release, both game/lab native smoke suites passed, 3,321 rendered rig configurations plus four motion clips passed the 3D limb audit, and lab/field captures were visually inspected. Build/import completed without new runtime errors. No physical gamepad was connected; bindings were tested with synthetic engine input. Export templates/presets remain unavailable, so **packaged-release validation remains outstanding**. The lab retains slow-motion/pause/single-tick rehearsal; a recorded ball-replay feature was not added by this rework.

## Revised human mesh and 3D motion — 2026-11-05

Following visual review, the segmented character was replaced by a continuous MakeHuman-derived skin with retargeted male proportions, original kit/equipment and shared core/render joints. The source now has 13,640 vertices, 13,556 polygons, 18 bones and ten shared materials. The renderer loads rest-length metadata once and no longer runs a second arm IK solve. The earlier measurements above describe the rejected visual version and are retained as historical context.

The same hardware, resolution, release-assembly runner and commands were used. `artifacts/performance-batsman-pre-shots-vsync.json` contains two six-second samples per scenario after two-second warm-ups; `artifacts/performance-batsman-pre-shots-uncapped.json` contains separate three-second samples with VSync disabled. Both confirmed a native 3840 × 2160 target and engine cap zero.

| Scenario | VSync cycle 2 FPS | VSync p99 ms | Uncapped FPS | Uncapped 1% low FPS | Uncapped p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ready | 120.00 | 8.432 | 2129.05 | 1318.32 | 0.685 |
| Live batting | 120.00 | 8.435 | 456.06 | 322.83 | 2.730 |
| Wide view | 120.00 | 8.437 | 837.44 | 626.77 | 1.489 |
| Moving camera | 120.00 | 8.430 | 344.60 | 280.32 | 3.451 |
| Settings drag | 120.16 | 8.438 | 1210.93 | 585.12 | 1.326 |
| Delivery resets | 120.00 | 8.438 | 1432.68 | 860.83 | 0.926 |
| Menu | 120.00 | 8.431 | 3281.37 | 1726.74 | 0.480 |
| Random practice | 120.17 | 8.443 | 1731.30 | 962.49 | 0.859 |

VSync camera-motion 1% lows were 86.49 and 99.54 FPS across the two cycles; the average is not a perfect 120 FPS lock. Compared with the earlier short uncapped run, camera throughput decreased from 379.21 to 344.60 FPS while live batting rose from 443.69 to 456.06 FPS. Animation, contacts and camera paths also changed, so this is not an isolated renderer-cost comparison and does not establish a general performance improvement.

All measured windows recorded zero managed garbage collections. Animated draw-call medians were 78–79. VSync working set ranged from 263.8 to 284.3 MiB; the uncapped run reached 317.7 MiB while rapidly changing settings. VSync live batting allocated 272–295 bytes/frame and camera motion 226–239 bytes/frame, including benchmark bookkeeping. Native resource count stayed at 26. These short runs do not prove prolonged memory stability or separately measure GPU execution time.

Validation: Debug and ExportRelease builds completed with zero warnings/errors; 51/51 Release core checks passed, including new 3D limb, elbow-continuity and rear-leg support assertions. Native gameplay and preview controls/playback/reload checks passed. The rendered audit covered 3,321 poses plus four clips, and guard/backlift/contact/follow-through/front-foot/back-foot captures were inspected. A final wrist material assignment correction did not change topology, bones or material count. No physical controller was connected. Packaged-export validation remains outstanding; measurements use ExportRelease assemblies in the local runner.

The motion is an authored approximation with IK, not captured athlete motion or a complete biomechanical/self-collision model. Passing numerical checks does not certify reference-level visual fidelity.

## Deliberate rotation and four shot styles — 2026-11-05

The input gate now requires an outer-stick anchor and a 24° counterclockwise arc. Four authored strokes replace right-stick steering. Arm skin roll follows the elbow bend plane, and remaining wrist skin follows the forearm. Gesture/pose evaluation uses fixed-size state, value types and bounded reach projections; it adds no per-frame collections or resource loads. The pause hint adds roughly one HUD draw call. The mesh remains 13,640 vertices, 13,556 polygons, 18 bones and ten materials.

The benchmark input was updated to perform valid arcs, recover through centre, cycle all five natural/modified strokes and alternate trigger pressure. This changes animation/contact trajectories, so comparison with the previous section is a regression check rather than an isolated CPU-cost experiment.

Same Ryzen 7 7800X3D / RTX 4080 SUPER, NVIDIA 610.88, Godot 4.7.2 .NET Compatibility renderer, confirmed 3840 × 2160 target, engine cap zero and ExportRelease assemblies in the local runner:

- `artifacts/performance-batsman-pre-clock-vsync.json`: two cycles, two-second warm-up and six-second sample per scenario. Taken before the temporary navigation-path disposal correction described below.
- `artifacts/performance-batsman-pre-clock-uncapped.json`: final three-second samples after two-second warm-ups, VSync disabled only for this diagnostic; clean process exit.

| Scenario | VSync cycle 2 FPS | VSync p99 ms | Uncapped FPS | Uncapped 1% low FPS | Uncapped p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ready | 120.00 | 8.421 | 2116.53 | 1290.46 | 0.693 |
| Live batting | 120.00 | 8.436 | 449.98 | 326.05 | 2.771 |
| Wide view | 120.00 | 8.422 | 833.83 | 513.36 | 1.782 |
| Moving camera | 120.16 | 8.441 | 341.57 | 248.01 | 3.723 |
| Settings drag | 120.00 | 8.435 | 1223.71 | 708.71 | 1.264 |
| Delivery resets | 120.00 | 8.416 | 1410.19 | 794.38 | 0.984 |
| Menu | 120.00 | 8.417 | 3273.65 | 1799.66 | 0.475 |
| Random practice | 120.17 | 8.441 | 1762.39 | 939.21 | 0.859 |

The final short uncapped run is close to the preceding motion version: live batting 449.98 versus 456.06 FPS, moving camera 341.57 versus 344.60 FPS. These samples do not establish a statistically significant improvement or regression. VSync live-batting 1% low was 116.37 FPS in cycle 2; camera motion was 99.84/108.21 FPS across cycles. Averages around 120 FPS do not establish a perfect frame lock.

Every measured window recorded zero managed garbage collections. Animated draw-call medians were 79–80. VSync working set ranged approximately 262–299 MiB; the final uncapped run peaked at 307.4 MiB. Native resource count remained 26. Final uncapped live/camera allocation was 255/248 bytes per frame, including benchmark bookkeeping; live physics p95 was 1.416 ms. GPU execution time was not measured separately. These runs do not establish prolonged memory stability.

The first uncapped attempt wrote all samples and then reported a native `NodePath.Finalize` access violation at shutdown, despite a zero runner exit code. Its report is preserved as `performance-batsman-shots-exit-error.json`. The delivery panel was leaving temporary `GetPathTo` results for finalization. They are now disposed immediately after assignment to native focus properties. Godot's [NodePath implementation](https://github.com/godotengine/godot/blob/master/modules/mono/glue/GodotSharp/GodotSharp/Core/NodePath.cs) confirms that explicit disposal suppresses finalization. The repeat benchmark and subsequent native smoke/capture runs exited cleanly; this supports the correction but is not proof against every engine shutdown race. The benchmark script now captures stderr and rejects fatal/runtime-error text even if the runner returns zero.

Validation: Debug and ExportRelease builds passed without warnings/errors; 52/52 Release core checks passed. These cover radial pushes, centre crossings, jitter, shot selection, disabled right stick, actual accelerated elbow continuity, limb lengths, bat clearance, weight transfer and collisions. Native gameplay and preview smoke checks passed, including Start pause, focus recovery, View settings and A/B/X/Y routing. The final rendered audit covered 1,845 poses plus seven clips; all shot finishes and the final wrist skin were inspected. Blender source contains all seven current baked review actions. No physical controller was connected. Packaged-export and long-duration hardware validation remain outstanding; the local runner used release assemblies.

## Direct stick-clock control — 2026-11-05

A is now the default and the separate natural mode is removed. Active stroke progress follows absolute stick angle without a speed cap, acceleration governor, response easing or one-tick character-render interpolation. X/B stop at 2/1:30; A/Y stop at 12. Pose reconstruction keeps the grip on the arm ray while limiting extension to both hands' reach and ground clearance. Bat mesh orientation now includes the actual face normal. Mesh/bone/material counts are unchanged.

The rig consumes input once per 120 Hz tick. Eight ball-contact substeps reconstruct intervening poses, so a whole controller sample is not incorrectly assigned to the first 1/960-second interval. The game renders the current stored character pose; it no longer reconstructs another interpolated character on every render frame. These changes add no per-frame collection or resource loading. Rendering the latest fixed-step pose prioritizes input latency over interpolation between character ticks; it does not measure or eliminate controller/OS/display latency.

Measurements used the same Ryzen 7 7800X3D / RTX 4080 SUPER, NVIDIA 610.88, Godot 4.7.2 .NET Compatibility renderer, confirmed 3840 × 2160 target, engine cap zero and ExportRelease assemblies in the local runner. Both processes exited cleanly.

- `artifacts/performance-batsman-vsync.json`: two cycles; two-second warm-up plus six-second sample per scenario. This preceded the final stationary-push gate correction and clock-label adjustment; neither changes the valid stroke pose curves.
- `artifacts/performance-batsman-pre-830-uncapped.json`: final code at that revision; two-second warm-up plus three-second sample per scenario with VSync disabled only for this diagnostic.

| Scenario | VSync cycle 2 FPS | VSync p99 ms | Uncapped FPS | Uncapped 1% low FPS | Uncapped p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: |
| Ready | 120.00 | 8.436 | 2154.24 | 1401.77 | 0.660 |
| Live batting | 120.00 | 8.506 | 484.01 | 307.98 | 2.728 |
| Wide view | 120.00 | 8.439 | 962.26 | 641.53 | 1.431 |
| Moving camera | 120.00 | 8.457 | 361.60 | 280.74 | 3.461 |
| Settings drag | 120.00 | 8.447 | 1230.06 | 784.26 | 1.150 |
| Delivery resets | 120.17 | 8.424 | 1418.19 | 866.74 | 0.919 |
| Menu | 120.00 | 8.424 | 3367.20 | 1915.17 | 0.487 |
| Random practice | 120.00 | 8.457 | 1707.57 | 988.67 | 0.854 |

VSync live-batting 1% lows were 117.77/102.72 FPS across cycles; moving-camera lows were 108.18/110.19 FPS. The average is not a perfect 120 FPS lock. Uncapped live/camera averages were 484.01/361.60 versus the preceding version's 449.98/341.57 FPS, but the gesture, contact paths and time spent reusing a pose changed. These short runs do not isolate a rendering improvement or establish end-to-end input latency.

Every live/wide/camera window and every uncapped window recorded zero managed collections. The first VSync settings-drag window recorded one collection in each generation, so the complete run is not collection-free. Final uncapped live/camera allocation was 248/257 bytes per frame, including diagnostic bookkeeping; settings dragging was 3,768 bytes/frame. VSync working set ranged 262.1–283.5 MiB; uncapped peaked at 305.7 MiB. Native resource count remained 26. Live uncapped physics p95 was 1.708 ms. Animated draw-call medians were 85–86 with VSync and 75–76 uncapped, where more render frames reuse the latest 120 Hz character texture. GPU time and prolonged memory stability were not separately established.

Validation: Debug and ExportRelease builds completed with zero warnings/errors; 52/52 Release checks passed, including exact per-tick angle and speed assertions, immediate hold/reverse, all mode stops, A/default equivalence, stationary pushes near 9, 3D limb/clearance sweeps and collision/audio regression checks. Native game and preview smoke tests passed. The rendered audit covered 1,476 poses plus seven clips; A/X/B/Y starts and finishes were visually inspected. Blender review actions were refreshed from those poses. No physical controller was connected and no packaged export was available; controller feel, packaged release and prolonged-session validation remain outstanding.

## 8:30 start and ignored-rotation fixes — 2026-11-05

All four modes now share the 255° (8:30) start constant. Regression tests reproduced two ignored-input cases before correction: entering the start zone after an off-angle push, and starting a second full rim rotation without centring. The gate no longer permanently rejects an earlier push or accumulates extra revolutions after leaving the legal arc. Moving back toward the start refreshes the activation anchor. A fresh deliberate arc can also interrupt recovery; its velocity/sweep begins at the new stick-sample pose rather than the unrelated recovery position. Idle practice accepts swings while the delivery panel is closed, without advancing a ball or delivery statistics.

The changes add bounded scalar gesture state and one stored value-type frame; no new gameplay collections, resources or threads. A new stroke reconstructs its initial sweep pose once. Idle rehearsal now evaluates the rig at the normal 120 Hz rate, while unchanged rendered poses still reuse the character texture. The preview adds stick/arm clock telemetry at its existing 10 Hz refresh rate and marker rays; those are absent from normal gameplay. Mesh, bone and material counts are unchanged.

`artifacts/performance-batsman-pre-live-modes-uncapped.json` preserves the final ExportRelease-assembly run at this revision: same reference hardware, NVIDIA 610.88, Godot 4.7.2 Compatibility renderer, confirmed native 3840 × 2160, no engine cap, two-second warm-up and three-second sample per scenario. VSync was disabled only for this diagnostic. The process exited cleanly.

| Scenario | Average FPS | 1% low FPS | Frame p99 ms | Median draw calls |
| --- | ---: | ---: | ---: | ---: |
| Ready / idle rehearsal | 2047.21 | 1250.71 | 0.722 | 71 |
| Live batting | 471.84 | 286.47 | 2.827 | 75 |
| Wide view | 955.07 | 639.09 | 1.445 | 76 |
| Moving camera | 351.64 | 256.53 | 3.725 | 76 |

Live/camera averages were 471.84/351.64 versus 484.01/361.60 FPS in the preceding short run, preserved as `performance-batsman-pre-830-uncapped.json`. Gesture paths and machine timing differ; this does not establish an isolated performance improvement. All measured windows recorded zero managed collections. Live/camera managed allocation was 249/248 bytes per frame including diagnostics; live physics p95 was 1.666 ms. Working set ranged 270.1–305.2 MiB and native resource count remained 26. No new VSync or packaged-export claim is made from this uncapped run.

Validation: 53/53 Release core checks passed, with regression coverage for rejected starts, entry from both sides of the start zone, twelve interrupted recoveries, consecutive full rotations, exact arm clocks, mode stops and correct re-arm velocity. Native simulated-controller checks repeated the start/recovery/full-circle cases and passed alongside menu, pause and delivery checks. Both build configurations passed without warnings/errors. The 1,476-pose/seven-clip rendered audit also compares the resolved bat bone against the physical bat. Captures were inspected and Blender actions refreshed to start at 8:30. Physical-controller feel, end-to-end hardware input latency, long-duration stability and a packaged export remain unverified.

## Live mode selection and Y's final blade turn — 2026-11-05

Mode selection now runs every fixed tick, including ready, active, held-finish and recovery states. Y matches A through arm clock 2, then maps arms 2 / 1 / 12 to blade 2 / 2:30 / 3. These are bounded scalar changes with no new gameplay collections or resources. On a mode change, the collision bat interpolates its previous and current grip/angle across the full tick; unchanged modes retain the existing constrained-pose sampling. The mesh remains 13,640 vertices, 13,556 polygons, 18 bones and 10 materials.

`artifacts/performance-batsman-uncapped.json` is the new ExportRelease-assembly run on the same hardware/settings and 4K render target, with two-second warm-up and three-second samples. VSync was disabled only for this diagnostic; the engine cap remained zero. The local runner exited cleanly.

| Scenario | Average FPS | 1% low FPS | Frame p99 ms | Median draw calls |
| --- | ---: | ---: | ---: | ---: |
| Ready | 2090.18 | 1391.95 | 0.662 | 71 |
| Live batting | 480.00 | 293.28 | 2.644 | 75 |
| Wide view | 948.94 | 656.04 | 1.419 | 76 |
| Moving camera | 348.55 | 222.17 | 4.350 | 76 |

The prior short run averaged 471.84 FPS in live batting and 351.64 FPS during camera motion. These samples do not establish an isolated improvement; camera p99 increased from 3.725 to 4.350 ms. Every measured window recorded zero managed collections. Live/camera allocations were 250 bytes per rendered frame including diagnostics, and live physics p95 was 1.788 ms. Native resource count stayed at 26; working set ranged 273.0–309.0 MiB. No fresh VSync, packaged-export, end-to-end latency or prolonged-memory claim follows from this run.

Validation: 55/55 Release core checks passed, including regressions that failed before the fixes, mode changes throughout the entire arc and recovery, unclipped stick position after leaving X/B, Y blade angles, and mode-change collision velocity over all substeps. Debug and ExportRelease builds had zero warnings/errors. Native Godot controller-event checks passed for mode selection before, during and after a stroke and Y's 2-to-3 blade turn. The 1,476-pose/seven-clip rendered anatomy audit passed; Y captures near arm clocks 2, 1 and 12 were inspected. Blender review actions and the imported character asset were refreshed. No physical controller was connected.

## Controller pause and delivery flow — 2026-11-09

Start now opens a reusable pause menu, D-pad Up requests the next ball through the existing asynchronous preparation worker, and leaving practice resets settings and guides. Menu navigation uses input events; removing stick navigation also removes the main menu and settings panel's per-frame repeat callbacks. The pause menu is constructed once with the HUD. No rendering cap or physics frequency changed.

The current `artifacts/performance-batsman-uncapped.json` records ExportRelease assemblies in the local Godot 4.7.2 runner on Ryzen 7 7800X3D / RTX 4080 SUPER, GL Compatibility, with a verified 3840 × 2160 render target, engine FPS cap zero and VSync disabled only for this diagnostic. Each of eight scenarios used two seconds of warm-up and three seconds of sampling, one cycle.

| Scenario | Average FPS | 1% low FPS | Frame p99 ms |
| --- | ---: | ---: | ---: |
| Ready | 2007.98 | 874.06 | 0.775 |
| Live batting | 475.94 | 289.09 | 2.891 |
| Wide view | 1113.69 | 558.95 | 1.465 |
| Moving camera | 349.13 | 257.46 | 3.577 |
| Settings drag | 1170.04 | 665.28 | 1.264 |
| Delivery resets | 2044.94 | 1104.42 | 0.740 |
| Main menu | 3227.77 | 1582.30 | 0.549 |
| Random practice | 1679.52 | 944.92 | 0.903 |

Every sampling window recorded zero managed collections. Live batting allocated 258 bytes per rendered frame including diagnostics; physics p95 was 1.840 ms. Native resource count remained 27 across all scenarios, and working set ranged 265.4–302.1 MiB. Live/camera averages are close to the prior short run; these samples do not establish an isolated performance improvement or sustained 4K/120 performance. This was not a packaged export or prolonged memory test.

Validation: Debug and ExportRelease builds completed with zero warnings/errors; 56/56 Release core checks passed. Godot input smoke checks covered first/next delivery, repeated requests, cancellation on pause, invalid preparation, frozen ball/statistics, pause focus recovery, settings persistence until leaving practice, default restoration and blocked stick navigation in menus/sliders/native dropdowns. Rendered pause/menu settings captures were inspected. Controller events were injected; no physical controller was connected.

## Ball bounce and outfield rolling — 2026-11-09

Ground contact now selects prepared-strip or outfield coefficients using the same pitch edges as the renderer. Outfield impacts include an effective deformation loss; rolling resistance is stronger on grass. The lookup returns a value type and adds no per-contact allocation, scene query or resource load. Fixed physics remains 120 Hz with eight contact substeps, and normal presentation settings are unchanged. [PHYSICS.md](PHYSICS.md) records the model, reference results and limitations.

Before/after reports are preserved as `artifacts/performance-ball-physics-before.json` and `artifacts/performance-ball-physics-after.json`, with corresponding logs. Both used ExportRelease assemblies in the local Godot 4.7.2 .NET runner, Ryzen 7 7800X3D / RTX 4080 SUPER, NVIDIA 610.88, GL Compatibility, verified 3840 × 2160 target, engine cap zero and VSync disabled only for this diagnostic. Each scenario used two seconds of warm-up and three seconds of sampling, one cycle. Both runs completed without logged runtime errors.

| Scenario | Before FPS | After FPS | Before 1% low | After 1% low | Before p99 ms | After p99 ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Ready | 2103.30 | 2176.02 | 1333.96 | 1426.11 | 0.690 | 0.637 |
| Live batting | 490.39 | 484.46 | 297.67 | 289.44 | 2.447 | 2.620 |
| Wide view | 945.93 | 969.59 | 654.09 | 686.14 | 1.456 | 1.372 |
| Moving camera | 367.87 | 366.11 | 296.21 | 299.40 | 3.228 | 3.202 |
| Settings drag | 1231.52 | 1257.80 | 800.92 | 838.84 | 1.129 | 1.096 |
| Delivery resets | 1418.82 | 1431.42 | 843.83 | 873.88 | 0.940 | 0.908 |
| Main menu | 3442.70 | 3562.19 | 2293.19 | 2251.98 | 0.402 | 0.415 |
| Random practice | 1748.45 | 1789.31 | 1044.40 | 1084.10 | 0.782 | 0.762 |

Live batting averaged 1.2% lower FPS; camera motion averaged 0.5% lower. Live physics p95 was 1.756 ms versus 1.761 ms before. These short runs do not establish a statistically significant improvement or regression, and altered trajectories affect the animated workload. Every sampling window recorded zero managed collections. After-change live/camera allocations were 248/231 bytes per rendered frame including diagnostics; native resource count stayed at 27 and working set ranged 268.7–306.3 MiB. GPU execution time, prolonged stability and packaged-export performance were not separately measured. These results do not establish sustained 4K/120 performance.

Validation: 62/62 Release checks passed; Debug and ExportRelease builds completed without warnings/errors, and all Godot gameplay smoke groups passed. The three initial reference checks failed on the old model, reproducing excessive drop rebound, outfield rolling distance and angled-impact speed retention. Added coverage includes both surface transitions, 3,000 spinning impacts followed by supported contact, passive energy loss, grazing contacts, configurable coefficients and stable rest in the live simulation. Existing swept collisions, render-rate independence, allocation and animation-preview playback checks also passed. Controller events were injected; no physical controller was connected.
