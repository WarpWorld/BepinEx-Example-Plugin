using System.IO;
using System.Reflection;

namespace CrowdControl.CustomEffects;

/// <summary>The well-known locations and identifiers the custom effect system relies on.</summary>
/// <remarks>
/// The folder layout is deliberately predictable so that the Crowd Control app, this mod, and a
/// creator writing effects all agree on where things live without anyone configuring anything.
/// </remarks>
public static class CustomEffectPaths
{
    /// <summary>The shared root for every game's custom effects.</summary>
    /// <remarks>Resolves to <c>%APPDATA%\CrowdControl-Apps\CustomEffects</c>.</remarks>
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CrowdControl-Apps", "CustomEffects");

    /// <summary>The folder this game's custom effects are loaded from.</summary>
    public static string GameFolder => Path.Combine(Root, GameName);

    /// <summary>Where compiled packs are cached, so a streamer only pays the compile cost once per change.</summary>
    public static string CacheFolder => Path.Combine(GameFolder, ".cache");

    /// <summary>The prefix on every effect ID this mod generates.</summary>
    /// <remarks>
    /// Sent to the client so it knows which custom effects are the mod's to manage, and can leave
    /// the rest - anything the streamer built by hand in the app - alone.
    /// </remarks>
    public const string ID_PREFIX = "cc_custom_";

    /// <summary>The key that carries the handler ID in an effect request's arguments.</summary>
    public const string HANDLER_ARG = "handler";

    /// <summary>The tag applied to every custom effect, so they are identifiable in the menu and in support logs.</summary>
    public const string TAG = "__cc_custom_effect";

    /// <summary>The menu group every custom effect this mod generates belongs to.</summary>
    /// <remarks>
    /// The client can hide or show a whole group in one message, which is how custom effects are kept
    /// out of a session until the mod has said which ones it actually has. It has to be a group we
    /// own rather than a category, so that hiding it never touches a custom effect the streamer built
    /// by hand in the app.
    /// </remarks>
    public const string GROUP = "__cc_custom_effects";

    /// <summary>The game name used in the folder path.</summary>
    /// <remarks>
    /// Derived from the mod's own assembly name (<c>CrowdControl.AngerFoot</c> gives
    /// <c>AngerFoot</c>) so that setting <c>GameName</c> in the csproj is enough and there is no
    /// second copy of the name to forget about.
    /// </remarks>
    public static string GameName
    {
        get
        {
            if (_gameName != null) return _gameName;

            string name = Assembly.GetExecutingAssembly().GetName().Name ?? "Unknown";
            const string prefix = "CrowdControl.";
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                name = name.Substring(prefix.Length);

            return _gameName = string.IsNullOrEmpty(name) ? "Unknown" : name;
        }
    }

    private static string? _gameName;

    /// <summary>Reduces an arbitrary file or folder name to something usable in an effect ID.</summary>
    /// <param name="name">The file or folder name.</param>
    /// <returns>The name with every unusable character replaced by an underscore.</returns>
    public static string SanitizeID(string name)
    {
        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_') continue;
            chars[i] = '_';
        }
        return new string(chars);
    }
}
