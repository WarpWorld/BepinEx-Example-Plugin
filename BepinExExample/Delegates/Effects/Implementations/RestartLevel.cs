namespace CrowdControl.Delegates.Effects.Implementations;

/* == EXAMPLE (Anger Foot) - an instant (non-timed) effect ==
 * See CompleteLevel.cs for a full explanation of this pattern.
 * Uncomment and adapt this for your game.

using ConnectorLib.JSON;

[Effect(id: "level_restart")]
public class RestartLevel(CrowdControlMod mod, NetworkClient client) : Effect(mod, client)
{
    public override EffectResponse Start(EffectRequest request)
    {
        GameState.RestartCurrentLevel();
        GameStateManager.DialogMsgAsync($"{request.GetViewerDisplayName()} restarted the level!", false).Forget();
        return EffectResponse.Success(request.ID);
    }
}

*/
