namespace CrowdControl.Delegates.Effects;

/// <summary>Describes how an effect should appear in the Crowd Control menu.</summary>
/// <remarks>
/// Effects that ship inside the mod do not need this - their menu entries live in the game pack,
/// which is the single source of truth for anything Warp World publishes. This attribute exists for
/// custom effects loaded from disk, which have no pack entry and must describe themselves.
/// <para>
/// Every field here maps onto the record accepted by <c>PUT /menu/custom-effects</c>. The duration
/// itself is not repeated: it comes from <see cref="EffectAttribute.DefaultDuration"/> so there is
/// only ever one place to change it.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Effect(id: "chaos_gravity", defaultDuration: 30, conflicts: ["chaos_gravity", "low_gravity"])]
/// [EffectMenu("Chaos Gravity", 250,
///     Description = "Gravity flips direction at random.",
///     Category = ["Physics"],
///     Orderliness = -1f,
///     Morality = -0.5f)]
/// public class ChaosGravity : Effect { }
/// </code>
/// </example>
/// <param name="name">The name shown in the menu.</param>
/// <param name="price">The default price in coins. The streamer can change this afterwards.</param>
[AttributeUsage(AttributeTargets.Class)]
public class EffectMenuAttribute(string name, long price) : Attribute
{
    /// <summary>The name shown in the menu.</summary>
    public string Name { get; } = name;

    /// <summary>The default price in coins.</summary>
    public long Price { get; } = price;

    /// <summary>The menu description. Worth writing - viewers read this before spending.</summary>
    public string? Description { get; set; }

    /// <summary>Who wrote the effect.</summary>
    /// <remarks>
    /// Part of the effect's identity, not just credit: it is what keeps two people's effects apart
    /// when they happen to pick the same name. Changing it gives the effect a new ID, so the
    /// streamer's price and other settings stay with the old one.
    /// </remarks>
    public string? Author { get; set; }

    /// <summary>The category path the effect appears under.</summary>
    public string[]? Category { get; set; }

    /// <summary>The groups the effect belongs to.</summary>
    public string[]? Group { get; set; }

    /// <summary>Freeform tags.</summary>
    public string[]? Tags { get; set; }

    /// <summary>The name of the image to display for the effect.</summary>
    public string? Image { get; set; }

    /// <summary>A note shown to the streamer.</summary>
    public string? Note { get; set; }

    /// <summary>True to register the effect but keep it out of the menu.</summary>
    public bool Hidden { get; set; }

    /// <summary>True to show the effect in the menu but leave it unpurchasable.</summary>
    public bool Inactive { get; set; }

    /// <summary>True to stop the streamer changing the duration of a timed effect.</summary>
    public bool DurationLocked { get; set; }

    /// <summary>Minutes of session-wide cooldown after the effect fires. Zero for none.</summary>
    /// <remarks>Minutes, not seconds, and capped at 120 by the Crowd Control API.</remarks>
    public float SessionCooldown { get; set; }

    /// <summary>Minutes of per-viewer cooldown after the effect fires. Zero for none.</summary>
    /// <inheritdoc cref="SessionCooldown" path="/remarks"/>
    public float UserCooldown { get; set; }

    /// <summary>Chaotic (-1) to orderly (+1). Leave unset to omit the alignment.</summary>
    public float Orderliness { get; set; } = float.NaN;

    /// <summary>Evil (-1) to good (+1). Leave unset to omit the alignment.</summary>
    public float Morality { get; set; } = float.NaN;

    /// <summary>The lowest quantity a viewer may buy. Leave both quantity bounds at zero to omit the range.</summary>
    public long QuantityMin { get; set; }

    /// <summary>The highest quantity a viewer may buy.</summary>
    public long QuantityMax { get; set; }

    /// <summary>True if an alignment was supplied.</summary>
    internal bool HasAlignment => !float.IsNaN(Orderliness) && !float.IsNaN(Morality);

    /// <summary>True if a quantity range was supplied.</summary>
    internal bool HasQuantity => (QuantityMin != 0) || (QuantityMax != 0);
}
