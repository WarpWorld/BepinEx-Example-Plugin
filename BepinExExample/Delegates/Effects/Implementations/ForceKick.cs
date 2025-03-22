using ConnectorLib.JSON;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CrowdControl.Delegates.Effects.Implementations;

//the selfConflict flag is set true here for clarity, but it's actually the default value if the duration is greater than 0
[Effect(
    id: "force_kick",
    defaultDuration: 30,
    selfConflict: true)
]
public class ForceKick(CrowdControlMod mod, NetworkClient client) : Effect(mod, client)
{
    private PlayerInput playerInput;
    
    public override EffectResponse Start(EffectRequest request)
    {
        //FindObjectOfType is a slow operation, but it's fine here because it's only called once
        //we avoid calling it in Tick because it's called every frame and would substantially strain the game's physics loop
        playerInput = Object.FindObjectOfType<PlayerInput>();
        if (playerInput is null) return EffectResponse.Failure(request.ID);
        return EffectResponse.Success(request.ID);
    }

    public override EffectResponse? Tick(EffectRequest request)
    {
        playerInput.ForceKick();
        return EffectResponse.Success(request.ID);
    }
}