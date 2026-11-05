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

Cross's roughly 0.58 ball restitution example concerns a **hard steel plate**. It does not establish the same restitution for a prepared soil pitch. `GroundRestitution = 0.58` therefore remains an uncalibrated pitch assumption, along with friction, rolling resistance and the constant aerodynamic coefficients.

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
