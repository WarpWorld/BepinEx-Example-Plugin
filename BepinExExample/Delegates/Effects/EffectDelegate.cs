using ConnectorLib.JSON;

namespace CrowdControl.Delegates.Effects;

/// <summary>
/// Represents a delegate for handling effect requests.
/// </summary>
/// <param name="client">The control client.</param>
/// <param name="req">The effect request.</param>
/// <returns>The effect response.</returns>
public delegate EffectResponse EffectDelegate(ControlClient client, EffectRequest req);