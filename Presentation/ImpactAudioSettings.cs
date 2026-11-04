using System.Text.Json;
using System.Text.Json.Serialization;
using Cricket.Physics;

namespace Cricket2D.Presentation;

public sealed record ImpactAudioSettings
{
    public bool Enabled { get; init; } = true;
    public double MasterVolumeDb { get; init; } = -6;
    public int MaxVoices { get; init; } = 4;
    public double MinimumImpactSpeed { get; init; } = .5;
    public double FullVolumeImpactSpeed { get; init; } = 20;
    public double SoftImpactVolumeDb { get; init; } = -12;
    public Dictionary<string, ImpactSoundCue> Cues { get; init; } = [];
    public ImpactSoundRule[] Rules { get; init; } = [];

    public static ImpactAudioSettings FromJson(string json)
    {
        var options = new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        options.Converters.Add(new JsonStringEnumConverter<ContactKind>(allowIntegerValues: false));
        var settings = JsonSerializer.Deserialize<ImpactAudioSettings>(json, options)
            ?? throw new ArgumentException("Audio configuration cannot be null.");
        settings.Validate();
        return settings;
    }

    public void Validate()
    {
        if (!InRange(MasterVolumeDb, -60, 0) || MaxVoices is < 1 or > 16 ||
            !InRange(MinimumImpactSpeed, 0, 100) || !InRange(FullVolumeImpactSpeed, .01, 200) ||
            FullVolumeImpactSpeed <= MinimumImpactSpeed || !InRange(SoftImpactVolumeDb, -60, 0))
            throw new ArgumentException("Invalid audio volume, voice limit or impact-speed range.");
        if (Cues == null || Rules == null || Cues.Count == 0 || Rules.Length == 0)
            throw new ArgumentException("Audio requires Cues and ordered Rules.");
        foreach (var (id, cue) in Cues)
        {
            if (string.IsNullOrWhiteSpace(id) || cue == null || !InRange(cue.VolumeDb, -60, 6) ||
                !InRange(cue.CooldownSeconds, 0, 5) || cue.Variants == null || cue.Variants.Length is < 1 or > 64)
                throw new ArgumentException($"Invalid audio cue '{id}'.");
            foreach (string path in cue.Variants)
                if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("res://Assets/Sounds/", StringComparison.Ordinal) ||
                    path.Contains("..", StringComparison.Ordinal) || path.Contains('\\') ||
                    Path.GetExtension(path).ToLowerInvariant() is not (".wav" or ".ogg" or ".mp3"))
                    throw new ArgumentException($"Cue '{id}' needs WAV, Ogg or MP3 paths under res://Assets/Sounds/.");
        }
        foreach (var rule in Rules)
        {
            if (rule == null || !Enum.IsDefined(rule.Contact) || string.IsNullOrWhiteSpace(rule.Cue) || !Cues.ContainsKey(rule.Cue) ||
                !InRange(rule.MinImpactSpeed, 0, 200) || !InRange(rule.MaxImpactSpeed, 0, 200) ||
                rule.MinImpactSpeed > rule.MaxImpactSpeed ||
                !InRange(rule.MinBladeFraction, 0, 1) || !InRange(rule.MaxBladeFraction, 0, 1) ||
                rule.MinBladeFraction > rule.MaxBladeFraction || !InRange(rule.MinFaceAlignment, 0, 1) ||
                rule.Contact != ContactKind.Bat && rule.HasBatFilter)
                throw new ArgumentException("Invalid audio rule: check cue, contact kind and impact/blade ranges.");
        }
    }

    private static bool InRange(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;
}

public sealed record ImpactSoundCue
{
    public double VolumeDb { get; init; }
    public double CooldownSeconds { get; init; } = .08;
    public string[] Variants { get; init; } = [];
}

public sealed record ImpactSoundRule
{
    public ContactKind Contact { get; init; }
    public string Cue { get; init; } = "";
    public double MinImpactSpeed { get; init; }
    public double MaxImpactSpeed { get; init; } = 200;
    public double MinBladeFraction { get; init; }
    public double MaxBladeFraction { get; init; } = 1;
    public double MinFaceAlignment { get; init; }
    public bool OncePerDelivery { get; init; }
    [JsonIgnore] public bool HasBatFilter => MinBladeFraction > 0 || MaxBladeFraction < 1 || MinFaceAlignment > 0;
}
