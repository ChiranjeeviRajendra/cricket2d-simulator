# Batsman controls, anatomy and Blender workflow

The batsman is a skinned 3D character composited into the 2D pitch. Ball flight and bat contacts run in the fixed-step physics core. One authoritative bat transform drives collision detection and the rendered bat.

## Controls

Clock positions describe the top-hand arm direction from its shoulder, viewed on screen with the bowler to the right. Move the left stick outward near 8:30 and rotate counterclockwise through 6 and 3 toward 12. The initial gate requires 60% radius and 6 degrees of deliberate rotation; radial pushes, centre crossings and small jitter cannot launch a stroke. Below 45% radius the stick is released.

| Mode | Input | Arm stop | Blade behavior |
| --- | --- | --- | --- |
| A, initial default | A / K, or no button before selecting another mode | 12 | Blade aligned with the arm |
| X | X / J | 2 | Finishes as a vertical wall |
| B | B / L | 1:30 | Blade finishes pointing at 4 |
| Y | Y / I | 12 | Follows A to 2, then turns toward 3 as the arms continue to 12 |

The selected mode persists on button release. Releasing the stick **or** the held shot button brakes the swing, then holds its resulting pose; there is no automatic return to guard or to A. A new deliberate arc re-engages from the actual held pose without teleporting. Returning toward the beginning of the stroke also obeys the speed limit. Buttons may change modes during a stroke, but the blade moves continuously into the new orientation. Right-stick batting remains disabled.

R2/RT (E) increases forward transfer from the default 35% stance to full forward transfer. L2/LT (Q) moves toward rear-leg support and the tallest stance. Equal trigger pressures cancel to the default stance. Trigger release returns body weight to that baseline; swing angle and shot mode remain held. Feet lift before stepping and remain planted while supporting weight.

L1/LB + R1/RB together on the same controller bowls; either shoulder can be pressed first. D-pad Up / keyboard Up only navigate menus. Menu/Start, Space or Escape pauses; View/Select or Enter opens delivery setup. Menus retain A/confirm and B/back. Free swings work before bowling while the delivery panel is closed.

## Swing speed and braking

The requested maximum is interpreted as **bat-tip linear speed**, including grip motion: 136 km/h = 37.7778 m/s. The initial driving envelope is 50 km/h = 13.8889 m/s. Slower stick motion can request a slower actual swing; these limits do not force a minimum speed when positioning the bat.

The drive uses actual angular travel along the arm swing, with an accelerating speed envelope:

```text
v^2 = v_start^2 + (v_max^2 - v_start^2) * min(travel_degrees / 25, 1)
```

This corresponds to constant tangential acceleration for a fixed effective swing radius. Local tip travel per degree is measured from the constrained pose, so arm angular speed is not mistaken for bat speed. On release, braking uses the equivalent of stopping from maximum speed within 10 degrees; lower-speed releases stop sooner. Reversals brake through zero before moving in the other direction. A nearer target or stroke endpoint can stop the bat sooner.

Both hand translation and blade rotation are checked against the speed ceiling, including shot-mode changes and re-arming. A bounded correction limits the next pose. Collision substeps interpolate the same accepted grip/angle endpoints; they do not receive an independent faster bat. The maximum velocity along that rigid-bat sweep is evaluated analytically, including its interior angular maximum.

Input and swing advance at 120 Hz, independent of rendered FPS. The collision path uses each tick's accepted motion, so a measured speed over the tick that crosses 25 degrees includes the earlier acceleration. In the regression fixture, 960 Hz sampling gives 51.85 km/h for the first moving interval, 135.40 km/h on crossing 25 degrees and a 135.99 km/h peak; at the production 120 Hz tick the interval measurements are 64.86, 122.67 and 135.34 km/h. Both stick and button releases stop in 10.000 degrees in this maximum-speed fixture. These are numerical measurements of the requested game-control profile, not measured athlete capabilities.

## Stance, anatomy and ground

Neutral starts already planted at 35% forward transfer. With corrected trigger difference d = R2 - L2, the weight parameter is 0.35 + 0.65d for d >= 0 and 0.35 + 1.35d for d < 0. Full front transfer uses a 0.60 m front-toe stride (previously 0.42 m) and lower pelvis. Full rear transfer raises the pelvis within leg reach. The measured default, full-front and full-back head-centre heights are 1.523, 1.230 and 1.640 m; full-front ankle spacing is approximately 1.032 m.

