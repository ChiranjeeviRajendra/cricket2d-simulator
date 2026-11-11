using System.Text.Json;
using Cricket.Physics;

namespace Cricket2D.Presentation;

/// <summary>Startup/menu-only persistence. A complete temporary file replaces the previous save.</summary>
public static class UserSettingsStore
{
    public static GameplaySettings Load(string path)
    {
        try { return JsonSerializer.Deserialize<GameplaySettings>(File.ReadAllText(path)) ?? new(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { return new(); }
    }

    public static bool TrySave(string path, GameplaySettings settings, out string error)
    {
        try
        {
            string destination = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            string temporary = destination + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings));
            File.Move(temporary, destination, overwrite: true);
            error = "";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }
}
