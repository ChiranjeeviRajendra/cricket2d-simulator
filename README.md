# Cricket 2D — The Practice Ground

A native Godot 4.7.2 .NET / C# cricket practice game. Configure a delivery, control an articulated batter with two sticks, and repeat. This first playable version implements the supplied blueprint's physics-first architecture.

## Play on this PC

Double-click **`Play.cmd`**. It finds Godot .NET in your Downloads folder, builds the C# projects using the packages bundled with Godot, and launches the game. Godot 4.7.2 .NET and .NET SDK 10 are already installed; **no additional application is needed**.

Alternatively, open `project.godot` in the Godot **.NET** editor, build, and press **F6/F5**. Run `Play.cmd` once before the first editor build to copy Godot's local dependency packages.

If Godot has moved:

```powershell
.\tools\Run.ps1 -GodotPath 'C:\Tools\Godot\Godot_v4.7.2-stable_mono_win64_console.exe'
```

You can also set `GODOT4` to the Godot executable. `Play.cmd` uses a process-local PowerShell execution-policy override; it does not change the machine's execution policy. The launcher does not install applications or require administrator access. The initial build needs a .NET 10 SDK, selected by `global.json`. Dependency packages and build outputs stay in this repository's ignored directories. Godot uses its ordinary Windows user-profile cache directories.

## Delivery setup

The game opens on the **Main Menu** with **Play Match** (disabled), **Practice**, **Settings** (disabled) and **Exit**. Select **Practice → Batting** to enter the existing batting gameplay; **Bowling** is disabled. Menus support mouse clicks, the left stick, arrow keys/D-pad, Enter/A to select, and Escape/B to go back from Practice. Selected items use a charcoal fill and soft ivory text; the remaining UI uses soft ivory panels and warm grey text.

Batting starts on the idle field. Press **A / Enter** to open **Batting Practice Settings**. Press **B / Escape** to return to the main menu and pause the session; selecting Practice → Batting again resumes it. The top-right Menu/A buttons and popup Begin button have been removed.

| Setting | Meaning | Supported range |
| --- | --- | --- |
| Release speed | Magnitude of velocity when the ball leaves the hand | 18–180 km/h |
| Pitching length | First ground contact, measured **from the bowler-end wicket** toward the striker | 8–25 m |
| Release height | Height of the **ball centre** above the pitch; not the bowler's body height | 1.53–2.16 m (153–216 cm) |

Pitch length and its slider share one frame at the top. Drag the slider or edit the number; they update each other live. The entire diagram uses one 0–25 m scale: the bowler's stumps are at 0 m on the right, the batter's stumps at 20.12 m, and the left limit extends 4.88 m beyond them. The selectable range remains 8–25 m; the 0–8 m section is shown in grey for scale. Left moves the slider farther from the bowler; right moves it closer. D-pad up/down selects pitch length, speed and height in that order.

With the panel open, press **A / Enter** to begin. The automated bowler winds up for 1.25 s, then releases the ball. You can move the bat during the windup. There is **no timed reset**: the ball continues naturally until you press **A / Enter** for the next delivery settings. Opening settings and beginning a delivery require separate presses. Opening settings retains the previous delivery's maxima; beginning a delivery clears them. **B / Escape** returns to the main menu from either settings or play. Leaving the window pauses the simulation.

The bowler calculates a launch angle using the same aerodynamic integrator as the live ball. It selects the lower reachable trajectory and rejects impossible combinations. Length describes the unimpeded first pitch and can extend beyond the striker's wicket: a bat or wicket interception can prevent the ball reaching that point. Release height can be adjusted in 0.01 m increments, including both endpoints.

The release is at x = 1.2192 m, on the bowler's popping crease. The length setting still uses x = 0 at the wicket, so a length of 16 m means 14.7808 m horizontally from release. Both wickets remain visible, **exactly 20.12 m apart**. The **batter is on the left** and the **bowler is on the right** in the initial view.

The batter is always visible and permanently invulnerable to the ball. The ball passes through his body, pads and helmet without a collision response. **The bat and wickets remain collidable.**

## Stats, camera and boundaries

The camera automatically zooms out to keep the ball and both wickets visible. When the ball moves beyond the left side, the wickets remain toward the right edge; when it moves beyond the right side, they remain toward the left edge. High shots also expand the view vertically. The camera keeps the widest zoom for the rest of that delivery and returns to the initial view when settings reopen.

