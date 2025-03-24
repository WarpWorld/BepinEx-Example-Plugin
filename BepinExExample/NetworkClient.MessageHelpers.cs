using ConnectorLib.JSON;
using CrowdControl.Delegates.Metadata;

namespace CrowdControl;

public partial class NetworkClient
{
    public void AttachMetadata(EffectResponse response)
    {
        response.metadata = new();
        foreach (string key in MetadataDelegates.CommonMetadata)
        {
            if (MetadataLoader.Metadata.TryGetValue(key, out MetadataDelegate? del))
                response.metadata.Add(key, del.Invoke(m_mod));
            else
                CrowdControlMod.Instance.Logger.LogError($"Metadata delegate \"{key}\" could not be found. Available delegates: {string.Join(", ", MetadataLoader.Metadata.Keys)}");
        }
    }
    
    #region Show Effects

    /// <summary>Shows the specified effects on the menu.</summary>
    /// <param name="codes">The effect IDs to show.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool ShowEffects(params string[] codes) => Send(new EffectUpdate(codes, EffectStatus.Visible));

    /// <inheritdoc cref="ShowEffects(string[])"/>
    public bool ShowEffects(params IEnumerable<string> codes) => Send(new EffectUpdate(codes, EffectStatus.Visible));

    /// <inheritdoc cref="ShowEffects(string[])"/>
    /// <summary>Asynchronously shows the specified effects on the menu.</summary>
    public Task<bool> ShowEffectsAsync(params string[] codes) => SendAsync(new EffectUpdate(codes, EffectStatus.Visible));

    /// <inheritdoc cref="ShowEffectsAsync(string[])"/>
    public Task<bool> ShowEffectsAsync(params IEnumerable<string> codes) => SendAsync(new EffectUpdate(codes, EffectStatus.Visible));

    /// <summary>Shows all effects on the menu.</summary>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool ShowAllEffects() => ShowEffects(m_mod.EffectLoader.Effects.Keys);

    /// <inheritdoc cref="ShowAllEffects()"/>
    /// <summary>Asynchronously shows all effects on the menu.</summary>
    public Task<bool> ShowAllEffectsAsync() => ShowEffectsAsync(m_mod.EffectLoader.Effects.Keys);

    #endregion
    
    #region Hide Effects
    
    /// <summary>Hides the specified effects on the menu.</summary>
    /// <param name="codes">The effect IDs to hide.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool HideEffects(params string[] codes) => Send(new EffectUpdate(codes, EffectStatus.NotVisible));

    /// <inheritdoc cref="HideEffects(string[])"/>
    public bool HideEffects(params IEnumerable<string> codes) => Send(new EffectUpdate(codes, EffectStatus.NotVisible));

    /// <inheritdoc cref="HideEffects(string[])"/>
    /// <summary>Asynchronously hides the specified effects on the menu.</summary>
    public Task<bool> HideEffectsAsync(params string[] codes) => SendAsync(new EffectUpdate(codes, EffectStatus.NotVisible));

    /// <inheritdoc cref="HideEffectsAsync(string[])"/>
    public Task<bool> HideEffectsAsync(params IEnumerable<string> codes) => SendAsync(new EffectUpdate(codes, EffectStatus.NotVisible));

    /// <summary>Hides all effects on the menu.</summary>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool HideAllEffects() => HideEffects(m_mod.EffectLoader.Effects.Keys);

    /// <inheritdoc cref="HideAllEffects()"/>
    /// <summary>Asynchronously hides all effects on the menu.</summary>
    public Task<bool> HideAllEffectsAsync() => HideEffectsAsync(m_mod.EffectLoader.Effects.Keys);

    #endregion

    #region Enable Effects

    /// <summary>Makes the specified effects selectable on the menu.</summary>
    /// <param name="codes">The effect IDs to make selectable.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool EnableEffects(params string[] codes) => Send(new EffectUpdate(codes, EffectStatus.Selectable));

    /// <inheritdoc cref="EnableEffects(string[])"/>
    public bool EnableEffects(params IEnumerable<string> codes) => Send(new EffectUpdate(codes, EffectStatus.Selectable));

    /// <inheritdoc cref="EnableEffects(string[])"/>
    /// <summary>Asynchronously makes the specified effects selectable on the menu.</summary>
    public Task<bool> EnableEffectsAsync(params string[] codes) => SendAsync(new EffectUpdate(codes, EffectStatus.Selectable));

    /// <inheritdoc cref="EnableEffectsAsync(string[])"/>
    public Task<bool> EnableEffectsAsync(params IEnumerable<string> codes) => SendAsync(new EffectUpdate(codes, EffectStatus.Selectable));

    /// <summary>Makes all effects selectable on the menu.</summary>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool EnableAllEffects() => ShowEffects(m_mod.EffectLoader.Effects.Keys);

    /// <inheritdoc cref="EnableAllEffects()"/>
    /// <summary>Asynchronously makes all effects selectable on the menu.</summary>
    public Task<bool> EnableAllEffectsAsync() => ShowEffectsAsync(m_mod.EffectLoader.Effects.Keys);

    #endregion
    
    #region Disable Effects
    
    /// <summary>Makes the specified effects unselectable on the menu.</summary>
    /// <param name="codes">The effect IDs to make unselectable.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool DisableEffects(params string[] codes) => Send(new EffectUpdate(codes, EffectStatus.NotSelectable));

    /// <inheritdoc cref="DisableEffects(string[])"/>
    public bool DisableEffects(params IEnumerable<string> codes) => Send(new EffectUpdate(codes, EffectStatus.NotSelectable));

    /// <inheritdoc cref="DisableEffects(string[])"/>
    /// <summary>Asynchronously makes the specified effects unselectable on the menu.</summary>
    public Task<bool> DisableEffectsAsync(params string[] codes) => SendAsync(new EffectUpdate(codes, EffectStatus.NotSelectable));

    /// <inheritdoc cref="DisableEffectsAsync(string[])"/>
    public Task<bool> DisableEffectsAsync(params IEnumerable<string> codes) => SendAsync(new EffectUpdate(codes, EffectStatus.NotSelectable));

    /// <summary>Makes all effects unselectable on the menu.</summary>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool DisableAllEffects() => ShowEffects(m_mod.EffectLoader.Effects.Keys);

    /// <inheritdoc cref="DisableAllEffects()"/>
    /// <summary>Asynchronously makes all effects unselectable on the menu.</summary>
    public Task<bool> DisableAllEffectsAsync() => ShowEffectsAsync(m_mod.EffectLoader.Effects.Keys);
    
    #endregion
}