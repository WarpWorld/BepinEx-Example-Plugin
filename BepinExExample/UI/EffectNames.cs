using System.Text;

namespace CrowdControl.UI;

/// <summary>
/// Display names for effect codes, used by the on-screen overlay.
/// </summary>
/// <remarks>
/// The Crowd Control pack (the .cs file describing your effects to the app) is the single source of
/// truth for what an effect is called. An overlay that disagreed with the menu a viewer just bought
/// from would be worse than no label at all, so fill <see cref="Names"/> in from the pack - a
/// generated block is easiest to keep honest:
///
/// <code>
///     { "speedUp",   "Speed Up" },
///     { "lowGravity", "Low Gravity" },
/// </code>
///
/// Anything missing falls back to a tidied-up version of the code, so the overlay stays readable
/// while an effect is still being written.
/// </remarks>
public static class EffectNames
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        //TODO: paste your effect codes and display names here, from your Crowd Control pack.
    };

    /// <summary>The display name for an effect code.</summary>
    /// <remarks>
    /// The fallback handles both naming styles effect codes come in: "speedUp" becomes "Speed Up",
    /// "stat_up_speed" becomes "Stat Up Speed".
    /// </remarks>
    public static string Pretty(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Effect";
        if (Names.TryGetValue(code, out string name)) return name;

        StringBuilder sb = new(code.Length + 4);
        bool startOfWord = true;
        foreach (char c in code)
        {
            if (c is '_' or '-')
            {
                sb.Append(' ');
                startOfWord = true;
                continue;
            }

            if (!startOfWord && char.IsUpper(c)) sb.Append(' ');

            sb.Append(startOfWord ? char.ToUpperInvariant(c) : c);
            startOfWord = false;
        }

        return sb.ToString();
    }
}
