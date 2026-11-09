# Batsman controls, anatomy and Blender workflow

The batsman is a skinned 3D character composited into the existing 2D pitch. Ball flight and bat contacts still run in the engine-independent fixed-step physics core. A single physical bat transform drives both collision detection and the rendered bat.

## Controls

Clock positions describe the **top-hand arm direction from its shoulder**, viewed on screen. The bowler is to the right. Every mode starts at 8:30 and travels counterclockwise through 6 and 3.

| Mode | Input | Arm stop | Blade behaviour |
| --- | --- | --- | --- |
| A (default) | No button, or A / K | 12 | Blade remains aligned with the arm ray throughout the 255° arc |
| X | Hold X / J | 2 | Flat face starts toward the ground, turns toward the bowler, and finishes as a vertical wall |
| B | Hold B / L | 1:30 | Face rotates forward; blade finishes pointing at 4 o'clock |
| Y | Hold Y / I | 12 | Same as A through 2; then the blade turns from 2 toward 3 as the arms continue to 12, ending horizontal with its face toward the sky |

R2/L2 transfer weight forward/back progressively (E/Q on keyboard). D-pad Up bowls the first or next ball. Menu/Start opens the pause menu with Resume, Settings and Main Menu; Settings edits practice options while the current ball stays frozen. View/Select still opens explicit delivery setup. Space or Escape opens pause during play, Enter opens setup, and keyboard Up requests the next ball. A/B retain confirm/back roles inside menus. Only the D-pad navigates controller UI; sticks control gameplay. Leaving practice through Main Menu or exiting the game discards practice overrides.

**To swing:** move the left stick outward near 8:30, then rotate **8:30 → 6 → 3 → 12**. Mode buttons work before, during and after the swing, including while holding the stick still or recovering. The currently held button applies on the next physics tick without re-arming; releasing all modifiers returns to A. Switching away from X/B restores the current stick angle beyond their stops. Simultaneous modifiers use priority X, B, Y, A. No button is exactly A; there is no fifth “natural” mode. Right-stick batting remains disabled.

Y keeps the blade aligned with A until the arms reach 2. Over the final arm arc the mapping is linear: **arms 2 / 1 / 12 → blade 2 / 2:30 / 3**. Reversing the stick retraces this curve; there is no timed blade animation.

The stick must reach 60% radius within ±15° of 8:30 and pass a 6° initial rotation gate. Direct pushes, small radial movements, centre crossings and discontinuous direction jumps cannot launch a stroke. The initial gate rejects jitter; **after activation, the actual angle is used without subtracting the gate**, so the stick and arm clocks agree. An off-angle push remains inactive, but you can rotate back into the start area without centring first. Moving back toward 8:30 updates the activation anchor so an older angle cannot delay a later swing.

Active progress has no smoothing, acceleration ramp, speed cap or delayed animation target. Holding the stick stops immediately; reversing it reverses the stroke on the current fixed tick. X/B clamp at their stops without rescaling the preceding arc. Holding an endpoint cannot start another stroke. Continuing around the rim through the upper-left sector and back into 8:30 can start a new deliberate stroke; a centre release is not required between full rotations. Returning below 45% radius ends control and returns toward 8:30 at 240°/s. A fresh deliberate arc interrupts that return immediately; it no longer waits for recovery to finish. Release recovery is separate from active swing tracking. The upper-left sector outside the legal arc releases the old gesture instead of accumulating an extra revolution. When a new arc interrupts it, collision velocity is measured from the new arc's adjacent stick samples; the reset from the old recovery pose cannot create a false fast bat sweep.

The current authoritative pose is rendered directly. This removes the former extra interpolation tick for the batsman. Input still passes through controller/OS polling, the fixed 120 Hz physics tick (up to 8.33 ms to the next tick), rendering and display presentation; this is not a claim of zero end-to-end latency. The ball retains its normal visual interpolation. In the main practice view, batting input works before bowling too; opening delivery settings suspends it so UI navigation cannot become a stroke.

Trigger pressure remains dead-zone corrected and combined as **R2 − L2**. Equal pressure cancels. R2 advances the front foot and lowers the rear knee; L2 steps back with the rear foot before gathering the front foot and moving pelvis/head above rear-leg support. Start pauses simulation and audio; focus restoration does not clear a manual pause.

## Motion and constraints

