using System.Text.Json;
using LithophaneGUI.Core;

namespace LithophaneGUI;

/// <summary>What the app remembers between runs (besides the session project itself).</summary>
/// <param name="Language">The language the user picked, or null to follow the OS.</param>
internal sealed record SessionState(string? ProjectPath, bool Modified, string? Language = null);

internal static class AppState
{
    public static readonly string Dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LithophaneGUI");
    /// <summary>Unsaved work, kept so it comes back on the next start.</summary>
    public static readonly string SessionFile = Path.Combine(Dir, "session" + LithophaneProject.Extension);
    public static readonly string StateFile = Path.Combine(Dir, "state.json");

    public static SessionState? Load()
    {
        try
        {
            return File.Exists(StateFile) ? JsonSerializer.Deserialize<SessionState>(File.ReadAllText(StateFile)) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Save(SessionState state)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(StateFile, JsonSerializer.Serialize(state));
    }
}
