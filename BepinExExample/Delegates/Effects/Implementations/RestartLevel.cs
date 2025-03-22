using ConnectorLib.JSON;
using UnityEngine;

namespace CrowdControl.Delegates.Effects.Implementations;

[Effect(id: "level_restart")]
public class RestartLevel(CrowdControlMod mod, NetworkClient client) : Effect(mod, client)
{
    public override EffectResponse Start(EffectRequest request)
    {
        try
        {
            GameState.RestartCurrentLevel();
            GameStateManager.DialogMsgAsync("Restart Level!", false).Forget();
            return EffectResponse.Success(request.ID);
        }
        catch (Exception e)
        {
            CrowdControlMod.Instance.Logger.LogError($"Crowd Control Error: {e}");
            return EffectResponse.Retry(request.ID);
            //return EffectResponse.Failure(request.ID);
        }
    }
}