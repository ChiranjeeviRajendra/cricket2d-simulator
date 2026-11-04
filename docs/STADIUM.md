# Field boundary, barricades and stadium seating

Edit [`Config/field.json`](../Config/field.json) and restart the game. The same validated configuration supplies the collision geometry and the rendered ovals. Omitted objects/fields get defaults. Unknown keys, null objects and invalid ranges produce a startup error.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `Boundary.LeftDistanceMetres` | 40 | Horizontal distance from the batter's fixed normal stance to the inner toe of the left wedge |
| `Boundary.RightDistanceMetres` | 75 | Same measurement on the right |
| `Boundary.DepthRadiusMetres` | 45 | Oval radius into/out of the screen |
| `Boundary.WidthMetres` | 0.23 | Width of the triangular cross-section, extending outwards from the boundary |
| `Boundary.HeightMetres` | 0.147 | Height of its central ridge above ground |
| `Boundary.Restitution` | 0.20 | Rebound ratio in the direction normal to the contacted foam face; tunable approximation |
| `Boundary.Friction` | 0.40 | Coulomb friction limit, including transfer between translation and spin; tunable approximation |
| `Barricade.ClearGapMetres` | 5 | Clear ground from the wedge's outer toe to the barricade's inner face |
| `Barricade.HeightMetres` | 1.5 | Height of the inner wall and the lowest seating tier |
| `Barricade.ThicknessMetres` | 0.25 | Wall thickness |
| `Barricade.Restitution`, `Barricade.Friction` | null | Inherit `GroundRestitution` and `GroundFriction` from `Config/physics.json`; numbers override them |
| `Seating.WidthMetres` | 21.5 | Radial depth of the terraces between the walls; 12–60 m |
| `Seating.Rows` | 28 | Number of seating tiers; 8–64, with at least 0.6 m depth per row |
| `Seating.Sections` | 16 | Number of radial access aisles and seating blocks; even numbers from 8–32 keep aisles away from the ball plane |
| `Seating.AisleWidthMetres` | 1.8 | Width reserved for each access staircase |
| `Seating.SeatSpacingMetres` | 0.62 | Spacing along a seating row, measured along the oval |
| `Seating.Occupancy` | 0.82 | Fraction of occupied seats; 0 is empty, 1 is full |
| `Seating.CrowdSeed` | 4821 | Repeatable distribution of spectators, empty seats and clothing |
| `OuterBarricade.HeightMetres` | 25 | Top seating tier and outer wall height; must exceed the inner wall height, at most 80 m |
| `OuterBarricade.ThicknessMetres` | 0.25 | Outer wall thickness, extending away from the seating |
| `OuterBarricade.Restitution`, `OuterBarricade.Friction` | null | Inherit the inner barricade's resolved coefficients; numbers override them |

The left/right distances use the normal batting stance at world x = 19.08 m, independent of the moving feet. Default inner wedge toes are x = 59.08 m and −55.92 m. The wall's inner faces are x = 64.31 m and −61.15 m: 5 m clear beyond the 23 cm wedge. Distances remain in metres when the camera zooms. Both old distance signboards are removed.

The barricades and terraces are outward **normal offsets** of the boundary ellipse. Adding the same amount to an ellipse's two radii would not maintain an exact 5 m gap around its curves; the normal-offset construction does. Concrete tiers descend from the 25 m outer wall to the 1.5 m inner wall. Coloured seats, seated spectators, radial access stairs, pale step edges and handrails form the stadium bowl. The first and last tiers meet the corresponding wall tops exactly.

The outer barricade begins at the back of the seating. Its offset is the wedge width + inner-wall gap + inner-wall thickness + seating width: 26.98 m by default. Its inner faces cross the ball plane at x = 86.06 m and −82.90 m. The old parking and verge have been replaced without moving either wall. Editing seating width or inner-wall placement moves the outer wall automatically. The far half of the outer wall is drawn behind the terraces, and its near half in front. The straight camera projection is unchanged.

The old `Parking` JSON object has been replaced by `Seating`; when migrating a custom configuration, use its former `VergeMetres + WidthMetres` as the new seating width to preserve the perimeter. Missing `Seating` receives defaults. Row count, aisle count, spacing and occupancy are bounded to keep the generated layout finite. Crowd placement uses a local integer hash and never consumes gameplay random state.

