using System.Reflection;
using ConnectorLib.JSON;

namespace CrowdControl.Delegates.Effects;

/// <summary>Represents an effect that can be applied to the game.</summary>
/// <remarks>Effect implementations should inherit from this class.</remarks>
public abstract class Effect
{
    public EffectAttribute EffectAttribute { get; }
    
    public bool IsTimed => EffectAttribute.DefaultDuration > 0;

    public CrowdControlMod Mod { get; }
    public NetworkClient Client { get; }

    protected Effect(CrowdControlMod mod, NetworkClient client)
    {
        Mod = mod;
        Client = client;
        EffectAttribute = GetType().GetCustomAttributes<EffectAttribute>(false).First();
    }

    /// <summary>Starts an effect in response to an effect request.</summary>
    /// <param name="request">The effect request to handle.</param>
    /// <returns>An <see cref="EffectResponse"/> indicating the result of the operation.</returns>
    public abstract EffectResponse Start(EffectRequest request);

    /// <inheritdoc cref="Start"/>
    /// <summary>Performs an update tick for a timed effect.</summary>
    public virtual EffectResponse? Tick(EffectRequest request) => null;

    /// <inheritdoc cref="Start"/>
    /// <summary>Pauses a timed effect.</summary>
    public virtual EffectResponse? Pause(EffectRequest request) => EffectResponse.Paused(request.ID);

    /// <inheritdoc cref="Start"/>
    /// <summary>Resumes a paused timed effect.</summary>
    public virtual EffectResponse? Resume(EffectRequest request) => EffectResponse.Resumed(request.ID);

    /// <inheritdoc cref="Start"/>
    /// <summary>Stops a running timed effect.</summary>
    public virtual EffectResponse? Stop(EffectRequest request) => EffectResponse.Finished(request.ID);
}