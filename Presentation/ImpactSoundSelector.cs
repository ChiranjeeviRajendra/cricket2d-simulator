using Cricket.Physics;

namespace Cricket2D.Presentation;

public readonly record struct ImpactPlayback(string Cue, string Path, double VolumeDb);

/// <summary>Ordered, configurable impact rules; independent of the audio engine and physics RNG.</summary>
public sealed class ImpactSoundSelector
{
    private sealed class CueState(ImpactSoundCue cue)
    {
        public readonly ImpactSoundCue Cue = cue;
        public int NextVariant;
        public double LastTime = double.NegativeInfinity;
    }
    private readonly ImpactAudioSettings _settings;
    private readonly Dictionary<string, CueState> _cues;
    private readonly bool[] _playedRules;
    private readonly double[] _contactReadyAt = new double[Enum.GetValues<ContactKind>().Length];

    public ImpactSoundSelector(ImpactAudioSettings settings)
    {
        settings.Validate();
        _settings = settings;
        _cues = settings.Cues.ToDictionary(pair => pair.Key, pair => new CueState(pair.Value));
        _playedRules = new bool[settings.Rules.Length];
        ResetDelivery();
    }

    public void ResetDelivery()
    {
        Array.Clear(_playedRules);
        Array.Fill(_contactReadyAt, double.NegativeInfinity);
        foreach (var cue in _cues.Values) cue.LastTime = double.NegativeInfinity;
        // Keep variant rotation across deliveries so adding samples produces variety immediately.
    }

    public bool TrySelect(ContactEvent contact, double time, out ImpactPlayback playback)
    {
        playback = default;
        if (!_settings.Enabled || !double.IsFinite(time) || time < 0 || !Enum.IsDefined(contact.Kind) ||
            !double.IsFinite(contact.ImpactSpeed) || contact.ImpactSpeed <= _settings.MinimumImpactSpeed) return false;
        for (int i = 0; i < _settings.Rules.Length; i++)
        {
            var rule = _settings.Rules[i];
            if (rule.Contact != contact.Kind || contact.ImpactSpeed < rule.MinImpactSpeed || contact.ImpactSpeed > rule.MaxImpactSpeed) continue;
            if (rule.HasBatFilter &&
                (contact.BatBladeFraction is not double fraction || !double.IsFinite(fraction) ||
                 fraction < rule.MinBladeFraction || fraction > rule.MaxBladeFraction ||
                 contact.BatFaceAlignment is not double alignment || !double.IsFinite(alignment) || alignment < rule.MinFaceAlignment)) continue;
            var state = _cues[rule.Cue];
            // First match owns the contact, including suppression; never fall through to another shot sound.
            if (rule.OncePerDelivery && _playedRules[i] || time - state.LastTime < state.Cue.CooldownSeconds ||
                time < _contactReadyAt[(int)contact.Kind]) return false;
            double strength = Math.Clamp((contact.ImpactSpeed - _settings.MinimumImpactSpeed) /
                (_settings.FullVolumeImpactSpeed - _settings.MinimumImpactSpeed), 0, 1);
            double volume = _settings.MasterVolumeDb + state.Cue.VolumeDb + _settings.SoftImpactVolumeDb * (1 - strength);
            playback = new(rule.Cue, state.Cue.Variants[state.NextVariant], volume);
            state.NextVariant = (state.NextVariant + 1) % state.Cue.Variants.Length;
            state.LastTime = time;
            _contactReadyAt[(int)contact.Kind] = time + state.Cue.CooldownSeconds;
            _playedRules[i] = true;
            return true;
        }
        return false;
    }
}
