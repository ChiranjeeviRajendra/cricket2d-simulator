# Field boundary, barricade and parking

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
| `Barricade.HeightMetres` | 2 | Height of the solid perimeter wall |
| `Barricade.ThicknessMetres` | 0.25 | Wall thickness |
| `Barricade.Restitution`, `Barricade.Friction` | null | Inherit `GroundRestitution` and `GroundFriction` from `Config/physics.json`; numbers override them |
| `Parking.VergeMetres` | 1.5 | Grass verge outside the wall |
| `Parking.WidthMetres` | 20 | Width of the surrounding asphalt parking ring |

The left/right distances use the normal batting stance at world x = 19.08 m, independent of the moving feet. Default inner wedge toes are x = 59.08 m and −55.92 m. The wall's inner faces are x = 64.31 m and −61.15 m: 5 m clear beyond the 23 cm wedge. Distances remain in metres when the camera zooms. Both old distance signboards are removed.

The barricade and parking are outward **normal offsets** of the boundary ellipse. Adding the same amount to an ellipse's two radii would not maintain an exact 5 m gap around its curves; the normal-offset construction does. Parking includes two rows of bays, cars, a marked central driving lane and kerbs. Cars and painted parking details are scenery.

## Contact behaviour

The existing simulation is a vertical 2D ball plane: the ball can move left/right and up/down. The complete ovals are rendered with depth, and their left/right crossings of that ball plane have physical cross-sections. There is no hidden third-axis ball movement.

Both sloping wedge faces and their rounded *ball-centre* corner paths are swept continuously, using the actual ball radius. The barricade has inner/outer faces and a top. A low ball can pop upward, lose speed and change spin on a wedge; a ball can clear either object if it is high enough. A wall-face impact rebounds according to the same normal-restitution and friction principles as the pitch. Contacts from outside the oval work too. Contact does not end a delivery or award runs automatically.

For surface normal `n`, tangent `t = perpendicular(n)`, ball radius `r`, mass `m` and solid-sphere inertia `I = 0.4*m*r*r`:

```text
vn = dot(velocity, n)
Jn = -(1 + restitution) * m * vn          (only when closing)
slip = dot(velocity, t) - spin * r
Jt = clamp(-slip / (1/m + r*r/I), -friction*Jn, friction*Jn)
velocity += (Jn*n + Jt*t) / m
spin -= r*Jt/I
```

Normal closing speeds below 0.2 m/s use zero restitution to avoid persistent tiny bounces. Supported balls slide/roll down a wedge under tangential gravity, with normal-load-limited friction and rolling resistance. Swept contacts compete with the existing pitch, bat and wicket contacts for the earliest impact in each physics substep. There are separate `Boundary` and `Barricade` contact events; neither marks the wicket as hit. Statistics observe velocity before and after these impulses.

This is a passive, anchored rigid-surface approximation of padding. It does not simulate foam compression, wedge displacement or seam-dependent impact variation. The shape and impact principles have source support; the foam's precise restitution/friction are adjustable choices, not measured coefficients for this particular installation. See [BOUNDARY_REFERENCES.md](BOUNDARY_REFERENCES.md).

## Sky artwork and verification

`Assets/Field/Source/sky.svg` is editable in Inkscape. It contains atmosphere, high wisps and cumulus layers. Inkscape exports the PNG used by the game:

```powershell
& 'C:\path\to\inkscape.com' 'Assets/Field/Source/sky.svg' --export-type=png --export-area-page '--export-filename=Assets/Field/sky.png'
```

`tools/Create-FieldSky.py` regenerates the source SVG; running it replaces manual SVG edits. `tools/Run.ps1` imports the texture before launch. Geometry and parked cars are drawn directly from the configured dimensions rather than baked into a fixed background.

Executable checks cover editable distances, normal-offset spacing, collision/render alignment across camera scales, 3,000 passive oblique/spinning impacts, fast swept contacts, both approach directions, aerial clearance, slow settling, solid exclusion and 30/60/144 Hz rendering schedules. `--capture-preview` saves the normal views plus `artifacts/field-overview.png`, which shows the complete layout through a developer camera override.
