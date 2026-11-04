using Cricket.Physics;
using Godot;

namespace Cricket2D;

/// <summary>Opt-in native import/playback/lifecycle checks, using the same player as gameplay.</summary>
internal partial class AudioSmokeRun : Node
{
    public override async void _Ready()
    {
        try
        {
            var audio = new ImpactAudio();
            AddChild(audio);
            Require(audio.ReadyForPlayback && audio.VoiceCount == 4, "preload configured samples and bounded voices");
            var perfect = new ContactEvent(ContactKind.Bat, default, 30, 40, 25, .55, 1);
            var imperfect = perfect with { BatBladeFraction = .05 };
            var wicket = new ContactEvent(ContactKind.Wicket, default, 25, 8, 25);
            foreach (var (contact, cue) in new[] { (perfect, "shot_perfect"), (imperfect, "shot_imperfect"), (wicket, "wicket") })
            {
                audio.ResetDelivery();
                int before = audio.PlayedCount;
                audio.PlayContact(contact, 1);
                Require(audio.PlayedCount == before + 1 && audio.LastCue == cue, "play " + cue);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(audio.ActiveVoiceCount > 0, "native playback " + cue);
                await ToSignal(GetTree().CreateTimer(.12), SceneTreeTimer.SignalName.Timeout);
            }
            int played = audio.PlayedCount;
            audio.PlayContact(wicket, 2);
            Require(audio.PlayedCount == played, "one wicket sound per delivery");
            Require(audio.ActiveVoiceCount > 0, "playback active before pause");
            audio.SetPaused(true);
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
            Require(audio.PausedVoiceCount > 0, "pause active playback");
            audio.PlayContact(perfect, 3);
            Require(audio.PlayedCount == played, "ignore impacts while paused");
            audio.SetPaused(false);
            await ToSignal(GetTree().CreateTimer(.05), SceneTreeTimer.SignalName.Timeout);
            Require(audio.PausedVoiceCount == 0 && audio.ActiveVoiceCount > 0, "resume playback");
            audio.ResetDelivery();
            // Fill all voices, then steal the oldest. No additional nodes may be created.
            for (int i = 0; i < 20; i++) audio.PlayContact(perfect, 4 + i * .1);
            Require(audio.GetChildCount() == 4 && audio.ActiveVoiceCount <= 4, "fixed voice pool");
            audio.Stop();
            Require(audio.ActiveVoiceCount == 0, "stop on menu/setup");
            audio.ResetDelivery();
            audio.PlayContact(wicket, 7);
            Require(audio.LastCue == "wicket", "wicket rearms next delivery");
            audio.ResetDelivery();
            var simulation = new Simulation(new());
            Require(simulation.Begin(new(32, 16, 2.15), out _), "live delivery setup");
            int beforeDelivery = audio.PlayedCount;
            for (int i = 0; i < 400; i++)
            {
                simulation.Tick(default);
                audio.ConsumeContacts(simulation);
                int once = audio.PlayedCount;
                audio.ConsumeContacts(simulation);
                Require(audio.PlayedCount == once, "same physics tick consumed twice");
            }
            Require(audio.PlayedCount > beforeDelivery, "live collision reached native audio");
            audio.Stop();
            // The mixer owns stopped playbacks until its next asynchronous cleanup.
            await ToSignal(GetTree().CreateTimer(.15), SceneTreeTimer.SignalName.Timeout);
            GD.Print("AUDIO SMOKE PASS: imported samples, both shot sounds, wicket, live contacts, tick deduplication, pause, reset and bounded voices.");
            GetTree().Quit();
        }
        catch (Exception ex) { GD.PushError(ex.ToString()); GetTree().Quit(1); }
    }
    private static void Require(bool valid, string message)
    {
        if (!valid) throw new InvalidOperationException(message);
    }
}
