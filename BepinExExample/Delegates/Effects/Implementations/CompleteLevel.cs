namespace CrowdControl.Delegates.Effects.Implementations;

/* == EXAMPLE (Anger Foot) - an instant (non-timed) effect ==
 * Instant effects only need to implement Start(). Return Success on success, Retry if the
 * effect can't run right now (the client will re-attempt it), or Failure/Unavailable if it
 * should be refunded.
 * Uncomment and adapt this for your game.

using ConnectorLib.JSON;

[Effect(id: "level_complete")]
public class CompleteLevel(CrowdControlMod mod, NetworkClient client) : Effect(mod, client)
{
    public override EffectResponse Start(EffectRequest request)
    {
        try
        {
            GameState.CompleteLevel();
            return EffectResponse.Success(request.ID);
        }
        catch (Exception e)
        {
            CrowdControlMod.Instance.Logger.LogError($"Crowd Control Error: {e}");
            return EffectResponse.Retry(request.ID);
        }
    }
}

*/