## Contact behaviour

The existing simulation is a vertical 2D ball plane: the ball can move left/right and up/down. The complete ovals are rendered with depth, and their left/right crossings of that ball plane have physical cross-sections. There is no hidden third-axis ball movement.

Both sloping wedge faces and their rounded *ball-centre* corner paths are swept continuously, using the actual ball radius. Each barricade has inner/outer faces and a top. Terraces have horizontal treads and vertical risers at the same offsets and heights as the rendered seating. A lofted shot that clears the inner wall can rebound off a terrace; sufficiently high shots clear the entire stadium. Terraces inherit the inner wall's resolved restitution/friction and support a resting or rolling ball. Access aisles are placed away from the physical ball plane. Spectators and individual chairs are scenery, with no separate collision bodies. Contact does not end a delivery or award runs automatically.

For surface normal `n`, tangent `t = perpendicular(n)`, ball radius `r`, mass `m` and solid-sphere inertia `I = 0.4*m*r*r`:

```text
vn = dot(velocity, n)
Jn = -(1 + restitution) * m * vn          (only when closing)
slip = dot(velocity, t) - spin * r
Jt = clamp(-slip / (1/m + r*r/I), -friction*Jn, friction*Jn)
velocity += (Jn*n + Jt*t) / m
spin -= r*Jt/I
```

Normal closing speeds below 0.2 m/s use zero restitution to avoid persistent tiny bounces. Supported balls slide/roll down a wedge under tangential gravity, with normal-load-limited friction and rolling resistance. Swept contacts compete with the existing pitch, bat and wicket contacts for the earliest impact in each physics substep. There are separate `Boundary`, `Barricade` and `Terrace` contact events; none marks the wicket as hit. Statistics observe velocity before and after these impulses. Sweep bounds reject distant surfaces before the iterative contact query.

This is a passive, anchored rigid-surface approximation of padding. It does not simulate foam compression, wedge displacement or seam-dependent impact variation. The shape and impact principles have source support; the foam's precise restitution/friction are adjustable choices, not measured coefficients for this particular installation. See [BOUNDARY_REFERENCES.md](BOUNDARY_REFERENCES.md).

## Sky artwork and verification

`Assets/Field/Source/sky.svg` is editable in Inkscape. It contains atmosphere, high wisps and cumulus layers. Inkscape exports the PNG used by the game:

```powershell
& 'C:\path\to\inkscape.com' 'Assets/Field/Source/sky.svg' --export-type=png --export-area-page '--export-filename=Assets/Field/sky.png'
```

`tools/Create-FieldSky.py` regenerates the sky source SVG; running it replaces manual SVG edits. `tools/Run.ps1` imports textures before launch. `tools/Create-StadiumCrowd.py` generates `Assets/Field/Source/crowd.svg`, exported with Inkscape to `Assets/Field/crowd.png` using the same command pattern. The 1024 × 1280 atlas contains 200 seated figures with different clothing, skin tones and front/back/side views. Keep its cell layout and bottom-right white patch intact: the field meshes share this texture for spectators and solid-colour geometry. Source SVGs are excluded from runtime import through `.gdignore`.

World geometry and crowd placement are cached when the field is created. Rendering uses retained background/foreground meshes, with projection and GPU buffer updates only when camera framing changes. Each visible spectator uses one textured quad, sharing the same two draw batches as the terraces. Seats are culled before projection into the mesh; spectators do not require separate nodes, timers, materials or physics bodies. The crowd is static in this pass. Measurements are in [PERFORMANCE.md](PERFORMANCE.md).

Executable checks cover editable distances, normal-offset spacing, collision/render alignment across camera scales, 3,000 passive oblique/spinning impacts, fast swept contacts, both approach directions, aerial clearance, slow settling, solid exclusion and 30/60/144 Hz rendering schedules. `--capture-preview` saves the normal views plus `artifacts/field-overview.png`, which shows the complete layout through a developer camera override.

The seating change adds checks for wall-to-tier alignment, deterministic crowd density, clear aisles, lofted shots, tread bounces, resting support and high-speed riser contacts. Normal and zoomed captures are inspected separately from timed performance runs.
