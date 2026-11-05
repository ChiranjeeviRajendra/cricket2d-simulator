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

Focused rendering has **no engine FPS cap**, with VSync enabled. Focus loss pauses play and limits background rendering to **30 FPS**. Use `.\tools\Run.ps1 -FourK` for fullscreen 4K, or `-PerformanceTest` for an automated rendered benchmark. The [performance audit](docs/PERFORMANCE.md) records the measured improvements, hardware, regression checks and limitations.

## Delivery setup

The game opens on the **Main Menu** with **Play Match** (disabled), **Practice**, **Settings** (disabled) and **Exit**. Select **Practice → Batting** to enter the existing batting gameplay; **Bowling** is disabled. Menus support mouse clicks, the left stick, arrow keys/D-pad, Enter/A to select, and Escape/B to go back from Practice. Selected items use a charcoal fill and soft ivory text; the remaining UI uses soft ivory panels and warm grey text.

Batting starts on the idle field. Press **View / Select / Enter** to open **Batting Practice Settings**. **Bowl** sits at the top and is focused by default, remaining disabled until preparation finishes. Inside the popup, A/Enter activates the focused selector, checkbox or Bowl button. Only Bowl starts a delivery. Inside setup, **B / Escape** closes the bowler list or returns to the main menu. During play, **Menu / Start** pauses and **Escape** opens the main menu; A/B are reserved for shots. Selecting Practice → Batting again resumes the same session and pending delivery.

Choose a bowler at the top of the popup. **Medium** is selected initially, with both random options enabled:

| Bowler | Random release speed |
| --- | ---: |
| Super Slow | 18–49.9 km/h |
| Slow | 50–80 km/h |
| Medium | 80.1–110 km/h |
| Fast | 110.1–130 km/h |
| Super Fast | 130.1–180 km/h |

**Random pitch length** samples 8–25 m; uncheck it to reveal the existing scaled pitch slider. **Random release speed** shows the selected bowler's range; uncheck it for a manual 18–180 km/h slider. Changing manual speed automatically selects its bowler category. Explicitly choosing a category clamps the remembered manual speed to that category's nearest limit. Release height is always manual. Initial manual values are 16 m, 95 km/h and 2.15 m.

| Setting | Meaning | Supported range |
| --- | --- | --- |
| Release speed | Magnitude of velocity when the ball leaves the hand | 18–180 km/h |
| Pitching length | First ground contact, measured **from the bowler-end wicket** toward the striker | 8–25 m |
| Release height | Height of the **ball centre** above the pitch; not the bowler's body height | 1.53–2.16 m (153–216 cm) |

Settings retain their unnumbered boxes and live value badges. The pitch diagram uses one 0–25 m scale: the bowler's stumps are at 0 m on the right, the batter's stumps at 20.12 m, and the left limit extends 4.88 m beyond them. The selectable range remains 8–25 m; the 0–8 m section is shown in grey for scale. Left moves the pitch slider farther from the bowler; right moves it closer. Speed and height increase to the right. Use mouse drag, arrows, D-pad or the left stick. Up/down and Tab/Shift+Tab navigate visible controls; hidden sliders are skipped. The body scrolls when expanded, while Bowl and delivery status stay visible.

Activate **Bowl** when preparation is complete. The automated bowler winds up for 1.25 s, then releases the ball. You can move the bat during the windup. There is **no timed reset**: the ball continues until you press **A / Enter** for the next delivery settings. Each new ball gets a new random seed; reopening an unbowled setup or returning from the main menu keeps the pending ball. Opening settings retains the previous delivery's maxima; bowling clears them. Leaving the window pauses the simulation. Practice choices last for the current game session and are not written to disk.

**Show pitch position** and **Show predicted bowling trajectory** default on and are independent. Turning off the position guide hides the marker throughout the delivery. The predicted path appears only before release. Neither checkbox rerolls the ball. The actual live ball trail has its own **T** shortcut and is enabled initially; T does not change either prediction guide. Random settings display ranges rather than the sampled values.

The bowler calculates a launch angle using the same aerodynamic integrator as the live ball. A cancellable worker prepares the exact plan later used for bowling. Random values are sampled uniformly at 0.01 m / 0.1 km/h resolution, including endpoints, then physically unreachable combinations are rejected. Generation retries only randomized fields, at most 64 times, then tries the shortest randomized length and highest randomized speed. Manual values are never changed by this feasibility check. If no permitted combination works, Bowl remains disabled with an actionable error. Very low manual Super Slow speeds cannot reach even 8 m with the current physics. Length describes the unimpeded first pitch; bat or wicket interception can prevent that landing. Release height uses 0.01 m increments, including both endpoints.

The release is at x = 1.2192 m, on the bowler's popping crease. The length setting still uses x = 0 at the wicket, so a length of 16 m means 14.7808 m horizontally from release. Both wickets remain visible, **exactly 20.12 m apart**. The **batter is on the left** and the **bowler is on the right** in the initial view.

The batter is always visible and permanently invulnerable to the ball. The ball passes through his body, pads and helmet without a collision response. **The bat and wickets remain collidable.**

Bat contacts now use the supplied **perfect-shot** or **imperfect-shot** recording based on contact position and face alignment; wicket hits use the supplied **wicket** sound. Quieter impacts play more softly. Add sample variations and tune selection/volume in `Config/audio.json`; see [AUDIO.md](docs/AUDIO.md) for extending the sound library.

## Stats, camera and boundaries

The camera automatically zooms out to keep the ball and both wickets visible. When the ball moves beyond the left side, the wickets remain toward the right edge; when it moves beyond the right side, they remain toward the left edge. High shots also expand the view vertically. The camera keeps the widest zoom for the rest of that delivery and returns to the initial view when settings reopen.

