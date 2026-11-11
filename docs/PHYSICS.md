# Physics assumptions and units

All core distances are metres, time is seconds, mass is kilograms, velocity is m/s, angular velocity is rad/s, forces are newtons and impulse is N·s. The world origin is the bowler-end wicket at pitch level. The drawing layer starts at approximately 69.18 pixels per metre and automatically reduces the scale to keep the ball and wickets visible. It flips both axes: the bowler is on the right, the batter on the left, and positive height points upward. Visual stump offsets show the three overlapping stumps in a side projection; physics uses one stump capsule at the wicket line. Camera zoom never changes physical quantities.

The setup panel and speed stats display **km/h**. Input is divided by 3.6 before entering the simulation; output is multiplied by 3.6 for display. Practice defaults to a Medium bowler with randomized speed (80.1–110 km/h) and length (8–25 m), at a manually set 2.15 m height. The remembered manual speed is 95 km/h. The supported manual range remains 18–180 km/h, equivalent to 5–50 m/s. Tests and diagnostics may use a fixed 115.2 km/h (32 m/s) fixture.

Bowler profiles and seeded sampling live in `Core/Practice/PracticeBowling.cs`. They select delivery settings without changing the aerodynamic model or fixed timestep. Candidate plans pass the same solver as manual deliveries; retries and fallback can change only randomized fields. A physically unreachable manual combination remains an error. `Simulation.BeginPrepared` starts the validated preview plan without solving again, rejects plans generated with different physics settings, and shares initialization with the existing `Begin(DeliverySettings)` entry point. Random seeds belong to pending deliveries, never physics or rendering ticks.

## Geometry

| Quantity | Value |
| --- | --- |
| Wicket separation | Exactly 20.12 m, as specified for this game |
| Stump height | 0.7112 m |
| Stump radius in side-view contact model | 0.019 m |
| Popping crease distance from wicket | 1.2192 m |
| Bowler release x | 1.2192 m |
| Bowler release height limits | 1.53–2.40 m inclusive (153–240 cm) |
| Ball radius | 0.036 m |
| Bat total length from grip | 0.85 m |
| Handle before blade starts | 0.28 m |
| Blade capsule radius | 0.032 m |
| Boundary left of batter's normal stance | 40 m (configurable) |
| Boundary right of batter's normal stance | 75 m (configurable) |
| Boundary wedge width / height | 0.23 m / 0.147 m |
| Clear gap from outer wedge toe to barricade | 5 m |
| Inner barricade height / thickness | 1.5 m / 0.25 m |
| Outer barricade height / thickness | 25 m / 0.25 m; same response as inner barricade |
| Stadium seating | 28 tiers over 21.5 m radial depth, from 1.5 to 25 m high; treads and risers inherit inner-wall response |

Maximum height is the greatest ball-centre world y coordinate above ground during the current delivery. Maximum length is the greatest absolute horizontal distance from the fixed normal stance at x = 19.08 m, including either side and the incoming delivery. Boundary cushions share this stance reference; pitching length continues to use the bowler-end wicket as its origin. Maxima are sampled at release, each physics substep and immediately before/after collision response, including wedge and barricade contacts. They are retained until the next delivery begins.

Boundary, wall and seating dimensions are configured in `Config/field.json`. Swept finite-radius contacts resolve against sloped wedge faces, barricade sides/tops and terrace treads/risers. Normal restitution dissipates energy; tangential friction changes both translation and spin. Slow supported balls slide or roll under gravity, and aerial balls can clear the objects. The barricade inherits the pitch's restitution/friction unless explicitly overridden; terraces inherit the inner barricade's resolved values. Spectators and chairs are scenery. Foam defaults are uncalibrated tuning choices. [STADIUM.md](STADIUM.md) documents the solver and [BOUNDARY_REFERENCES.md](BOUNDARY_REFERENCES.md) records the research.