The screen displays only four compact stats: **live ball speed**, **maximum ball speed**, **maximum length from the batter**, and **maximum height above ground**. Both speeds use **km/h**; distances use **metres**. Maxima are tracked from release through the entire delivery, including incoming flight, bat contact, bounce and rolling. Length is the greatest absolute horizontal distance from the batter's fixed normal stance, on either side; height uses the ball centre above ground. The maxima do not decrease when the ball slows, comes closer or falls. The bottom instructions, countdown, extra telemetry, ruler and live-height guide have been removed.

The pitch is surrounded by white triangular boundary cushions, **40 m left** and **75 m right** of the batter's normal stance. A solid oval barricade sits **5 m clear beyond the wedge**, with parking outside it. Both obstacles collide with the ball: the wedge deflects it according to its slope and friction, and the barricade uses the pitch's rebound settings by default. High balls can clear either object. The old distance signboards are removed. Delivery pitching length still starts at the bowler-end wicket.

Edit [`Config/field.json`](Config/field.json) and restart to change boundary distances, wedge dimensions/material response, barricade spacing or parking width. [`docs/STADIUM.md`](docs/STADIUM.md) explains the shared geometry, 2D collision plane and editable sky artwork. [`docs/BOUNDARY_REFERENCES.md`](docs/BOUNDARY_REFERENCES.md) records the research and distinguishes measured dimensions from tunable impact assumptions.

## Controls

| Action | Controller | Keyboard |
| --- | --- | --- |
| Aim the full shoulder swing | Left stick, rotate through 360 degrees | WASD (diagonals supported) |
| Aim the blade about the fist | Right stick, constrained by current swing angle | Arrow keys (diagonals supported) |
| Select setup field | D-pad up / down | Tab / Shift+Tab or click |
| Change setup value | D-pad left / right | Type a number or use the spinner |
| Begin from settings | A / south button | Enter |
| Open / return to settings | A / south button | Enter |
| Toggle trajectory | — | T |
| Full screen | — | F11 |
| Main menu (pauses batting) | B / east button | Escape |

Start at the default **115.2 km/h, 16 m, 2.15 m**. The left stick points the shoulder-to-fist direction on the screen clock. Rotate counterclockwise from **8 through 6 toward 1** for the straight-arm hitting arc. The swing accelerates from 8 to 7, peaks through 7 to 2, slows from 2 to 12, then commits to a fast return from **12 to 8**. Before reaching 12, you can reverse through the same arc, with a hard stop at **8**. Backlift speed is capped at 240 degrees/s and 30% of the local forward limit, with slower acceleration. Reversing cannot enter the snap-only arc. Once the arm reaches 12, the return cannot be reversed or cancelled.

The right stick points the blade about the fist, within limits determined by the current shoulder angle. It never moves the shoulder or hands. With the right stick centred, the arms and bat stay straight from 8 to 1. Partial right-stick deflection makes a smaller wrist adjustment. Releasing the right stick eases the bat back to its neutral angle; releasing the left stick retains the last swing target. After the snap, continue rotating through 8 or release the left stick to start another swing. Holding the follow-through position does not trigger repeated swings.

Controller bindings use Godot's standard gamepad mappings with separately configurable radial dead zones (0.18 by default) and support connection during play. Synthetic controller circles and delivery menu events are covered by runtime checks. Physical controller feel still needs hands-on play testing.

## Implemented physics and animation

- SI-only simulation: metres, seconds, kilograms and radians. Speed entry and display convert between km/h and the core's m/s; pixel conversion exists only in the drawing layer.
- A 0.156 kg, 0.072 m diameter ball with gravity, quadratic air drag, a bounded 2D Magnus approximation and spin decay. Deliveries start without spin; impacts create spin.
- RK4 flight integration at a fixed 120 Hz with eight substeps per tick. Ground contact time is solved within the substep.
- Pitch restitution, Coulomb friction, ball angular inertia, slipping-to-rolling transition and rolling resistance. Passive ground contact dissipates energy.
- Swept ball-versus-moving-capsule contact detection for the bat. Impulses depend on the bat contact-point velocity, finite effective mass, rotational lever arm and friction.
- Bat and wicket contacts; an always-visible, invulnerable batter; a trajectory trail and persistent per-delivery speed, distance and height maxima.
- Fixed-length arms and legs, straight arms through the hitting arc, a full shoulder circle with phase-specific speed limits, independent wrist rotation, two-link IK, an automatic front-foot step, planted rear-toe pivot, head compensation and configurable weight transfer.
- Procedural field presentation with a configurable oval wedge, outer barricade, surrounding parking and an Inkscape daylight sky. Physical objects retain a shared metre scale; scenery depth uses perspective that matches the ball plane at each boundary crossing. Four compact stats and a minimum drawn ball radius keep gameplay readable during camera zoom.

