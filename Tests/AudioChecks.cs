using System.Text.Json;
using Cricket.Physics;
using Cricket2D.Presentation;

internal static class AudioChecks
{
    private static ImpactAudioSettings Settings() => ImpactAudioSettings.FromJson(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Config", "audio.json")));
    private static readonly ContactEvent Perfect = new(ContactKind.Bat, default, 30, 40, 25, .55, 1);
    private static readonly ContactEvent Imperfect = Perfect with { BatBladeFraction = .05 };
    private static readonly ContactEvent Wicket = new(ContactKind.Wicket, default, 25, 8, 25);

    public static void Selection()
    {
        var settings = Settings();
        var selector = new ImpactSoundSelector(settings);
        Require(selector.TrySelect(Perfect, 0, out var shot) && shot.Cue == "shot_perfect", "Middle/face hit must use perfect sample.");
        Require(!selector.TrySelect(Perfect, 0, out _), "Duplicate contact played twice.");
        Require(!selector.TrySelect(Imperfect, .02, out _), "Changing shot classification bypassed contact cooldown.");
        Require(selector.TrySelect(Imperfect, .2, out shot) && shot.Cue == "shot_imperfect", "Toe/shoulder hit must use imperfect sample.");
        Require(selector.TrySelect(Perfect with { BatFaceAlignment = .2 }, .4, out shot) && shot.Cue == "shot_imperfect", "Glancing contact must use imperfect sample.");
        Require(selector.TrySelect(Wicket, .4, out shot) && shot.Cue == "wicket", "Bat cooldown must not suppress a wicket.");
        Require(!selector.TrySelect(Wicket, 2, out _), "Wicket repeated within the same delivery.");
        Require(!selector.TrySelect(Perfect with { ImpactSpeed = .1 }, 3, out _), "Resting contact should be silent.");
        Require(!selector.TrySelect(Perfect with { Kind = ContactKind.Pitch }, 3, out _), "Unconfigured pitch contact made a shot sound.");
        selector.ResetDelivery();
        Require(selector.TrySelect(Wicket, 0, out _), "New delivery did not rearm wicket.");
        Require(selector.TrySelect(Perfect with { ImpactSpeed = 2 }, 0, out var soft), "Soft contact missing.");
        Require(selector.TrySelect(Perfect, .2, out var loud) && loud.VolumeDb > soft.VolumeDb, "Impact strength must affect gain.");
        Require(!new ImpactSoundSelector(settings with { Enabled = false }).TrySelect(Perfect, 0, out _), "Disabled audio played.");

        var variants = new Dictionary<string, ImpactSoundCue>(settings.Cues);
        string first = variants["shot_perfect"].Variants[0];
        const string second = "res://Assets/Sounds/Runtime/future-perfect.wav";
        variants["shot_perfect"] = variants["shot_perfect"] with { Variants = [first, second] };
        selector = new(settings with { Cues = variants });
        Require(selector.TrySelect(Perfect, 0, out shot) && shot.Path == first, "First variation missing.");
        selector.ResetDelivery();
        Require(selector.TrySelect(Perfect, 0, out shot) && shot.Path == second, "Variants must rotate across deliveries.");
        Require(selector.TrySelect(Perfect, 1, out shot) && shot.Path == first, "Variant cycle did not wrap.");

        // Adding a rule and cue changes selection without adding another switch to the player.
        variants["soft_defence"] = new() { Variants = [second] };
        selector = new(settings with { Cues = variants, Rules = [
            new() { Contact = ContactKind.Bat, Cue = "soft_defence", MaxImpactSpeed = 5 }, ..settings.Rules] });
        Require(selector.TrySelect(Perfect with { ImpactSpeed = 2 }, 0, out shot) && shot.Cue == "soft_defence", "Custom ordered rule ignored.");
    }

    public static void Configuration()
    {
        var settings = Settings();
        foreach (var bad in new[] {
            settings with { MaxVoices = 0 }, settings with { MaxVoices = 500 }, settings with { MasterVolumeDb = double.NaN },
            settings with { FullVolumeImpactSpeed = .1 }, settings with { Rules = [new() { Cue = "missing" }] },
            settings with { Rules = [new() { Contact = ContactKind.Bat, Cue = "shot_perfect", MinBladeFraction = .9, MaxBladeFraction = .1 }] },
            settings with { Cues = new() { ["bad"] = new() { Variants = ["res://Assets/Sounds/../other.wav"] } } },
            settings with { Cues = new() { ["bad"] = new() { Variants = ["res://Assets/Sounds/perfect-shot-1.m4a"] } } }
        })
        {
            bool rejected = false;
            try { bad.Validate(); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "Invalid audio settings accepted.");
        }
        foreach (string json in new[] { "null", "{\"Cues\":null}", "{\"MaxVoice\":4}", "{\"Rules\":[null]}" })
        {
            bool rejected = false;
            try { ImpactAudioSettings.FromJson(json); }
            catch (ArgumentException) { rejected = true; }
            catch (JsonException) { rejected = true; }
            Require(rejected, "Malformed audio JSON accepted.");
        }
    }

    public static void LiveContacts()
    {
        int bats = 0, wickets = 0, perfect = 0, imperfect = 0;
        var settings = Settings();
        for (int timing = 150; timing <= 240; timing += 6)
        {
            var simulation = new Simulation(new());
            Require(simulation.Begin(new(32, 16, 2.15), out _), "Delivery setup failed.");
            var selector = new ImpactSoundSelector(settings);
            for (int tick = 0; tick < 500; tick++)
            {
                simulation.Tick(BattingChecks.AtClock(240 - Math.Clamp((tick - timing) * 8.0, 0, 270)));
                foreach (var contact in simulation.Events)
                {
                    Require(double.IsFinite(contact.ImpactSpeed) && contact.ImpactSpeed >= 0, "Invalid closing speed.");
                    if (contact.Kind == ContactKind.Bat)
                    {
                        bats++;
                        Require(contact.BatBladeFraction is >= 0 and <= 1 && contact.BatFaceAlignment is >= 0 and <= 1,
                            "Bat contact measurements missing or out of range.");
                        if (selector.TrySelect(contact, simulation.TickCount * SimulationSettings.Tick, out var sound))
                        {
                            if (sound.Cue == "shot_perfect") perfect++;
                            else if (sound.Cue == "shot_imperfect") imperfect++;
                        }
                    }
                    else
                    {
                        Require(contact.BatBladeFraction == null && contact.BatFaceAlignment == null, "Non-bat contact carries stale blade measurements.");
                        if (contact.Kind == ContactKind.Wicket && contact.ImpactSpeed > settings.MinimumImpactSpeed) wickets++;
                    }
                }
            }
        }
        Require(bats > 0 && wickets > 0 && perfect > 0 && imperfect > 0, "Real swings must exercise both shot sounds and wicket impacts.");
        Console.WriteLine($"      audio contacts: {perfect} perfect, {imperfect} imperfect, {wickets} wicket impacts");
    }

    private static void Require(bool valid, string message)
    { if (!valid) throw new InvalidOperationException(message); }
}