- `BattingStroke` maps the absolute arm clock to each mode's blade angle and coordinated torso rotation. No time-based clip controls swing progress.
- The grip stays on the requested arm ray. Analytic reach limits keep both hands connected without stretching bones. Elbows may bend to accommodate the second hand or ground clearance, especially during deep front-foot weight transfer; perfectly locked elbows throughout every stance would force the bat through the pitch.
- Separate pelvis, spine, chest, neck and head joints distribute body motion. `AthletePose` carries the same 3D grips/elbows used by rendering and physics.
- Elbow guides and cached rest-roll offsets control arm skin orientation. Wrist skin follows the forearm; closed gloves follow the handle.
- Feet lift before translating and plant at fixed toe anchors. Only one foot can be airborne. Hip reach stays within both planted legs' reach.
- The physical blade angle and front-face normal also orient the mesh. Its edge is visible in side view when the flat face looks at the ground or sky.
- Input advances once per 120 Hz tick. Collision substeps reconstruct intervening poses from that tick's endpoints, avoiding the former risk of applying a whole stick sample in only 1/960 s. Contact speed uses the actual grip/angle changes over elapsed simulation time.
- A live mode change preserves the previous bat pose and interpolates its grip/angle across the full tick for collisions. The new mode is rendered immediately at the current stick position. This avoids multiplying a mode change's collision velocity by the substep count; mode changes remain direct controls rather than a physically simulated wrist transition.
- Preview playback remains fixed-step and supports pause, slow motion, reverse demonstrations and single steps. It renders the current pose, without a separate trailing character pose.

These are authored animation approximations with fixed-length IK. The character does not have a full-body self-collision or muscle-torque model. Direct input can request changes faster than an athlete would perform; large discontinuities are rejected, but accepted input is intentionally not acceleration-limited. Numerical limb checks do not establish photo-reference visual fidelity.

## Editable settings

Edit `Config/batsman.json`, then restart or choose **Reload JSON** in the batting lab. Invalid or obsolete properties are rejected and failed reloads retain the working rig.

| Object | Settings and default |
| --- | --- |
| `Input` | Release radius 0.45; engage radius 0.60; initial rotation 6°; start window ±15° around 8:30; maximum per-sample jump 65°; trigger deadzone 0.04 |
| `Swing` | `RecoveryDegreesPerSecond`: 240, used only after stick release/cancellation |
| `Body` | Grip spacing 0.105 m; transfer settling 0.28 s; step 0.24 s; front/rear toe strides 0.42/0.14 m; lift 0.055 m; rear heel 58° |

The former speed/acceleration/response settings were removed because active swing speed comes from the stick. Body proportions and reach live in `BattingRig.cs`, gesture gating in `SwingGesture.cs`, and mode angles in `BattingStroke.cs`.

## Blender source and preview

Double-click **Preview Batsman.cmd**, or run:

```powershell
.\tools\Run.ps1 -BattingPreview
```

The lab shares the gameplay renderer. It displays separate stick/arm clock readings and whether a deliberate arc is active. With Joint markers enabled, orange shows the stick ray and teal shows the arm ray; X/B stop at their mode limits even if the stick continues. It offers live controls, front/back pressure sliders, slow/fast/reverse/front-foot/back-foot and all four shot demonstrations, looping, pause (Menu/Start or Space), one fixed tick (period), 25/50/100% playback, grip markers and JSON reload. It creates no delivery. Recorded ball replay is not implemented by this preview.

Open **Art/Batsman/batsman.blend** in Blender. The model includes cricket clothing, pads, shoes, gloves, helmet/grille and bat, with a review camera and studio lights saved only in the editable source. Seven baked sample actions—Fast, FrontFoot, BackFoot, Defence, FullDrive, Push and HighFinish—are available in the Action Editor at 60 FPS. They are review/editing references exported from the actual runtime pose system. Gameplay uses the current stick angle and constrained IK; the baked actions are review examples.

The source generator is **tools/build_batsman.py**, with the mesh/morph/skin adapter in **tools/batsman_anatomy.py**. Run it with Blender's background launcher:

```powershell
& "$env:LOCALAPPDATA\Microsoft\WindowsApps\blender-launcher.exe" --background --factory-startup --disable-autoexec --python-exit-code 1 --python "$PWD\tools\build_batsman.py"
```

