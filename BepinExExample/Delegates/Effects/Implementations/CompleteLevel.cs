using ConnectorLib.JSON;

namespace CrowdControl.Delegates.Effects.Implementations;

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
            //return EffectResponse.Failure(request.ID);
        }
    }
}