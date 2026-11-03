# Batsman behavior tuning

Edit `Config/batsman.json` and restart the game, or use **Reload JSON** in the batting preview (no C# rebuild is required). `BattingSettings` is the engine-independent configuration object. The same settings are reused whenever a delivery creates a fresh `BattingRig`. Invalid settings stop startup with an error naming the configuration problem; misspelled properties are rejected. Preview reloads reject invalid settings while retaining the working rig.

These defaults combine a procedural cricket swing with published bat-property and tip-speed references. The exact movement limits, reverse speeds and snap are gameplay choices. [CRICKET_REFERENCES.md](CRICKET_REFERENCES.md) records the online sources, conversions and remaining approximations.

## Angle convention and controls

All configuration angles are **degrees on the screen clock**, independent of the physics coordinate system:

| Clock | `ClockDegrees` |
| --- | ---: |
| 12 | 0 |
| 1 | 30 |
| 2 | 60 |
| 3 | 90 |
| 6 | 180 |
| 7 | 210 |
| 8 | 240 |
| 9 | 270 |
| 10 | 300 |
| 11 | 330 |

The left stick points the **shoulder-to-fist direction**. Its angle controls the whole swing; its vertical axis does not crouch the batter. Rotate counterclockwise through **8 → 7 → 2 → 1 → 12 → snap to 8**. Before reaching 12, reverse movement can return clockwise through the same manual arc, ending at 8. **8 is a hard reverse stop**: continuing backwards toward 9, 10 or 11 cannot move the arm or start a forward snap. The 12→11→10→9→8 arc is used only by the committed return. A centred left stick retains the last target. A forward stick crossing through 8 is remembered while the arm catches up.

The right stick points the **blade direction on screen**, pivoting about the shared fist. This request is converted to an offset from the current shoulder-to-fist direction and restricted by `Wrist.AnglesBySwing`. The actual arm position determines the limits, even while it is catching up with the left stick. Right-stick input never changes shoulder speed, elbow flexion, reach, hand position or stance.

Full right-stick deflection requests the full permitted angle. Partial deflection blends from the neutral angle toward that request. Release the right stick to ease back to neutral. Both stick axes matter; the previous right-X elbow inversion has been removed. WASD and arrow keys emulate the left and right sticks, including diagonals.

## Objects in the JSON

| Object | Purpose |
| --- | --- |
| `Input` | Independent radial `LeftDeadZone` and `RightDeadZone`; defaults 0.18. Applied once in the core. |
| `Swing` | Starting direction, target response, braking, reverse speed, speed/acceleration curve and committed return. |
| `Wrist` | Bat angle limits and neutral angle at each shoulder position; response and maximum adjustment speed. |
| `Arms` | Elbow flexion by shoulder position and the two elbow bend directions. |
| `Stance` | Hip/torso height, hip transfer, shoulder lean, crouch and minimum pitch clearance. |
| `BodyMotion` | Body settling/recovery times and additional speed-driven hip transfer and crouch. |
| `Footwork` | Front-foot step distance, lift, duration, speed trigger and rear-heel pivot. |
| `HeadMotion` | Bounded head compensation and settling time. |

Every `*BySwing` array is a circular keyframe curve. Keep `ClockDegrees` unique, ascending and in `[0, 360)`. Values interpolate linearly between keys, including the last key through 12 o'clock to the first. Add keys at any angle for finer control; at least two keys are required. Do not add a duplicate 360-degree key.

## Wrist angles around the fist

`Wrist.AnglesBySwing` contains `MinDegrees`, `NeutralDegrees` and `MaxDegrees`. These are **relative to the shoulder-to-fist ray**: zero extends the bat along the arm; positive rotates clockwise on screen, negative counterclockwise. For example:

```json
{ "ClockDegrees": 180, "MinDegrees": -45, "NeutralDegrees": 0, "MaxDegrees": 55 }
```

With the arm at 6 o'clock, this allows the blade to turn 45 degrees counterclockwise or 55 degrees clockwise about the fist. A centred right stick keeps it straight. Change just this key to alter the bottom of the swing; neighboring positions blend toward it.

The default neutral angle and elbow flexion are both zero from **8 through 7, 6, 5, 4, 3, 2 to 1**. With no right-stick input, both arms and bat stay straight throughout that arc. Above 1, the elbows fold and the neutral wrist angle tucks the bat into the follow-through. `Wrist.Response` (per second) controls how quickly a wrist adjustment settles; `MaxSpeedDegreesPerSecond` caps the player-driven offset. The neutral curve and current angle limits remain authoritative as the arm moves.

## Swing speed and the committed return

`Swing.SpeedBySwing` sets maximum degrees/second and acceleration in degrees/second² at each shoulder position. `TargetResponse` is the target-following gain in inverse seconds; `BrakingDegreesPerSecondSquared` limits normal braking. Slow stick rotations produce slow swings; moving the target farther ahead allows acceleration up to the local speed limit. The curve is a speed ceiling, not an automatic swing command.

| Forward phase | Default speed ceiling |
| --- | --- |
| 8 → 7 | Accelerates toward a ceiling rising from 700 to 1,100 degrees/s |
| 7 → 2 | 1,100 degrees/s |
| 2 → 12 | Falls to 420 degrees/s |
| 12 → 8 | Committed return, up to 1,440 degrees/s |

`ReverseMaxSpeedDegreesPerSecond` caps the backlift at **240 degrees/s**. `ReverseSpeedScale = 0.30` also restricts it to 30% of the local forward speed ceiling; the lower of these two limits wins. `ReverseAccelerationDegreesPerSecondSquared = 1800` gives the backlift a slower buildup than the forward stroke. Normal braking first removes forward momentum when direction changes. These values are adjustable gameplay approximations, not claimed measured human backlift values.

`Swing.Snap.StartClockDegrees = 0` is **12 o'clock**, and `EndClockDegrees = 240` is **8 o'clock**. The endpoint also defines the hard reverse stop, so changing it moves both together. The snap takes over when the **actual arm** reaches 12; centring or reversing the left stick cannot cancel it. Reverse input is accepted up to this threshold, not after commitment. The right stick remains active. The return uses rapid continuous physics substeps, keeping the bat collidable without teleportation.

After a snap, holding the stick at the follow-through position keeps the arm at 8 instead of starting repeated unwanted revolutions. Continue rotating forward through 8, return to the first `RearmWindowDegrees` of the next swing (90 degrees by default, from 8 through 5), or release the left stick to rearm. After pushing against the reverse stop, return the stick to 8, centre it, or make a direct aim into the playable arc. `Snap.SpeedDegreesPerSecond` and `AccelerationDegreesPerSecondSquared` tune recovery. The snap arc must be smaller than 180 degrees; the initial angle must lie outside it.

## Arms, stance and clearance

`Arms.FlexionBySwing` uses 0 degrees for a straight arm and permits up to 135 degrees of flexion. Both shoulders share a root in the side projection, and both hands share the bat grip. Two-link inverse kinematics preserves arm lengths while the grip stays on the left-stick direction. `LeadElbowBend` and `RearElbowBend` are each either -1 or 1.

`Stance.PoseBySwing` specifies `HipShiftMetres`, `ShoulderLeanMetres` and `CrouchMetres`. Position offsets use world metres: negative X moves toward the bowler (right on screen). Hip shifts are relative to the normal batting stance; shoulder lean is relative to the hip. These targets settle smoothly. Leg IK follows the moving hip and foot targets while preserving thigh and shin lengths.

The default stance is tall enough to keep the fully straight arm and bat above the pitch at 6 o'clock. Configuration validation rejects stance heights that would require ground correction or overstretch a leg. This keeps wrist limits and shoulder control independent: there is no hidden wrist correction that bends the straight bat or moves the shoulder. Bat length and collision properties remain in `Config/physics.json`.

## Verification

Run `dotnet run --project Tests/Cricket.Physics.Tests.csproj`. Checks cover screen-clock directions, the straight hitting arc, independent pivots, interpolated wrist limits, the speed phases, snap commitment, continuous revolutions, dead zones, configuration loading/reset/validation, bone lengths, ground clearance and playable contacts. The Godot `--smoke-test` also injects both stick circles into the input bindings and verifies the wrist pivot using the loaded JSON.

## Full-body motion

The supporting animation observes actual shoulder speed and position, not the requested stick direction. Its phases are `Ready`, `Backlift`, `ForwardSwing`, `FollowThrough` and `Recovery`. The last 60 degrees of manual forward travel are follow-through. Snap and the foot return use recovery. These phases never modify shoulder or wrist control.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `BodyMotion.SettlingSeconds` | 0.10 s | Critically damped stance settling, approximately 98% settled for a constant target |
| `BodyMotion.RecoverySeconds` | 0.22 s | Slower body settling during recovery and front-foot return duration |
| `BodyMotion.HipTransferMetres` | 0.025 m | Additional transfer toward the bowler, scaled by actual forward speed / local speed ceiling |
| `BodyMotion.AdditionalCrouchMetres` | 0.005 m | Additional crouch using the same speed fraction |
| `Footwork.StepMetres` | 0.05 m | Front-foot travel toward the bowler |
| `Footwork.LiftMetres` | 0.015 m | Peak lift along the step/return path |
| `Footwork.StepSeconds` | 0.12 s | Step duration; a resumed/interrupted step retains its current position and velocity |
| `Footwork.TriggerDegreesPerSecond` | 60°/s | Actual forward shoulder speed required to start a step |
| `Footwork.RearHeelDegrees` | 10° | Heel lift around a fixed toe after the front foot plants |
| `HeadMotion.CompensationDegrees` | 6° | Maximum neck/head counter-rotation as the torso leans |
| `HeadMotion.SettlingSeconds` | 0.10 s | Head compensation settling |

The front foot steps once, then remains fixed for the stroke. Reversing before planting returns the foot toward guard. A planted stroke retains its foot support during backlift until the shoulder returns to 8. After the committed snap, the foot returns; a new forward swing can interrupt that return. The rear toe always stays fixed. `HumanPose` exposes `FrontFoot`, `RearFoot`, `HeadAngle` and `MotionState`; each foot has a toe anchor, orientation and `Planted`, `Airborne` or `ToePivot` contact state.

Shoulder translation changes the physical fist and bat positions inside each physics substep. It therefore contributes to contact velocity. Head compensation is visual anatomy and never alters the bat. The head keeps facing the bowler. Right-stick input cannot change any body, head or foot state.

The new objects are optional when loading older JSON and receive the defaults above. Validation checks the combined stance, hip-transfer, crouch, lifted-foot and heel-pivot envelope, including combinations caused by animation lag. It rejects settings that require stretching a leg or violating bat clearance. These motion values are animation choices, not measurements from the cricket-reference studies.

## Close-up preview

Launch with `./tools/Run.ps1 -BattingPreview`, or pass `-- --batting-preview` to Godot. This starts an isolated rig with the same character drawing code; it creates no delivery. Use live sticks/WASD and arrows, or select a repeatable Slow, Fast, Reverse, Snap or Wrist demonstration. The Snap demonstration requests reversal during the committed return. Demo timing is fixed so configuration changes can be compared with identical input.

Controls include looping, pause/resume (Space), a single 1/120-second tick (period), Reset, 25%/50%/100% playback, joint markers and Reload JSON. Playback changes elapsed rehearsal time, never the integration step. A single tick stays paused and displays the new pose immediately. Focus loss also freezes the preview.

Reload reads the saved file. A valid reload resets the rehearsal using the new settings; an invalid reload shows the reason and retains the current pose, settings and timeline. Neither path writes configuration. Phase, shoulder clock/speed, wrist angle/limits, foot contacts, head compensation and snap status are displayed beside the character.

`./tools/Run.ps1 -BattingPreview -Headless -SmokeTest` exercises native UI playback, pause, stepping and valid/invalid reload. Godot arguments `-- --batting-preview --capture-preview` save close-up normal/quarter-speed frames under `artifacts/batting-preview/`. Core checks also cover interruptions, consecutive strokes, exact planted anchors, fixed bone lengths, pitch clearance, wrist independence, snap/reset interpolation and equivalent render poses at 30, 60 and 144 FPS.
