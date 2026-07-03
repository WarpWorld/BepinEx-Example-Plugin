using System.Text;
using ConnectorLib.JSON;
using Newtonsoft.Json.Linq;

namespace CrowdControl;

/// <summary>Helpers for working with <see cref="EffectRequest"/> objects.</summary>
public static class EffectRequestEx
{
    /// <summary>The name used when a request has no usable viewer name.</summary>
    public const string DEFAULT_VIEWER_NAME = "the crowd";

    /// <summary>The maximum number of characters allowed in a sanitized viewer name.</summary>
    public const int MAX_VIEWER_NAME_LENGTH = 32;

    /// <summary>
    /// Gets a sanitized, display-safe viewer name for the request.
    /// </summary>
    /// <param name="request">The effect request.</param>
    /// <param name="fallback">The name to use if the request has no usable viewer name.</param>
    /// <returns>A non-empty display name that is safe to pass to in-game text rendering.</returns>
    /// <remarks>
    /// Viewer names come from external streaming services and may contain control characters,
    /// unpaired surrogates, emoji, or markup (e.g. TextMeshPro rich text tags like &lt;color&gt;)
    /// that many game fonts and dialog systems cannot safely render. Always use this instead of
    /// reading <see cref="EffectRequest.viewer"/> directly when showing a name on screen.
    /// </remarks>
    public static string GetViewerDisplayName(this EffectRequest request, string fallback = DEFAULT_VIEWER_NAME)
    {
        string? name = request.viewer;

        //fall back to the first entry in the viewers array (group/coalesced effects)
        if (string.IsNullOrWhiteSpace(name) && (request.viewers != null))
        {
            foreach (JToken viewer in request.viewers)
            {
                name = (viewer as JObject)?.Value<string>("name") ?? viewer.Type switch
                {
                    JTokenType.String => viewer.Value<string>(),
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(name)) break;
            }
        }

        string sanitized = SanitizeDisplayName(name);
        return (sanitized.Length > 0) ? sanitized : fallback;
    }

    /// <summary>
    /// Strips characters that are unsafe to hand to in-game text rendering from a display name.
    /// </summary>
    /// <param name="name">The raw display name. May be null.</param>
    /// <returns>The sanitized name. May be empty if nothing printable remains.</returns>
    public static string SanitizeDisplayName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;

        StringBuilder result = new(Math.Min(name!.Length, MAX_VIEWER_NAME_LENGTH));
        bool lastWasSpace = true; //true so leading whitespace is dropped
        foreach (char c in name)
        {
            //control chars break log lines and dialogs; surrogates (emoji and other non-BMP glyphs)
            //are commonly missing from game fonts and render as boxes, so both are dropped
            if (char.IsControl(c) || char.IsSurrogate(c)) continue;

            //angle brackets are markup delimiters for TextMeshPro/UGUI rich text - drop them so
            //viewers can't inject tags like <size=200> or <color> into on-screen messages
            if (c is '<' or '>') continue;

            if (char.IsWhiteSpace(c))
            {
                if (lastWasSpace) continue; //collapse runs of whitespace
                result.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                result.Append(c);
                lastWasSpace = false;
            }

            if (result.Length >= MAX_VIEWER_NAME_LENGTH) break;
        }

        return result.ToString().TrimEnd();
    }
}
