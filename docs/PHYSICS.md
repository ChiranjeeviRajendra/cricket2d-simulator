# Physics assumptions and units

All core distances are metres, time is seconds, mass is kilograms, velocity is m/s, angular velocity is rad/s, forces are newtons and impulse is N·s. The world origin is the bowler-end wicket at pitch level. The drawing layer starts at approximately 63.22 pixels per metre and automatically reduces the scale to keep the ball and wickets visible. It flips both axes: the bowler is on the right, the batter on the left, and positive height points upward. Visual stump offsets show the three overlapping stumps in a side projection; physics uses one stump capsule at the wicket line. Camera zoom never changes physical quantities.

The setup panel and speed stats display **km/h**. Input is divided by 3.6 before entering the simulation; output is multiplied by 3.6 for display. The default release is 115.2 km/h, equivalent to 32 m/s. The supported range is 18–180 km/h, equivalent to 5–50 m/s.

## Geometry

| Quantity | Value |
| --- | --- |
| Wicket separation | Exactly 20.12 m, as specified for this game |
| Stump height | 0.7112 m |
| Stump radius in side-view contact model | 0.019 m |
| Popping crease distance from wicket | 1.2192 m |
| Bowler release x | 1.2192 m |
| Bowler release height limits | 1.53–2.16 m inclusive (153–216 cm) |
| Ball radius | 0.036 m |
| Bat total length from grip | 0.85 m |
| Handle before blade starts | 0.28 m |
| Blade capsule radius | 0.032 m |
| Boundary left of batter's normal stance | 37 m |
| Boundary right of batter's normal stance | 75 m |

Maximum height is the greatest ball-centre world y coordinate above ground during the current delivery. Maximum length is the greatest absolute horizontal distance from the fixed normal stance at x = 19.08 m, including either side and the incoming delivery. Boundary signs share this stance reference; pitching length continues to use the bowler-end wicket as its origin. Maxima are sampled at release, each physics substep and immediately before/after collision response. They are retained until the next delivery begins.

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
| `GroundRestitution` | 0.58 | dimensionless |
| `GroundFriction` | 0.22 | dimensionless Coulomb coefficient |
| `RollingResistance` | 0.025 | dimensionless deceleration coefficient |
| `BatMass` | 1.15 | kg |
| `BatInertiaAboutGrip` | 0.26 | kg·m²; effective driven-bat approximation |
| `BatRestitution` | 0.50 | dimensionless |
| `BatFriction` | 0.28 | dimensionless |
| `BatHalfThickness` | 0.032 | m |
| `BatLength` | 0.85 | m |
| `HandleLength` | 0.28 | m |
| `Substeps` | 8 | subdivisions of the fixed 1/120 s tick |

The aerodynamic and contact coefficients are starting assumptions, not measurements of a particular ball, bat or ground. Configuration is loaded and validated at launch. Changing it requires restarting the scene.

## Flight

Gravity is constant. Drag is `-0.5 ρ Cd A |v| v`, divided by mass for acceleration. Magnus lift is perpendicular to velocity with a bounded coefficient derived from `ωr/|v|`; it does no instantaneous work. Spin decays exponentially. Seam orientation, turbulence transitions and lateral swing require further models and are not represented.

## Ground and bat

Ground normal impulse uses restitution. Tangential impulse removes contact slip `vx + ωr` subject to the Coulomb limit. The ball uses solid-sphere inertia `0.4mr²`. Below the small vertical settling threshold, the supported-contact model continues to remove slip through friction before enforcing pure rolling. Rolling resistance slows a rolling ball to rest without changing its direction or introducing energy.

Bat contact uses `v_contact = v_grip + ω_bat × r`. The impulse denominator includes inverse ball mass, inverse bat mass, and the squared normal lever divided by bat inertia. Tangential impulse is limited by bat friction and changes ball spin. The bat remains animation-driven; there is no fully coupled bat-human recoil system. Wicket restitution is a simplified passive response in `Simulation`. The visible batter is permanently invulnerable, so body, pad and helmet overlap does not affect the ball's position, velocity or spin.

## Numerical validation

The executable tests use analytical vacuum flight and a separate, finer-step landing integration as independent reference checks. Requested launch combinations must land within 0.002 m in the independent test; live delivery checks allow 0.003 m. These are numerical tolerances, **not claims of real-world predictive accuracy**.

Tests also verify energy loss with random oblique impacts and initial spin, sliding and rolling, contact-point bat impulses, swept impacts that cross the entire blade in one tick, retained segment lengths, repeatable input sequences, and render-frequency independence. Realistic tuning still requires observed incoming speeds, pitch contact positions, rebound velocities and bat motion from measured footage.