Tune the batsman in [`Config/batsman.json`](Config/batsman.json): separate `Input`, `Swing`, `Wrist`, `Arms`, `Stance`, `BodyMotion`, `Footwork` and `HeadMotion` objects control stick response, speed phases, the snap, angle limits and body movement. Edit the JSON and restart the game, or reload it in the batting preview; no code rebuild is needed. [`docs/BATTING.md`](docs/BATTING.md) explains the angle convention, every object and examples. Defaults approximate a cricket swing and remain adjustable for play feel.

The tunable physical coefficients are in [`Config/physics.json`](Config/physics.json). Their units and defaults are documented in [`docs/PHYSICS.md`](docs/PHYSICS.md). [`docs/CRICKET_REFERENCES.md`](docs/CRICKET_REFERENCES.md) links the published bat mass/inertia, tip-speed and impact references and explains the conversions and remaining tuning assumptions.

## Code layout

For close-up animation tuning, run:

```powershell
.\tools\Run.ps1 -BattingPreview
```

The preview offers live controls, slow/fast/reverse/snap/wrist demonstrations, looping, pause, single-tick stepping, 25%/50%/100% playback and joint/contact telemetry. **Reload JSON** resets the preview on success or keeps the working configuration and shows an error. It never changes a delivery or writes configuration. See [the tuning guide](docs/BATTING.md#close-up-preview).

The match and preview share the character renderer. Both interpolate pose parameters before rebuilding limbs with IK, preserving bone lengths and the bat's fist pivot.

```text
Core/                    Engine-independent .NET physics library
  Physics/               Flight, delivery solver, collision and contact laws
  Animation/             Batting joints, human pose and IK
  Statistics/            Per-delivery maxima sampled during physics updates
  Simulation.cs          Fixed timestep and delivery state machine
Presentation/            Engine-independent camera helpers and batting preview session
Scripts/
  GameController.cs      Connects simulation, input and presentation
  Input/                 Named keyboard/gamepad actions
  Presentation/          Ground, characters, HUD and setup panel
Config/physics.json      Physical coefficients in SI units
Config/batsman.json      Swing, wrist, arms, stance and stick tuning
Scenes/Main.tscn         Godot entry point
Tests/                   Offline executable physics validation
tools/Run.ps1            Build, test and launch
```

The core has no dependency on Godot. Rendering cannot modify the ball, and the UI passes only validated delivery settings. See [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) for extension points and deliberate approximations.

## Validation

Build and run the automated checks:

```powershell
.\tools\Run.ps1 -BuildOnly -Test
```

Run the core checks independently of Godot:

```powershell
dotnet run --project Tests/Cricket.Physics.Tests.csproj
```

Run the actual Godot scene through A-button setup, bowling, bounce, continued play beyond five seconds, manual return to setup and a second delivery:

```powershell
.\tools\Run.ps1 -Headless -SmokeTest
```

Tests return a nonzero exit code on failure. They cover analytic vacuum flight, drag, 1,000 random bounce-energy checks, rolling friction, bat impulses, fast swept contacts, 48 launch combinations, invalid/unreachable deliveries, landing accuracy, continued play without a timed reset, retained/reset maxima, km/h conversion, playable swing sequences, bone lengths and determinism at different render rates. Camera checks cover both travel directions, direction changes, high shots, reset, and fixed boundary/height measurements. The Godot smoke test verifies A/Enter and B/Escape parity, mouse/keyboard/stick navigation, disabled choices, scaled slider dragging, live numeric editing, separate opening/beginning presses, speed entry in km/h, both stick circles, screen directions, and independent wrist rotation using the loaded batsman configuration.

## Current scope

This is a physically grounded **2D prototype**, with coefficients that still need calibration against measured cricket footage. It does not simulate lateral seam/swing, off-side/leg-side shot placement, muscle dynamics, full body-bat recoil, deformable pitch/ball/bat materials, fielding, scoring, audio or match rules. Wicket movement is a visual hit response. The bowler is an automated target solver with a procedural delivery animation, without tactical decision making. Art is deliberately simple while motion and contact are developed.

The game runs through the installed Godot runtime. A standalone distributable `.exe` has not been exported; that later step needs the matching Godot .NET export templates.

Pitch/equipment references: [MCC pitch dimensions](https://www.lords.org/mcc/the-laws/the-pitch), [MCC wickets](https://www.lords.org/mcc/the-laws/the-wickets), [MCC ball](https://www.lords.org/mcc/the-laws/the-ball). Engine references: [Godot C#](https://docs.godotengine.org/en/stable/tutorials/scripting/c_sharp/c_sharp_basics.html), [Godot controllers](https://docs.godotengine.org/en/stable/tutorials/inputs/controllers_gamepads_joysticks.html).
