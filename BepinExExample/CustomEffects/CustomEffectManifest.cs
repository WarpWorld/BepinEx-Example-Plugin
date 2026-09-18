using ConnectorLib.JSON;
using CrowdControl.Delegates.Effects;

namespace CrowdControl.CustomEffects;

/// <summary>Turns a registered custom effect into the menu record the Crowd Control app upserts.</summary>
internal static class CustomEffectManifest
{
    /// <summary>The price used when an effect does not declare one.</summary>
    /// <remarks>Deliberately unremarkable. The streamer can change it, and most will.</remarks>
    private const long DEFAULT_PRICE = 100;

    /// <summary>The category custom effects land in when they do not choose one.</summary>
    private const string DEFAULT_CATEGORY = "Custom Effects";

    /// <summary>Builds the menu record for a registered effect.</summary>
    /// <param name="registration">The registered effect.</param>
    /// <param name="pack">The pack the effect came from.</param>
    /// <returns>The record to send to the client.</returns>
    internal static CustomEffectDefinition Build(EffectLoader.Registration registration, CustomEffectPack pack)
    {
        EffectMenuAttribute? menu = registration.Menu;
        EffectAttribute effect = registration.Attribute;

        //the registered ID is a derived slug plus a hash, so the declared ID is the only readable
        //thing left to build a name out of when an effect did not supply one
        string fallbackName = UI.EffectNames.Pretty(registration.DeclaredID);

        CustomEffectDefinition definition = new()
        {
            effectID = registration.ID,
            name = menu?.Name ?? fallbackName,
            //the API clamps to a minimum of one coin, so a zero here would silently become something else
            price = Math.Max(1, menu?.Price ?? DEFAULT_PRICE),
            description = menu?.Description,
            category = (menu?.Category is { Length: > 0 } category) ? category : [DEFAULT_CATEGORY, pack.DisplayName],
            group = BuildGroups(menu),
            image = menu?.Image,
            note = menu?.Note,
            hidden = (menu?.Hidden == true) ? true : null,
            inactive = (menu?.Inactive == true) ? true : null,
            sessionCooldown = Cooldown(menu?.SessionCooldown),
            userCooldown = Cooldown(menu?.UserCooldown),

            //the handler travels in the arguments rather than relying on the effect code, because a
            //custom effect can reach us either as its own ID or as an ID we do not recognise
            args = new() { [CustomEffectPaths.HANDLER_ARG] = registration.ID },

            tags = BuildTags(menu),
        };

        //the duration lives on [Effect] so a timed effect only declares it once
        if (effect.DefaultDuration > 0)
        {
            long seconds = Math.Max(1, (long)Math.Round(effect.DefaultDuration.TotalSeconds));
            definition.duration = new(seconds, (menu?.DurationLocked == true) ? true : null);
        }

        if (menu?.HasAlignment == true)
            definition.alignment = new(menu.Orderliness, menu.Morality);

        if (menu?.HasQuantity == true)
            definition.quantity = new(menu.QuantityMin, menu.QuantityMax);

        return definition;
    }

    /// <summary>The longest cooldown the Crowd Control API accepts, in minutes.</summary>
    private const float MAX_COOLDOWN_MINUTES = 120f;

    /// <summary>Converts a declared cooldown into something the API will accept.</summary>
    /// <remarks>
    /// Cooldowns are minutes, and anything above two hours is rejected outright - clamping keeps one
    /// over-eager number from costing the author their whole pack.
    /// </remarks>
    private static float? Cooldown(float? minutes) =>
        (minutes > 0) ? Math.Min(minutes.Value, MAX_COOLDOWN_MINUTES) : null;

    /// <summary>Puts the effect in the mod's own group, which is what lets a session hide all of them at once.</summary>
    private static string[] BuildGroups(EffectMenuAttribute? menu)
    {
        if (menu?.Group is not { Length: > 0 } groups) return [CustomEffectPaths.GROUP];

        return groups.Contains(CustomEffectPaths.GROUP) ? groups : [.. groups, CustomEffectPaths.GROUP];
    }

    private static string[] BuildTags(EffectMenuAttribute? menu)
    {
        if (menu?.Tags is not { Length: > 0 } tags) return [CustomEffectPaths.TAG];

        //the marker tag is always present, so custom effects stay identifiable in the menu and in support logs
        return tags.Contains(CustomEffectPaths.TAG) ? tags : [.. tags, CustomEffectPaths.TAG];
    }
}
