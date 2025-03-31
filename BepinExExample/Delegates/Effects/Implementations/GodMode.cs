using ConnectorLib.JSON;
using UnityEngine;

namespace CrowdControl.Delegates.Effects.Implementations;

//the selfConflict flag is set true here for clarity, but it's actually the default value if the duration is greater than 0
[Effect(
    id: "god_mode",
    defaultDuration: 30,
    selfConflict: true)
]
public class GodMode(CrowdControlMod mod, NetworkClient client) : Effect(mod, client)
{
    public override EffectResponse Start(EffectRequest request)
    {
        //setting selfConflict to true above means that this should only hit if the player is just playing with god mode on
        //this would never run if the effect were already running since it would be blocked by the selfConflict check
        //as such, we assume a player just playing with god mode on isn't going to be turning it off in a few seconds
        //and just fail here rather than retrying waiting for something that probably isn't happening
        if (Cheats.GodMode) return EffectResponse.Failure(request.ID);

        Cheats.GodMode = true;
        GameStateManager.DialogMsgAsync("God Mode Enabled", true).Forget();

        return EffectResponse.Success(request.ID);
    }

    public override EffectResponse? Stop(EffectRequest request)
    {
        //already off somehow(?), just abort with success since our job here is done
        if (!Cheats.GodMode) return EffectResponse.Finished(request.ID);

        Cheats.GodMode = false;
        GameStateManager.DialogMsgAsync("God Mode Ended", true).Forget();

        return EffectResponse.Finished(request.ID);
    }
}