The bat is transparent to the pitch. Its blade may pass below ground without a collision, rebound, sound or wrist retraction. Anatomical two-hand reach still limits the grip; legs and feet still respect the ground. This intentional bat/ground exception does not disable ball/pitch or ball/bat contacts.

Separate pelvis, spine, chest, neck and head joints distribute body motion. Fixed-length IK maintains both hands on the handle, with one foot supported during stepping. Actual blade angle is included in the compact pose frame so partial mode transitions and held positions can be reconstructed. Rendered limbs use the same resolved bat as collisions. These remain authored motion approximations, without full-body self-collision or muscle-torque simulation.

## Editable settings

Edit Config/batsman.json, then restart or use Reload JSON in the batting lab. Invalid and obsolete properties are rejected.

| Object | Defaults |
| --- | --- |
| Input | Release radius 0.45; engage radius 0.60; rotation gate 6 degrees; start window +/-15 degrees; maximum sample jump 65 degrees; trigger deadzone 0.04 |
| Swing | StartSpeedKph 50; MaxSpeedKph 136; AccelerationDegrees 25; BrakingDegrees 10 |
| Body | DefaultForwardWeight 0.35; FrontStrideMetres 0.60; BackStrideMetres 0.14; grip spacing 0.105 m; transfer 0.28 s; step 0.24 s; lift 0.055 m; rear heel 58 degrees |

RecoveryDegreesPerSecond was removed with automatic recovery. Setting MaxSpeedKph above 136 is rejected. Body proportions and reach remain in BattingRig.cs, the angular motor in SwingMotion.cs, gesture gating in SwingGesture.cs and mode curves in BattingStroke.cs.

## Blender source and preview

Double-click **Preview Batsman.cmd**, or run:

```powershell
.\tools\Run.ps1 -BattingPreview
```

The lab shares the gameplay renderer. It displays separate stick/arm clock readings and whether a deliberate arc is active. With Joint markers enabled, orange shows the stick ray and teal shows the arm ray; X/B stop at their mode limits even if the stick continues. It offers live controls, front/back pressure sliders, slow/fast/reverse/front-foot/back-foot and all four shot demonstrations, looping, pause (Menu/Start or Space), one fixed tick (period), 25/50/100% playback, grip markers and JSON reload. It creates no delivery. Recorded ball replay is not implemented by this preview.

Open **Art/Batsman/batsman.blend** in Blender. The model includes cricket clothing, pads, shoes, gloves, helmet/grille and bat, with a review camera and studio lights saved only in the editable source. Seven baked sample actions—Fast, FrontFoot, BackFoot, Defence, FullDrive, Push and HighFinish—are available in the Action Editor at 60 FPS. They are review/editing references exported from the actual runtime pose system. Gameplay uses the speed-limited swing and constrained IK; the baked actions are review examples.

The source generator is **tools/build_batsman.py**, with the mesh/morph/skin adapter in **tools/batsman_anatomy.py**. Run it with Blender's background launcher:

```powershell
& "$env:LOCALAPPDATA\Microsoft\WindowsApps\blender-launcher.exe" --background --factory-startup --disable-autoexec --python-exit-code 1 --python "$PWD\tools\build_batsman.py"
```

It writes the editable blend, runtime `Assets/Characters/batsman.glb`, build counts and `artifacts/batsman-blender.log`. It uses a vendored CC0 MakeHuman whole-body surface, male morph targets and skin weights, reduced to 18 runtime bones. Clothing, gloves, pads, shoes, helmet and bat are generated locally. The source retains continuous shoulder/elbow topology and blended skin weights. `batsman-rig.json` records bone names, rest lengths and rig version; the runtime validates this at startup. See [asset provenance](../Art/Batsman/Reference/README.md). Blender and source assets are not runtime dependencies.

To refresh the baked actions after changing motion code, build the game and run Godot with `-- --batting-preview --capture-preview`, then rerun the Blender generator. The capture writes `Art/Batsman/reference-motion.json` and pose images under `artifacts/batsman-preview`. Reimport the GLB in Godot after rebuilding it.

## Runtime cost and verification

The 11 November 2026 swing revision passes all 67 executable checks, Debug and ExportRelease builds without warnings, and Godot gameplay smoke checks. Rendered capture verifies 1,476 anatomical poses plus seven motion clips, including agreement between the rendered bat and collision transform. Blender samples were regenerated and the GLB reimported. No physical controller was attached; controller inputs were synthesized by the smoke checks.