It writes the editable blend, runtime `Assets/Characters/batsman.glb`, build counts and `artifacts/batsman-blender.log`. It uses a vendored CC0 MakeHuman whole-body surface, male morph targets and skin weights, reduced to 18 runtime bones. Clothing, gloves, pads, shoes, helmet and bat are generated locally. The source retains continuous shoulder/elbow topology and blended skin weights. `batsman-rig.json` records bone names, rest lengths and rig version; the runtime validates this at startup. See [asset provenance](../Art/Batsman/Reference/README.md). Blender and source assets are not runtime dependencies.

To refresh the baked actions after changing motion code, build the game and run Godot with `-- --batting-preview --capture-preview`, then rerun the Blender generator. The capture writes `Art/Batsman/reference-motion.json` and pose images under `artifacts/batsman-preview`. Reimport the GLB in Godot after rebuilding it.

## Runtime cost and verification

The renderer caches 18 bone references and uses one skinned mesh with shared materials. An adaptive 256/512/1024-pixel transparent viewport updates only when pose or display scale changes. No textures are loaded and no meshes are constructed during animation. The source directory is excluded from Godot imports. Physics remains 120 Hz with the existing eight collision substeps. Focused rendering has no engine FPS cap; VSync remains configurable in Godot, and focus loss pauses play and limits background rendering to 30 FPS.

`dotnet run --project Tests/Cricket.Physics.Tests.csproj` covers controls, trigger cancellation, separate grips, random movement continuity, 3D joint lengths, elbow continuity, rear-leg support, pitch clearance, planted toes, render-rate independence, collisions, momentum, energy and allocation behavior. Native capture additionally checks 1,476 combinations of weight/stroke/shot and all seven sample motion clips for stretched 3D limbs and agreement between the resolved rendered bat bone and the physical bat. See [performance measurements](PERFORMANCE.md) for the actual hardware results and export limitations.

## Research behind the motion

- [Stretch et al., front-foot drive kinetics (1998)](https://pubmed.ncbi.nlm.nih.gov/10189076/) and [Taliep et al., head and centre-of-mass movement (2007)](https://pubmed.ncbi.nlm.nih.gov/17933197/) informed forward body transfer and coordinated head movement.
- [Peploe et al., technique and bat speed (2019)](https://pubmed.ncbi.nlm.nih.gov/30500770/) informed pelvis/thorax separation, lead-arm extension and wrist release.
- [McErlain-Naylor et al., power-hitting kinematics (2021)](https://www.stuartmcnaylor.com/publication/male_female_batting/McErlain-Naylor_et_al_2021.pdf) informed the distinction between joint motion and bat speed. Group averages are not used as universal human joint limits.
- The supplied stance photographs guide guard position, moderate knee flexion, distinct grips and facing the bowler. The supplied GIF guides the deep front-foot variation and overhead follow-through.

Stride, timing, depth, grip adjustment and clearance values remain tunable gameplay choices. A physical controller play-test and further artist refinement are still useful for assessing feel and reference-level visual fidelity.

## Implementation files

| Change | Files |
| --- | --- |
| Replaced control/pose system | `Core/Animation/AthletePose.cs`, `SwingGesture.cs`, `ShotStyle.cs`, `BattingStroke.cs`, `BattingRig.cs`, `BattingMotion.cs`, `BattingSettings.cs`, `Config/batsman.json` |
| Added skinned presentation | `Scripts/Presentation/BatterModel.cs`, `Assets/Characters/batsman.glb` and its build/import metadata |
| Added editable sources | `tools/build_batsman.py`, `tools/batsman_anatomy.py`, `Art/Batsman/batsman.blend`, `reference-motion.json`, CC0 reference/provenance files |
| Updated integration/preview | `BattingControls.cs`, `GroundView.cs`, `BattingPreview.cs`, `Presentation/BattingPreviewSession.cs`, `GameController.cs`, `project.godot` |
| Added preview/export diagnostics | `Preview Batsman.cmd`, `Scripts/Diagnostics/BattingPreview.Capture.cs`, `tools/Benchmark-Batsman.ps1` |
| Updated validation | `Tests/BattingChecks.cs`, `Tests/AnimationChecks.cs`, `Tests/Program.cs`, gameplay automation and performance diagnostics |
| Removed obsolete renderer | `Scripts/Presentation/BatterDrawing.cs` and its UID |
| Updated guidance | README, batting/architecture/research/performance docs; ignored Blender backup files |

Existing unrelated practice, field, audio and project-instruction edits were retained.
