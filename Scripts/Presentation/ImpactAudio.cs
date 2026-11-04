using Cricket.Physics;
using Cricket2D.Presentation;
using Godot;

namespace Cricket2D;

/// <summary>Preloaded samples and a fixed voice pool; no loading or node creation during a hit.</summary>
public partial class ImpactAudio : Node
{
    private ImpactSoundSelector? _selector;
    private readonly Dictionary<string, AudioStream> _streams = [];
    private AudioStreamPlayer[] _voices = [];
    private long[] _started = [];
    private long _sequence, _lastTick = -1;
    private bool _paused;
    internal int PlayedCount { get; private set; }
    internal int VoiceCount => _voices.Length;
    internal int ActiveVoiceCount => _voices.Count(voice => voice.Playing);
    internal string? LastCue { get; private set; }
    internal bool ReadyForPlayback => _selector != null && _voices.Length > 0;
    internal int PausedVoiceCount => _voices.Count(voice => voice.StreamPaused);

    public override void _Ready()
    {
        try
        {
            using var file = Godot.FileAccess.Open("res://Config/audio.json", Godot.FileAccess.ModeFlags.Read);
            if (file == null) throw new InvalidOperationException("Cannot read Config/audio.json.");
            var settings = ImpactAudioSettings.FromJson(file.GetAsText());
            if (!settings.Enabled) return;
            foreach (var cue in settings.Cues.Values)
            foreach (string path in cue.Variants)
            {
                if (_streams.ContainsKey(path)) continue;
                if (!ResourceLoader.Exists(path)) throw new InvalidOperationException($"Missing or unimported audio sample: {path}");
                var stream = GD.Load<AudioStream>(path);
                if (stream == null || stream.GetLength() <= 0) throw new InvalidOperationException($"Empty audio sample: {path}");
                _streams.Add(path, stream);
            }
            _selector = new(settings);
            _voices = new AudioStreamPlayer[settings.MaxVoices];
            _started = new long[settings.MaxVoices];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = new AudioStreamPlayer { Name = $"ImpactVoice{i + 1}", MaxPolyphony = 1 };
                AddChild(_voices[i]);
            }
        }
        catch (Exception ex)
        {
            // Audio is optional: configuration/asset failures must not break a delivery.
            _selector = null;
            GD.PushWarning($"Impact audio disabled: {ex.Message}");
        }
    }

    public void ConsumeContacts(Simulation simulation)
    {
        if (_lastTick == simulation.TickCount) return;
        _lastTick = simulation.TickCount;
        double time = simulation.TickCount * SimulationSettings.Tick;
        foreach (var contact in simulation.Events) PlayContact(contact, time);
    }

    internal void PlayContact(ContactEvent contact, double time)
    {
        if (_paused || _selector == null || !_selector.TrySelect(contact, time, out var playback)) return;
        int selected = 0;
        for (int i = 0; i < _voices.Length; i++)
        {
            if (!_voices[i].Playing) { selected = i; break; }
            if (_started[i] < _started[selected]) selected = i;
        }
        var voice = _voices[selected];
        voice.Stop();
        voice.Stream = _streams[playback.Path];
        voice.VolumeDb = (float)playback.VolumeDb;
        voice.StreamPaused = false;
        voice.Play();
        _started[selected] = ++_sequence;
        LastCue = playback.Cue;
        PlayedCount++;
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        foreach (var voice in _voices) voice.StreamPaused = paused;
    }

    public void Stop()
    {
        foreach (var voice in _voices) voice.Stop();
    }

    public void ResetDelivery()
    {
        Stop();
        _selector?.ResetDelivery();
        _lastTick = -1;
        LastCue = null;
    }
    // Children release their playback on exit; imported streams are owned by Godot's shared cache.
    public override void _ExitTree() => Stop();
}
