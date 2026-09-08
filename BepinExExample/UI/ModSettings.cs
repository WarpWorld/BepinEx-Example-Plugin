using BepInEx.Configuration;

namespace CrowdControl.UI;

/// <summary>
/// User-facing mod settings, persisted by BepInEx to
/// <c>BepInEx\config\WarpWorld.CrowdControl.cfg</c> so a streamer can turn the on-screen pieces off.
/// </summary>
/// <remarks>
/// Everything here defaults to ON. The overlay is genuinely useful - knowing at a glance whether
/// Crowd Control is connected saves a lot of "is it broken?" - but it is drawn over someone's
/// stream, so it must be possible to opt out without touching the DLL.
/// </remarks>
public static class ModSettings
{
    private const string SECTION = "Overlay";
    private const string SECTION_CUSTOM_EFFECTS = "CustomEffects";

    private static ConfigEntry<bool> _showMessages;
    private static ConfigEntry<bool> _showIndicator;
    private static ConfigEntry<float> _messageSeconds;
    private static ConfigEntry<bool> _allowCustomEffects;
    private static ConfigEntry<bool> _customEffectDevReload;

    /// <summary>Show a line on screen when an effect fires.</summary>
    public static bool ShowMessages => _showMessages?.Value ?? true;

    /// <summary>Show the small connection dot.</summary>
    public static bool ShowIndicator => _showIndicator?.Value ?? true;

    /// <summary>How long each on-screen message stays up, in seconds.</summary>
    public static float MessageSeconds => _messageSeconds?.Value ?? 4f;

    /// <summary>Load community-written effects from the custom effects folder.</summary>
    /// <remarks>
    /// The mod's own <see cref="CrowdControlMod.CUSTOM_EFFECTS_SUPPORTED"/> has the final say. When a
    /// game has not adopted custom effects the setting is never created, and this stays false however
    /// the config file is edited.
    /// </remarks>
    public static bool AllowCustomEffects =>
        CrowdControlMod.CUSTOM_EFFECTS_SUPPORTED && (_allowCustomEffects?.Value ?? true);

    /// <summary>Reload a custom effect pack in place when its files change, instead of asking for a restart.</summary>
    /// <remarks>For writing effects, not for streaming with them - the replaced code stays in memory.</remarks>
    public static bool CustomEffectDevReload => _customEffectDevReload?.Value ?? false;

    /// <summary>Binds the settings to the plugin's config file. Safe to call more than once.</summary>
    public static void Initialize(ConfigFile config)
    {
        try
        {
            _showMessages = config.Bind(
                SECTION, "ShowMessages", true,
                "Displays a short line when an effect fires. Turn off for a clean capture.");

            _showIndicator = config.Bind(
                SECTION, "ShowConnectionIndicator", true,
                "Small dot showing whether the mod is connected to the Crowd Control app. " +
                "Green connected, red not.");

            _messageSeconds = config.Bind(
                SECTION, "MessageSeconds", 4f,
                "How long an on-screen effect message stays visible, in seconds.");

            //no settings for a feature this mod does not have - a knob that cannot do anything is
            //worse than no knob, because someone will find it and believe it works
#pragma warning disable CS0162 // Unreachable code detected
            if (!CrowdControlMod.CUSTOM_EFFECTS_SUPPORTED) return;

            _allowCustomEffects = config.Bind(
                SECTION_CUSTOM_EFFECTS, "AllowCustomEffects", true,
                "Load community-written effects from " +
                @"%APPDATA%\CrowdControl-Apps\CustomEffects\<game>. " +
                "Effects in that folder run as part of the game, so only put things there that you trust. " +
                "Turn this off to ignore the folder entirely.");

            _customEffectDevReload = config.Bind(
                SECTION_CUSTOM_EFFECTS, "DevReload", false,
                "Reload a custom effect as soon as its file changes, rather than at the next restart. " +
                "For writing effects: the old version stays in memory, so leave this off while streaming.");
#pragma warning restore CS0162 // Unreachable code detected
        }
        catch (Exception e)
        {
            //settings are a convenience - never let them stop the mod loading
            CrowdControlMod.Instance?.Logger.LogWarning($"Could not create settings: {e.Message}");
        }
    }
}
