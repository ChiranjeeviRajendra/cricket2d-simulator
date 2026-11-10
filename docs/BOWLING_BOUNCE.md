# Bowling and pitch-bounce calibration

Research and simulation checks: 11 November 2026. Published observations, fitted settings and simulated outputs are distinguished below. This is a representative prepared-pitch model, not a validated recreation of a named ground.

## What the bowler controls

The bowler gives the ball its initial velocity, release position and spin. Bowling a shorter length at the same pace requires a steeper downward release. A larger downward speed at impact produces a larger upward speed after a passive bounce. A higher release also changes the required trajectory. There is no continuing hand force after release and no extra energy added at the pitch.

[Manawadu (2022), *A biomechanical analysis of fast bowling variations*, Table 6.1, printed p. 67](https://drpaulfelton.com/wp-content/uploads/2023/08/2023-manawadu-a-biomechanical-analysis-of-fast-bowling-variations.pdf) provides the following selected group measurements from county fast bowlers:

| Intended delivery | Release speed, m/s | Downward release angle | Vertical release speed magnitude, m/s |
| --- | ---: | ---: | ---: |
| Stock | 32.1 ± 1.9 | 5.4 ± 1.2° | 3.0 ± 0.8 |
| Bouncer | 33.1 ± 2.2 | 13.0 ± 2.7° | 7.4 ± 1.6 |

Values are means ± SD, not universal delivery presets. Wrist, hand and trunk coordination change release conditions. Bouncers were released lower relative to standing height in this sample; greater bounce does not require the same bowler to release every bouncer higher. The [associated 707-trial, 21-bowler conference paper](https://commons.nmu.edu/isbs/vol40/iss1/99/) reports significant differences in release angle across delivery types.

[Cork, Justham and West (2013), *Three-dimensional vision analysis to measure the release characteristics of elite bowlers in cricket*](https://journals.sagepub.com/doi/10.1177/1754337112447264) measured release heights of **1.97–2.38 m**, speeds of **17.5–35.0 m/s**, and angles from **9.7° upward to 19.2° downward** across their sample. These ranges include different bowling styles and are not maximum human capabilities. The game's upper release-height limit is now **2.40 m**, allowing the reported heights; the existing 1.53 m lower limit and 2.15 m default remain. Release height means ball-centre height, not the bowler's standing height.

## Measurements used for pitch response

[James, Carré and Haake (2004), *The playing performance of county cricket pitches*, pp. 5–8 and Table 2](https://www.researchgate.net/publication/225959438_The_playing_performance_of_county_cricket_pitches) analysed roughly 3,000 match impacts. Their seam-bowling reference is **36 m/s at 12.5° to horizontal at impact**, not release. Reported pitch pace spans **87.10–90.15%** of total speed retained; bounce spans **52.4–72.7%** of incident angle. Selected pitch-session observations:

| Venue / date | Total speed retained | Rebound angle / incident angle |
| --- | ---: | ---: |
| Headingley, 28 July 2000 | 89.41% | 52.4% |
| Trent Bridge, 10 August 2001 | 90.14% | 65.8% |
| Chelmsford, 3 August 2001 | 87.86% | 72.7% |

These are session summaries with variation between balls. **Angle ratio is not the coefficient of restitution**, and speed retention is not horizontal-speed retention.

[James, Carré and Haake (2005), *Predicting the playing character of cricket pitches*](https://www.researchgate.net/publication/225873716_Predicting_the_playing_character_of_cricket_pitches) found that friction, restitution and deformation must be considered together. Vertical drop measurements alone did not reliably predict oblique bounce. Their simplified Newtonian model was more successful for pace than for bounce variation.

The earlier `GroundRestitution = 0.43` was based on a particular leather-ball vertical-drop study, recorded in [CRICKET_REFERENCES.md](CRICKET_REFERENCES.md#ball-bounce-and-outfield-roll). That remains a usable softer-surface setting, but was too low for the selected match-impact reference. A matching drop test cannot establish realistic seam-delivery behavior.

## Calibration and implementation

For a representative target we choose total speed retention `p = 0.89` and angle ratio `b = 0.65`, at incident angle `a = 12.5°`. These are our mid-range choices. For a sliding, initially unspun sphere on a flat plane:

```text
incoming velocity = (u cos(a), -u sin(a))
target outgoing velocity = (p u cos(b a), p u sin(b a))
effective restitution e = p sin(b a) / sin(a)
effective friction mu = [cos(a) - p cos(b a)] / [(1 + e) sin(a)]
```

Rounding these derived coefficients gives **`GroundRestitution = 0.58`** and **`GroundFriction = 0.28`**. The existing impulse solver then produces **88.94% total speed retention** and **64.91% angle ratio** at the reference impact. This is a fit to aggregate observations, not two independently measured material constants. The numerical similarity to a published steel-plate restitution is coincidental; that measurement is not the basis for this calibration.

The existing delivery solver already finds the lower trajectory using release speed, height, target length, gravity and drag. The collision computes `outgoing vy = -e * incoming vy`; friction changes horizontal speed and spin. No length multiplier, speed multiplier or invented upward impulse is necessary. A constant passive restitution below one cannot create normal kinetic energy, and the bounded friction impulse dissipates slip energy.

Runtime changes are two coefficient values and the release-height bound. There are no additional physics iterations, allocations, resources, nodes or per-frame calculations. Fixed simulation timing and presentation settings are unchanged. Existing field configuration deliberately inherits the pitch coefficients for barricades and terraces when their overrides are null, so those surfaces also inherit the new response. Explicit overrides remain available in `Config/field.json`.

## Measured simulator outputs

The following are **simulated**, not recorded match deliveries. Conditions: default atmosphere and ball, zero release spin, no bat interaction, first bounce on the prepared strip, ball-centre height above ground. Pitch x is measured from the **bowler-end wicket**; batter reference x is **19.08 m**. A smaller pitch x is a shorter delivery. Peak is the first post-bounce apex of an unobstructed trajectory, which may occur beyond the batter.

| Release km/h | Pitch x, m | Release height, m | Previous height at batter, m | Updated height at batter, m | Updated peak, m |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 140 | 18 | 2.15 | 0.125 | 0.160 | 0.698 |
| 140 | 16 | 2.15 | 0.271 | 0.377 | 0.772 |
| 140 | 14 | 2.15 | 0.409 | 0.594 | 0.892 |
| 140 | 12 | 2.15 | 0.565 | 0.850 | 1.093 |
| 140 | 10 | 2.15 | 0.784 | 1.212 | 1.444 |
| 140 | 8 | 2.15 | 1.163 | 1.829 | 2.117 |
| 110 | 10 | 2.15 | 0.623 | 1.068 | 1.089 |
| 150 | 10 | 2.15 | 0.817 | 1.241 | 1.576 |
| 140 | 10 | 1.80 | 0.618 | 0.971 | 1.091 |
| 140 | 10 | 2.38 | 0.895 | 1.375 | 1.697 |

Higher peak bounce and greater height at the batter are different quantities. In the pace-delivery examples, shortening the length or increasing release height raises both. Greater speed raises the peak, but a full delivery has less time to rise before reaching the batter and can arrive lower. Extremely short, slow balls can already be descending or bounce again. These qualifications follow from flight time and gravity; an unconditional “shorter/faster always higher at the batter” rule would be inaccurate.

## Validation and remaining limits

Run `dotnet run --project Tests/Cricket.Physics.Tests.csproj -c Release`. The executable prints the table above and checks the match-impact envelope, independent analytical vacuum trajectories, separate speed/height/length trends, and live bounce at 1, 8 and 16 substeps per 120 Hz tick. Existing tests cover passive energy loss with spin, drops, rolling, bat contacts, deterministic rendering-rate independence and steady-state allocations. All 65 checks passed after calibration.

The default 2 m vertical drop now rebounds 0.659 m on the strip, a prediction of the effective constant model rather than a new drop measurement. Outfield tests still yield 0.177 m vertical rebound, 57.1% horizontal retention at their angled reference, and 9.07 m roll in the ramp-equivalent test.

Debug and ExportRelease builds passed with zero warnings/errors. Godot's headless smoke suite passed, including the expanded height slider, delivery preparation, pause/resume, reset and batting controls. Animation-preview playback checks passed; this checkout has no implemented ball-replay system to validate seeking or reverse ball playback.

The existing `artifacts/Run-ReleasePracticeBenchmark.ps1` measured ExportRelease assemblies in the local Godot 4.7.2 .NET runner, with a confirmed 3840 × 2160 render target, RTX 4080 SUPER, GL Compatibility, VSync enabled and `MaxFps = 0`. Each of eight scenarios had two seconds of warm-up and three seconds of sampling. Averages were 119.998–120.001 FPS; frame-time p99 ranged 8.381–8.474 ms. Live delivery measured 118.18 FPS at the 1% low, physics p95 1.95 ms, 295 managed bytes/frame across the application and zero managed collections. The settings-drag scenario recorded one collection in each generation; random practice had a 104.01 FPS 1% low despite its near-120 average. Working set ranged 268–289 MiB. Raw results are in `artifacts/performance-practice.json`. This is a short measurement of the resulting build, not a before/after performance comparison or a long-session guarantee.

Pitch moisture, compaction, wear, local cracks, seam strikes and ball construction cause variation. This change does not model that variation, a deforming pitch, lateral movement, or style-specific release spin. Drag and Magnus remain approximations. Extrapolation from stock seam impacts to very steep bouncers and slow bowling requires further paired release/impact/rebound measurements. No 4K/120 FPS claim follows from physics tests, and no packaged export is configured in this checkout.

## Changed files

- `Config/physics.json`, `Core/SimulationSettings.cs`: calibrated strip coefficients.
- `Core/Physics/DeliverySolver.cs`: expanded release-height bound.
- `Tests/DeliveryBounceChecks.cs` (new), `Tests/Program.cs`, `Tests/BallSurfaceChecks.cs`, `Tests/Cricket.Physics.Tests.csproj`: reference, trajectory, configuration and drop checks.
- `Scripts/Diagnostics/GameController.PracticeChecks.cs`: exercises the new slider endpoint.
- `docs/BOWLING_BOUNCE.md` (new), `docs/PHYSICS.md`, `docs/CRICKET_REFERENCES.md`, `README.md`: research, data, assumptions and updated controls.

No files were removed.