The speed governor uses value-type poses, bounded correction and no per-tick heap allocation. Collision sampling now interpolates the accepted bat endpoints, avoiding repeated full-body reconstruction in each collision substep. The allocation regression still passes. No render cap, physics frequency, asset resolution or node count was added by the governor.

The existing 4K benchmark used ExportRelease assemblies in the Godot runner, RTX 4080 SUPER, GL Compatibility, VSync enabled, a confirmed 3840 x 2160 render target and no engine FPS cap. Eight scenarios each warmed for two seconds and sampled for three. Averages were 120.00-120.33 FPS; p99 frame times ranged 8.404-8.645 ms. Live play measured 117.31 FPS at the 1% low, physics p95 1.504 ms, 273 managed bytes/frame across the application and zero managed collections. Settings dragging recorded one collection per generation; working set ranged 271-290 MiB. Results are in artifacts/performance-practice.json, with the earlier bounce-build run preserved as artifacts/performance-bounce-baseline.json. These short runs do not establish long-session stability or packaged-export performance. The final cleanup removed unused pose arguments and old gesture-anchor state after measurement; it did not change the motion algorithm.

The renderer caches 18 bone references and uses one skinned mesh with shared materials. An adaptive 256/512/1024-pixel transparent viewport updates only when pose or display scale changes. No textures are loaded and no meshes are constructed during animation. The source directory is excluded from Godot imports. Physics remains 120 Hz with the existing eight collision substeps. Focused rendering has no engine FPS cap; VSync remains configurable in Godot, and focus loss pauses play and limits background rendering to 30 FPS.

`dotnet run --project Tests/Cricket.Physics.Tests.csproj` covers controls, trigger cancellation, separate grips, random movement continuity, 3D joint lengths, elbow continuity, rear-leg support, ground-transparent bat motion, planted toes, render-rate independence, collisions, momentum, energy and allocation behavior. Native capture additionally checks 1,476 combinations of weight/stroke/shot and all seven sample motion clips for stretched 3D limbs and agreement between the resolved rendered bat bone and the physical bat. See [performance measurements](PERFORMANCE.md) for the actual hardware results and export limitations.

## Research behind the motion

- [Stretch et al., front-foot drive kinetics (1998)](https://pubmed.ncbi.nlm.nih.gov/10189076/) and [Taliep et al., head and centre-of-mass movement (2007)](https://pubmed.ncbi.nlm.nih.gov/17933197/) informed forward body transfer and coordinated head movement.
- [Peploe et al., technique and bat speed (2019)](https://pubmed.ncbi.nlm.nih.gov/30500770/) informed pelvis/thorax separation, lead-arm extension and wrist release.
- [McErlain-Naylor et al., power-hitting kinematics (2021)](https://www.stuartmcnaylor.com/publication/male_female_batting/McErlain-Naylor_et_al_2021.pdf) informed the distinction between joint motion and bat speed. Group averages are not used as universal human joint limits.
- The supplied stance photographs guide guard position, moderate knee flexion, distinct grips and facing the bowler. The supplied GIF guides the deep front-foot variation and overhead follow-through.

Stride, timing, depth and grip adjustment values remain tunable gameplay choices. A physical controller play-test and further artist refinement are still useful for assessing feel and reference-level visual fidelity.

## Files changed for the swing revision

| Change | Files |
| --- | --- |
| Added swing motor | `Core/Animation/SwingMotion.cs` |
| Updated input, stance and pose | `Core/Animation/BattingRig.cs`, `BattingMotion.cs`, `BattingSettings.cs`, `BattingStroke.cs`, `SwingGesture.cs`, `Core/Simulation.cs`, `Config/batsman.json`, `Scripts/Input/BattingControls.cs` |
| Updated preview and automation | `Scripts/Presentation/BattingPreview.cs`, `Scripts/Diagnostics/BattingPreview.Capture.cs`, `Scripts/Diagnostics/GameController.Automation.cs` |
| Refreshed source motion/assets | `Art/Batsman/reference-motion.json`, `Art/Batsman/batsman.blend`, `Assets/Characters/batsman.glb` |
| Added/updated regression checks | `Tests/SwingDynamicsChecks.cs` (new), `Tests/BattingChecks.cs`, `Tests/AnimationChecks.cs`, `Tests/Program.cs` |
| Updated guidance | `README.md`, `docs/BATTING.md`, `docs/CRICKET_REFERENCES.md` |

No files were removed. Automatic recovery, the obsolete recovery setting, the old stroke-anchor reset and bat ground-clearance adjustment were removed from the existing implementations.