The screen displays five compact stats: **live ball speed**, **maximum ball speed**, **maximum length from the batter**, **maximum height above ground**, and **FPS**. FPS shows Godot's measured rendering frame rate, independently of the fixed physics tick rate. Both speeds use **km/h**; distances use **metres**. Maxima are tracked from release through the entire delivery, including incoming flight, bat contact, bounce and rolling. Length is the greatest absolute horizontal distance from the batter's fixed normal stance, on either side; height uses the ball centre above ground. The maxima do not decrease when the ball slows, comes closer or falls. The bottom instructions, countdown, extra telemetry, ruler and live-height guide have been removed.

The pitch is surrounded by white triangular boundary cushions, **40 m left** and **75 m right** of the batter's normal stance. A **1.5 m-high** solid oval barricade sits **5 m clear beyond the wedge**. Outside it, **28 stepped seating tiers** rise to the **25 m outer wall**, with coloured seats, seated spectators, access staircases and handrails. Parking has been removed. The wedge, walls and terrace treads/risers collide with the ball; walls and terraces share the pitch's rebound settings by default. High shots can clear the stadium. Delivery pitching length still starts at the bowler-end wicket.

Edit [`Config/field.json`](Config/field.json) and restart to change boundary distances, wedge dimensions/material response, wall heights, seating width, row count, aisles, seat spacing or crowd density. The outer wall follows the back of the seating automatically. [`docs/STADIUM.md`](docs/STADIUM.md) explains the shared geometry, 2D collision plane and editable Inkscape sky/crowd artwork. [`docs/BOUNDARY_REFERENCES.md`](docs/BOUNDARY_REFERENCES.md) records the research and distinguishes measured dimensions from tunable impact assumptions.

## Controls

| Action | Controller | Keyboard |
| --- | --- | --- |
| Swing | Rotate left stick counterclockwise: 8:30 → 6 → 3 → 12 | Overlapping WASD directions |
| X: stop at 2; vertical bat face | Hold X at any point | J |
| A: aligned arm/blade, 8:30 to 12 (default) | No button or hold A | No modifier or K |
| B: arms stop at 1:30; blade points at 4 | Hold B at any point | L |
| Y: follows A to 2, then blade turns toward 3 as arms reach 12 | Hold Y at any point | I |
| Front / back weight | R2 / L2, progressive | E / Q |
| Pause / resume | Menu / Start | Space outside setup controls |
| Open / return to delivery settings | View / Select | Enter |
| Navigate setup | D-pad | Arrows, Tab or mouse |
| Activate setup control (Bowl starts delivery) | A | Enter / Space |
| Back within setup/menu | B | Escape |
| Main menu during play | View, then B | Escape |
| Toggle ball trail / full screen | — | T / F11 |

Start the stick beyond 60% radius near 8:30, then rotate through 6 and 3. A direct push, centre crossing or small movement cannot start a swing. After the initial 6° gate, the arm clock matches the stick on the current physics tick with no animation easing or speed limiter. Holding stops immediately; reversing reverses the stroke. Centre the stick to recover toward 8:30. A fresh deliberate rotation can interrupt that recovery immediately; an off-angle push no longer blocks later entry into the start area. Consecutive full rotations also start consecutive strokes without requiring a centre release. Change the held mode button before, during or after the swing; it applies at the current stick position without re-arming. Releasing modifiers returns to A. Free swings also work before bowling, while the delivery panel is closed. No button is A.

The right stick is inactive for now. R2 progressively shifts weight forward and lowers the rear knee; L2 moves support backward. Opposing trigger pressures cancel. See [the batting guide](docs/BATTING.md) for gesture tuning, preview controls and the Blender workflow.

Start with the default Medium bowler. For repeatable deliveries, disable random speed/length and use **95 km/h, 16 m, 2.15 m**. Controller events are covered by synthetic native checks; physical-controller feel still needs hands-on testing.

## Implemented physics and animation

- SI-only simulation: metres, seconds, kilograms and radians. Speed entry and display convert between km/h and the core's m/s; pixel conversion exists only in the drawing layer.
- A 0.156 kg, 0.072 m diameter ball with gravity, quadratic air drag, a bounded 2D Magnus approximation and spin decay. Deliveries start without spin; impacts create spin.
- RK4 flight integration at a fixed 120 Hz with eight substeps per tick. Ground contact time is solved within the substep.
- Pitch restitution, Coulomb friction, ball angular inertia, slipping-to-rolling transition and rolling resistance. Passive ground contact dissipates energy.
- Swept ball-versus-moving-capsule contact detection for the bat. Impulses depend on the bat contact-point velocity, finite effective mass, rotational lever arm and friction.
- Bat and wicket contacts; an always-visible, invulnerable batter; a trajectory trail and persistent per-delivery speed, distance and height maxima.
- Blender-built skinned cricketer with separate grips, direct clock-angle modes, proportional trigger footwork, planted toe pivots, pelvis/spine/chest/head articulation and fixed-length limb IK. The same physical bat transform drives rendering and collisions.
- Procedural field presentation with a configurable oval wedge, inner and outer barricades, stepped stadium seating, a static spectator crowd and an Inkscape daylight sky. Physical objects retain a shared metre scale; scenery depth uses perspective that matches the ball plane at each boundary crossing. Five compact stats and a minimum drawn ball radius keep gameplay readable during camera zoom.

Tune the batsman in [`Config/batsman.json`](Config/batsman.json): `Input`, `Swing` and `Body` configure controls and motion. Edit and reload in the batting lab. [`docs/BATTING.md`](docs/BATTING.md) explains the settings, biomechanics sources, limitations and Blender workflow. Open `Art/Batsman/batsman.blend` to inspect the model and seven editable sample actions.
