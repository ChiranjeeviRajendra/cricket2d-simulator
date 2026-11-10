# Cricket measurements used by the simulator

Research checked on 3 November 2026. These references inform the defaults and automated reference checks. They do not make this 2D, controller-driven rig a complete model of a human batter. Each value below distinguishes a published measurement from a conversion or tuning choice.

## Bat mass and rotational inertia

[Curtis, Heller and Senior (2021), *Methods for estimating moment of inertia of cricket bats*, Table 1](https://link.springer.com/content/pdf/10.1007/s12283-021-00349-y.pdf) reports sample means of **1.170 kg** bat mass, **0.500 m** centre-of-mass distance from the handle end, and **0.3512 kg m²** inertia about that end. The study discusses hand-axis positions 7.5–15 cm from the handle end.

We choose a grip 0.100 m from the handle end and apply the parallel-axis theorem to those mean properties:

```text
M = 1.170 kg
I_com = I_handle_end - M * 0.500² = 0.0587 kg m²
d_grip_to_com = 0.500 - 0.100 = 0.400 m
I_grip = I_com + M * 0.400² = 0.2459 kg m²
```

Thus `Config/physics.json` uses `BatMass = 1.17`, `BatCentreOfMassFromGrip = 0.4` and `BatInertiaAboutGrip = 0.2459`. The grip placement is our assumption; these are conversions from sample means, not measurements of one particular bat. The existing 0.85 m grip-to-tip collision geometry remains a game approximation and is not the study's measured bat length.

## Forward speed reference

[McErlain-Naylor et al. (2021), *Comparing power hitting kinematics between skilled male and female cricket batters*, Table 1 and methods](https://www.stuartmcnaylor.com/publication/male_female_batting/McErlain-Naylor_et_al_2021.pdf) reports maximum distal bat-tip speeds of **28.4 ± 2.5 m/s** for the male sample and **22.6 ± 2.3 m/s** for the female sample. These were power-hitting trials, not universal limits for every stroke. The measured point matters: tip speed is different from grip speed or the speed of the impact point.

The rebuilt batsman uses authored hand travel and wrist release rather than a straight arm rotating around the shoulder. Its 600°/s forward ceiling describes progress through the 240° input arc, not human shoulder angular speed. The older 29.5 m/s tip-speed calibration no longer applies. Actual contact velocity is calculated from the moving physical bat; measured power-hitting means remain context rather than a requirement that every input produce the same speed.

## Impact response and rebound

[Rod Cross, University of Sydney, *Physics of Cricket*, sections 3–5](https://www.physics.usyd.edu.au/~cross/cricket.html) describes stationary-bat rebound-speed ratios around **0.1 near the tip to 0.3 around the middle**, and explains that impact-point velocity affects the outgoing ball. That rebound ratio is not the same quantity as the local relative-velocity coefficient of restitution.

`BatRestitution = 0.50` remains an **effective tuning coefficient**, not a directly measured universal bat/ball constant. With the updated mass distribution, normal stationary impacts 0.55 m and 0.70 m from the grip give approximate rebound ratios of 0.26 and 0.09. These are broad reference checks, not a complete measured rebound curve.

Cross's roughly 0.58 ball restitution example concerns a **hard steel plate**. It does not establish the same restitution for a prepared soil pitch. Separate strip and outfield responses were introduced on 9 November 2026. The strip was subsequently calibrated against match oblique impacts on 11 November; see [BOWLING_BOUNCE.md](BOWLING_BOUNCE.md) for the independent derivation of its effective 0.58 restitution and 0.28 friction. Aerodynamic coefficients remain simplified effective values.

## Ball bounce and outfield roll

Research checked on 9 November 2026. [Mondal et al. (2025), *A Study on Restitution of Different Cricket Balls on Different Types of Pitches*, Table 1](https://www.hrpub.org/download/20251130/SAJ7-19942607.pdf) reports SG leather-ball restitution on turf of 0.404, 0.439, 0.429 and 0.430 over its four drop heights (mean 0.4255). The earlier **0.43** strip setting represented those drops; it was superseded by the match-impact calibration in [BOWLING_BOUNCE.md](BOWLING_BOUNCE.md). It remains an optional softer-surface setting, not a universal high-speed oblique-impact value. [Howard's Cambridge experimental project (2012)](https://www-geo.eng.cam.ac.uk/system/files/documents/howard.pdf) independently demonstrates the influence of soil moisture and cracking on rebound; its laboratory pitches approached restitution around 0.4 before cracking reduced it.

The [AFL / Cricket Australia *Handbook of Testing for Synthetic Turf* (March 2018), sections 6.2, 6.3 and 7.9](https://resources.afl.com.au/afl/document/2020/07/02/c6fc0db5-e1e5-4a27-8512-d1f00f6104f7/2B_AFL_CA_Testing_Manual_-_Mar_2018_f__AFL_CA_Synthetic_Field_Standards_.pdf) provides reproducible cricket-ball reference envelopes: **0.1–0.4 m** rebound from a **2 m** drop, **35–60%** horizontal speed retention for a **75 km/h, 15-degree** impact, and **3.5–15 m** roll following release down a ramp from **1 m**. These describe synthetic outfield testing; we use them as an engineering cross-check, not as a claim that natural grass must satisfy the same standard.

The outfield defaults (`OutfieldRestitution = 0.30`, `OutfieldFriction = 0.35`, `OutfieldRollingResistance = 0.075`, `OutfieldTangentialRetention = 0.65`) are **our effective calibration**, not coefficients reported by that handbook. Additional tangential damping represents deformation losses absent from rigid-sphere friction, and fades out for gentle contacts. The rolling coefficient describes net deceleration after friction enforces the rolling constraint. [PHYSICS.md](PHYSICS.md) records the model equations, measured before/after results and remaining limits.

The flight audit also consulted [Sayers and Lelimo (2007), *Aerodynamic Coefficients of Stationary and Spinning Cricket Balls*](https://www.scielo.org.za/scielo.php?pid=S2309-89882007000100007&script=sci_arttext). Its seam- and speed-dependent results do not justify pretending that one constant drag/lift model covers every cricket ball. This change targets bounce and rolling; the existing quadratic drag, planar Magnus approximation and release solver remain in place. Lateral swing and seam deviation require an out-of-plane model.

## Bat impact model

[James, Curtis, Allen and Rippin (2012), *The validity of a rigid body model of a cricket ball-bat impact*](https://www.sciencedirect.com/science/article/pii/S1877705812017298) found useful agreement near the bat centre of mass, with larger discrepancies away from it. We retain a rigid-body impact approximation; bat vibrations, ball deformation and the location-dependent material response are not explicitly simulated.

## Momentum calculation

The velocity of the physical impact point still comes from the moving grip and the bat's angular velocity:

```text
v_contact = v_grip + omega_bat × r_from_grip
```

For the collision impulse, both the lever and inertia now refer to the **bat centre of mass**:

```text
r = r_from_grip - bat_direction * BatCentreOfMassFromGrip
I_com = BatInertiaAboutGrip - BatMass * BatCentreOfMassFromGrip²
K_n = 1 / ball_mass + 1 / bat_mass + cross(r, normal)² / I_com
J_n = -(1 + restitution) * relative_normal_velocity / K_n
```

The earlier calculation mixed translation at the centre of mass with rotation about the grip. Correcting that reference makes the finite-mass impulse internally consistent. Tangential slip includes the rotation caused by the normal impulse before applying friction. Tests reconstruct the equal-and-opposite bat response to verify linear momentum, angular momentum, relative normal restitution and total energy loss for isolated impacts, including 1,000 oblique frictional cases.

During gameplay, the bat remains driven by the requested pose: the calculated recoil is not fed back into the player's arms. The controller/muscles act as an external driver. The game therefore does **not** claim conservation of the entire batter/ball system or full human recoil dynamics.

## Deliberate control choices

The bounded 8-to-12 input arc, 600°/s forward and 300°/s return ceilings, ±55° bottom-hand adjustment, proportional trigger transfer and deep full-R2 kneel are gameplay choices. The cited studies do not establish these as universal human limits. The prior full-circle snap has been removed. [BATTING.md](BATTING.md#research-behind-the-motion) records the added whole-body research.

See [BATTING.md](BATTING.md) for every control parameter and [PHYSICS.md](PHYSICS.md) for SI units, numerical assumptions and the remaining coefficients.