References: [MCC pitch](https://www.lords.org/mcc/the-laws/the-pitch), [MCC creases](https://www.lords.org/mcc/the-laws/the-creases), [MCC wickets](https://www.lords.org/mcc/the-laws/the-wickets), [MCC ball](https://www.lords.org/mcc/the-laws/the-ball). The geometry does not implement every equipment law. A side view shows the bat's thickness; the bat's out-of-plane face width is not a separate collision axis.

## Configurable baseline

| JSON key | Default | Unit / interpretation |
| --- | --- | --- |
| `BallMass` | 0.156 | kg |
| `BallRadius` | 0.036 | m |
| `Gravity` | 9.80665 | m/s² |
| `AirDensity` | 1.225 | kg/m³; still air |
| `DragCoefficient` | 0.47 | dimensionless constant; not seam-calibrated |
| `MagnusCoefficient` | 0.12 | dimensionless spin-ratio gain; lift capped at ±0.3 |
| `SpinDecay` | 0.025 | 1/s |
| `GroundRestitution` | 0.58 | effective prepared strip normal rebound / incident speed |
| `GroundFriction` | 0.28 | effective prepared strip Coulomb coefficient |
| `RollingResistance` | 0.025 | prepared strip rolling deceleration / gravity |
| `OutfieldRestitution` | 0.30 | outfield normal rebound / incident speed |
| `OutfieldFriction` | 0.35 | outfield Coulomb coefficient |
| `OutfieldRollingResistance` | 0.075 | outfield rolling deceleration / gravity |
| `OutfieldTangentialRetention` | 0.65 | additional tangential speed/spin retention at a substantial outfield impact |
| `BatMass` | 1.17 | kg; published sample mean |
| `BatCentreOfMassFromGrip` | 0.40 | m along the bat from the grip |
| `BatInertiaAboutGrip` | 0.2459 | kg·m²; converted to the assumed grip axis |
| `BatRestitution` | 0.50 | dimensionless |
| `BatFriction` | 0.28 | dimensionless |
| `BatHalfThickness` | 0.032 | m |
| `BatLength` | 0.85 | m |
| `HandleLength` | 0.28 | m |
| `Substeps` | 8 | subdivisions of the fixed 1/120 s tick |

Bat mass and inertia use published measurements with an explicit grip-axis conversion; see [CRICKET_REFERENCES.md](CRICKET_REFERENCES.md) for online sources, calculations and reference checks. The aerodynamic coefficients, pitch response, friction and effective bat restitution remain tuning assumptions. In particular, a hard-plate ball bounce measurement does not calibrate a soil pitch. Configuration is validated at launch; changing it requires restarting the scene. Validation rejects centre-of-mass locations outside the bat and grip inertias that would imply nonpositive centre-of-mass inertia.

## Flight

The released ball advances at the global `GameplaySettings.BowlingSpeed` multiplier until its first positive bat impulse. Main Menu → Settings exposes 0.50×–1.00× in 0.01× steps, defaulting to 0.50×. Only ball travel uses the scaled duration: gravity, drag, spin, rolling and supported surface motion follow the same physical trajectory over proportionally longer gameplay time. Velocity and contact impulses retain their SI values, so displayed speeds and shot response are unchanged. Bat motion, windup, input sampling and `PhaseTime` keep the fixed gameplay timestep. Ground-contact times convert back to gameplay seconds before competing with swept bat/wicket/field contacts. A bat hit restores 1× time for the remainder of that contact substep and all later travel; pitch and wicket contacts retain the selected multiplier. Beginning another delivery clears the existing hit flag and uses the same global preference. No engine time scale or rendering setting changes.

Gravity is constant. Drag is `-0.5 ρ Cd A |v| v`, divided by mass for acceleration. Magnus lift is perpendicular to velocity with a bounded coefficient derived from `ωr/|v|`; it does no instantaneous work. Spin decays exponentially. Seam orientation, turbulence transitions and lateral swing require further models and are not represented.

## Ground and bat

`GroundSurface.At` chooses the prepared strip between x = −0.30 m and x = 20.42 m, matching the visible strip's extension beyond both wickets. Ground outside that interval uses the outfield coefficients. This is the game's 2D contact plane; no pitch-width collision axis is implied. Surface selection is a value-type lookup with no allocations, and rolling uses the material at the start of each physics substep.

Ground normal impulse uses the selected restitution. Tangential impulse removes contact slip `vx + ωr` subject to the selected Coulomb limit. The ball uses solid-sphere inertia `0.4mr²`. The outfield additionally damps horizontal velocity and spin together after friction to represent energy absorbed by grass and soil deformation. A rigid sphere with contact-point friction alone cannot capture this loss. The multiplier is `1 − (1 − OutfieldTangentialRetention) × min(1, |incoming vy| / 5)`. The 5 m/s scale approximates the normal speed of the reference angled impact. This is an explicit effective calibration, not a simulated deforming grass/soil bed; it fades to unity at grazing incidence so tiny hops do not abruptly stop the ball. A multiplier in [0, 1] cannot add kinetic energy and scaling both quantities preserves any no-slip state established by the impulse. The prepared strip has no additional tangential damping.

Below the existing small vertical settling threshold, supported contact continues to remove slip through friction before enforcing pure rolling. Rolling resistance slows pure rolling to rest without reversal or residual spin. Defaults give nominal decelerations of 0.245 m/s² on the strip and 0.735 m/s² on the outfield, plus air drag. All coefficients remain editable in `Config/physics.json`. Walls and terraces keep their existing inheritance from the strip coefficients; boundary cushions retain their independent response.

Bat contact velocity uses `v_contact = v_grip + ω_bat × r_from_grip`. The impulse uses the **centre-of-mass** lever and inertia: `I_com = I_grip - M d²`, which is 0.0587 kg·m² for the defaults. Its denominator includes inverse ball mass, inverse bat mass, and the squared normal COM lever divided by COM inertia. Tangential slip includes the rotation caused by the normal impulse before applying friction; the tangential impulse also changes ball spin. The bat remains animation-driven, so its recoil is not fed back into the arms. Wicket restitution is a simplified passive response in `Simulation`. The visible batter is permanently invulnerable, so body, pad and helmet overlap does not affect the ball's position, velocity or spin.

## Numerical validation

The executable tests use analytical vacuum flight and a separate, finer-step landing integration as independent reference checks. Requested launch combinations must land within 0.002 m in the independent test; live delivery checks allow 0.003 m. These are numerical tolerances, **not claims of real-world predictive accuracy**.

Tests also verify energy loss with random oblique impacts and initial spin, sliding and rolling, contact-point bat impulses, swept impacts that cross the entire blade in one tick, retained segment lengths, repeatable input sequences, and render-frequency independence.

The November 2026 surface checks are described in [CRICKET_REFERENCES.md](CRICKET_REFERENCES.md#ball-bounce-and-outfield-roll). The subsequent [bowling calibration](BOWLING_BOUNCE.md) uses match oblique impacts for the strip: the 36 m/s, 12.5° reference retains 88.94% of total speed and 64.91% of incident angle. A 2 m drop with the default atmosphere now rebounds 0.659 m on the strip (0.363 m with the earlier 0.43 setting) and 0.177 m on the outfield. The strip drop is a model prediction, not a fitted drop measurement. An ideal solid-sphere ramp exit from 1 m, `v = sqrt(10gh/7)`, rolls 9.07 m in 4.93 s on the outfield. This entrance-speed approximation neglects ramp and ramp-to-ground losses. A 75 km/h, 15-degree outfield impact retains 57.1% of horizontal speed. These are model measurements, not measurements on a particular real ground.

Regression checks also cover both pitch edges against an independent work/energy solution, 3,000 impacts with varying initial spin followed by supported contact, grazing impacts, custom/invalid settings, and a stopped ball in the live simulation. Natural turf varies with moisture, compaction, wear, ball construction and impact speed. The model still uses effective constant restitution and friction per material; it does not resolve cracks, individual seam strikes, soil deformation, lateral seam movement or ball ageing. Matching one drop and roll test does not validate every delivery or surface.
