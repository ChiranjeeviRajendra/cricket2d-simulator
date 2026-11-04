# Impact sounds

Batting practice plays the supplied recordings on physical bat/ball and wicket/ball contacts. A centred, square bat contact uses the perfect-shot sample; off-centre or glancing contact uses the imperfect-shot sample. A wicket strike plays the wicket sample once per delivery. Pitch, boundary and barricade contacts are currently silent.

`Config/audio.json` owns selection and volume settings. Restart the game after editing it. The defaults are sound-design choices: they do not change shot physics, score a shot or automatically select a batting action.

## Samples

| Original recording | Runtime copy | Leading silence removed |
| --- | --- | ---: |
| `Assets/Sounds/perfect-shot-1.m4a` | `Assets/Sounds/Runtime/perfect-shot-1.wav` | 202 ms |
| `Assets/Sounds/imprefect-shot-1.m4a` | `Assets/Sounds/Runtime/imperfect-shot-1.wav` | 149 ms |
| `Assets/Sounds/wicket-out.m4a` | `Assets/Sounds/Runtime/wicket-out.wav` | 86 ms |

The originals are retained, including the original `imprefect` filename. Runtime copies preserve stereo at 48 kHz / 16-bit PCM, with about 5 ms of quiet lead-in before the detected transient. Their combined decoded size is approximately 394 KiB. Looping, normalization and additional lossy compression are disabled in the WAV import settings.

Godot imports WAV, Ogg Vorbis and MP3, so the M4A originals are converted ahead of time. See [Godot audio importing](https://docs.godotengine.org/en/stable/tutorials/assets_pipeline/importing_audio_samples.html). FFmpeg was used as a development tool already available on this PC; it is not a game dependency. For example:

```powershell
& 'C:\path\to\ffmpeg.exe' -nostdin -i 'Assets/Sounds/perfect-shot-1.m4a' `
  -af 'atrim=start=0.202,asetpts=PTS-STARTPTS' -map_metadata -1 `
  -c:a pcm_s16le -ar 48000 'Assets/Sounds/Runtime/perfect-shot-1.wav'
```

Choose a trim offset for each new recording so its impact starts immediately. Keep the natural decay, and turn off looping. The regular launcher imports new audio before starting the game; the Godot editor also imports it automatically.

## Adding variations and shot categories

To add another recording of an existing sound, put an imported WAV/Ogg/MP3 beneath `Assets/Sounds/` and append its `res://` path to that cue's `Variants` array. Variants cycle in order across contacts and deliveries, without affecting any physics random state:

```json
"shot_perfect": {
  "VolumeDb": 0,
  "CooldownSeconds": 0.08,
  "Variants": [
    "res://Assets/Sounds/Runtime/perfect-shot-1.wav",
    "res://Assets/Sounds/Runtime/perfect-shot-2.wav"
  ]
}
```

To add another category, add a named cue and insert a matching rule before a broader fallback. For example, a `soft_defence` cue could use this rule before the current Bat rules:

```json
{ "Contact": "Bat", "Cue": "soft_defence", "MaxImpactSpeed": 5 }
```

The first matching rule owns the contact, including cooldown suppression. Available rule filters:

| Field | Meaning |
| --- | --- |
| `Contact` | `Bat`, `Wicket`, `Pitch`, `Boundary` or `Barricade` |
| `Cue` | Key in the `Cues` object |
| `MinImpactSpeed` / `MaxImpactSpeed` | Relative normal closing speed in m/s; defaults 0 / 200 |
| `MinBladeFraction` / `MaxBladeFraction` | Bat only: fraction along the collidable blade, 0 at the handle end, 1 at the toe; defaults 0 / 1 |
| `MinFaceAlignment` | Bat only: 0–1 alignment between collision normal and the broad-face normal; default 0 |
| `OncePerDelivery` | Suppress later matches of this rule until a new delivery; default false |

The initial perfect-shot rule accepts blade fractions 0.35–0.80 and face alignment at least 0.85. The next rule accepts all other bat contacts. The current two-dimensional capsule cannot identify three-dimensional bat edges or named cricket strokes; categories requiring those measurements would need more contact data from the core.

`MinimumImpactSpeed` silences negligible contacts. Gain rises from `SoftImpactVolumeDb` at that minimum to full volume at `FullVolumeImpactSpeed`. Final gain combines that adjustment, `MasterVolumeDb` and the cue's `VolumeDb`. Cue cooldowns also suppress rapid repeat contacts of the same physical kind, so a collision cannot chatter by switching between perfect/imperfect categories. Bat and wicket cooldowns are independent, allowing a ball deflected onto the stumps to play both sounds.

## Runtime and verification

The physics core reports closing speed, blade fraction and face alignment in `ContactEvent`; it has no audio dependency. `ImpactSoundSelector` applies the JSON rules. `ImpactAudio` consumes the event list immediately after every fixed tick, so contacts from multiple physics ticks within a render frame are not lost. It prevents processing a tick twice.

All configured clips are preloaded on entering batting practice. A fixed pool of four `AudioStreamPlayer` nodes supports overlap and replaces the oldest voice if full. `MaxVoices` is configurable from 1 to 16. Hits do not load files, create nodes or queue unbounded work. Playback is non-positional so camera zoom does not change volume. Missing/unimported samples or invalid configuration produce one warning and disable impact audio while gameplay continues.

Focus loss pauses audio with gameplay; focus return resumes it. Opening settings or the main menu stops active sounds. A new delivery stops playback and resets cooldown/once-per-delivery state. The exit path stops playback and allows 150 ms for the asynchronous mixer cleanup before quitting; this handles the [Godot shutdown playback-reference issue](https://github.com/godotengine/godot/issues/76745).

Verification includes all 50 core checks, with sound-selection tests, configuration rejection, variation cycling, contact suppression and real swing/contact measurements exercising both shot samples and wickets. Native playback checks use `--audio-smoke-test` as a Godot user argument:

```powershell
& 'C:\path\to\Godot_mono_console.exe' --headless --path . -- --audio-smoke-test
```

That check imports no files itself: run the normal launcher or editor once first. It verifies sample loading, native playback, overlapping voices, pause/resume, wicket rearming, stop/reset behavior and clean shutdown. Headless mode verifies engine behavior rather than the user's speaker hardware.

The native check also passed using the normal Windows audio backend. All 50 core checks passed in both local and Release configurations. A short rendered 3840 × 2160 regression run with audio enabled recorded 119.994–120.003 FPS averages across seven scenarios and zero managed collections in the sample windows (`artifacts/performance-audio.json`). This used the existing optimized local game build, with two seconds of warm-up and three seconds sampled per scenario, on the reference RTX 4080 SUPER / Ryzen 7 7800X3D PC. It is a regression check, not an exported-build or prolonged-session performance claim.
