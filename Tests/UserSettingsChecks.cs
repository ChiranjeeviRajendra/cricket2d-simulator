using Cricket.Physics;
using Cricket2D.Presentation;

internal static class UserSettingsChecks
{
    public static void Persistence()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cricket-settings-{Guid.NewGuid():N}.json");
        try
        {
            Require(UserSettingsStore.Load(path).BowlingSpeed == .5, "Missing save did not use half speed.");
            foreach (double speed in new[] { .5, .75, 1 })
            {
                var settings = new GameplaySettings { BowlingSpeed = speed };
                Require(UserSettingsStore.TrySave(path, settings, out string error), error);
                Require(UserSettingsStore.Load(path).BowlingSpeed == speed, "Fresh settings instance lost the saved speed.");
                Require(!File.Exists(path + ".tmp"), "Successful save left its temporary file behind.");
            }
            // A failed replacement must preserve the previous valid save.
            Directory.CreateDirectory(path + ".tmp");
            Require(!UserSettingsStore.TrySave(path, new(), out string failure) && failure.Length > 0,
                "Save failure was silently accepted.");
            Require(UserSettingsStore.Load(path).BowlingSpeed == 1, "Failed save damaged the previous value.");
            Directory.Delete(path + ".tmp");
            foreach (string invalid in new[] { "{broken", "null", "{}", "{\"BowlingSpeed\":0.49}",
                "{\"BowlingSpeed\":1.01}", "{\"BowlingSpeed\":\"NaN\"}", "{\"BowlingSpeed\":null}" })
            {
                File.WriteAllText(path, invalid);
                Require(UserSettingsStore.Load(path).BowlingSpeed == .5, "Invalid save did not fall back to the default.");
            }
        }
        finally
        {
            File.Delete(path);
            if (Directory.Exists(path + ".tmp")) Directory.Delete(path + ".tmp");
            else File.Delete(path + ".tmp");
        }
    }

    public static void UniversalPreference()
    {
        var gameplay = new GameplaySettings { BowlingSpeed = .75 };
        var first = new Simulation(new(), gameplay: gameplay);
        var second = new Simulation(new(), gameplay: gameplay);
        Require(first.Begin(new(32, 16, 2.15), out _), "Delivery setup failed.");
        first.ReturnToSetup();
        Require(first.BeginPrepared(first.Plan!, out _), "Prepared delivery rejected gameplay preferences.");
        Require(first.Gameplay.BowlingSpeed == .75, "Delivery reset the global speed.");
        gameplay.BowlingSpeed = 1;
        Require(first.Gameplay.BowlingSpeed == 1 && second.Gameplay.BowlingSpeed == 1, "Simulations did not share the global speed.");
        foreach (double invalid in new[] { .49, 1.01, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            bool rejected = false;
            try { gameplay.BowlingSpeed = invalid; }
            catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected && gameplay.BowlingSpeed == 1, "Invalid speed changed the universal setting.");
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
