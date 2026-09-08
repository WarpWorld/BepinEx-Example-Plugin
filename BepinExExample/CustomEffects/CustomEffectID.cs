using System.Security.Cryptography;
using System.Text;
using CrowdControl.Delegates.Effects;

namespace CrowdControl.CustomEffects;

/// <summary>Builds the stable, readable IDs that custom effects are registered under.</summary>
/// <remarks>
/// An effect's ID has to be derived rather than assigned, because the same effect has to end up with
/// the same ID on every machine the streamer plays on. That is what lets the Crowd Control app
/// recognise an effect it has seen before and keep the price and other settings the streamer changed,
/// instead of treating it as a brand new effect each time.
/// <para>
/// The ID is a readable slug plus a fingerprint of what the effect is: the game, the author, the
/// name, and whether it is timed. Two people writing an effect with the same name get the same ID
/// only if they also share an author, which is the point of including one.
/// </para>
/// <para>
/// The corollary is that renaming an effect gives it a new ID, and the streamer's settings stay
/// attached to the old name. That is the deliberate trade for not having to hand-assign IDs.
/// </para>
/// </remarks>
internal static class CustomEffectID
{
    /// <summary>How much of the fingerprint hash to keep.</summary>
    /// <remarks>
    /// Sixty bits, which is far more than enough to keep one streamer's effects apart and short
    /// enough to leave the readable part of the ID doing the work.
    /// </remarks>
    private const int HASH_LENGTH = 15;

    /// <summary>The longest the readable name slug may be, so the whole ID stays well inside the API's 150 character limit.</summary>
    private const int MAX_SLUG_LENGTH = 48;

    /// <summary>The longest the author slug may be.</summary>
    private const int MAX_AUTHOR_LENGTH = 24;

    /// <summary>Builds the ID for a custom effect.</summary>
    /// <param name="declaredID">The ID declared on the effect's <see cref="EffectAttribute"/>.</param>
    /// <param name="menu">The effect's menu metadata, if it declared any.</param>
    /// <param name="timed">True if the effect has a duration.</param>
    /// <returns>An ID of the form <c>cc_custom_customEffect_mario_a1b2c3d4e5f6789</c>.</returns>
    internal static string Build(string declaredID, EffectMenuAttribute? menu, bool timed)
    {
        string name = menu?.Name ?? declaredID;
        string author = menu?.Author ?? string.Empty;
        string type = timed ? "timed" : "instant";

        //deliberately excludes the pack folder name: moving a file between folders, or a streamer
        //renaming the folder they downloaded, must not orphan their settings
        string fingerprint = string.Join("\u001f", CustomEffectPaths.GameName, author, name, type);

        StringBuilder id = new(CustomEffectPaths.ID_PREFIX, 150);
        id.Append(Slug(name, MAX_SLUG_LENGTH, "effect"));

        string authorSlug = Slug(author, MAX_AUTHOR_LENGTH, string.Empty);
        if (authorSlug.Length > 0) id.Append('_').Append(authorSlug);

        id.Append('_').Append(Hash(fingerprint));

        return id.ToString();
    }

    /// <summary>Reduces a name to a readable camel-case identifier fragment.</summary>
    /// <param name="value">The name to reduce.</param>
    /// <param name="maxLength">The most characters to keep.</param>
    /// <param name="fallback">What to return when nothing usable is left.</param>
    /// <returns>For example, "Custom Effect" becomes "customEffect".</returns>
    private static string Slug(string value, int maxLength, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        StringBuilder slug = new(value.Length);
        bool startOfWord = false; //the first word stays lower case, so the result reads as an identifier

        foreach (char c in value)
        {
            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
            {
                if (slug.Length >= maxLength) break;
                slug.Append(startOfWord ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                startOfWord = false;
                continue;
            }

            //anything else is a word break rather than a character we try to keep
            startOfWord = slug.Length > 0;
        }

        return (slug.Length > 0) ? slug.ToString() : fallback;
    }

    /// <summary>Hashes the effect's identity into a short, stable, lower-case hex string.</summary>
    private static string Hash(string fingerprint)
    {
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(fingerprint));

        StringBuilder hex = new(HASH_LENGTH);
        foreach (byte b in hash)
        {
            if (hex.Length >= HASH_LENGTH) break;
            hex.Append(b.ToString("x2"));
        }

        return hex.ToString(0, HASH_LENGTH);
    }
}
