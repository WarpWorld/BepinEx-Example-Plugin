namespace CrowdControl.Delegates.Effects.Implementations;

/* == EXAMPLE (Anger Foot) - a timed effect that acts every tick ==
 * This demonstrates using Tick(), which is called every scheduler tick while the effect is
 * running (and automatically skipped while it is paused).
 * Uncomment and adapt this for your game.

using ConnectorLib.JSON;
using Object = UnityEngine.Object;

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
        if (playerInput is null) return EffectResponse.Failure(request.ID, StandardErrors.PlayerNotFound);
        return EffectResponse.Success(request.ID);
    }

    public override EffectResponse? Tick(EffectRequest request)
    {
        playerInput.ForceKick();
        return null;
    }
}

*/
