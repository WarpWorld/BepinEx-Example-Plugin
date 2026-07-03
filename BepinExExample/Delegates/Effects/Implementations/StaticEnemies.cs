namespace CrowdControl.Delegates.Effects.Implementations;

/* == EXAMPLE (Anger Foot) - a timed effect with cross-effect conflicts ==
 * See PassiveEnemies.cs for a full explanation of this pattern.
 * Uncomment and adapt this for your game.

using ConnectorLib.JSON;

[Effect(
    id: "static_enemies",
    defaultDuration: 30,
    conflicts: ["passive_enemies", "static_enemies"])
]
public class StaticEnemies(CrowdControlMod mod, NetworkClient client) : Effect(mod, client)
{
    public override EffectResponse Start(EffectRequest request)
    {
        //setting conflicts above means that this should only hit if the player is just playing with passive or static enemies on
        //this would never run if the effects were already running since it would be blocked by the conflict check
        //as such, we assume a player just playing with passive or static enemies on isn't going to be turning it off in a few seconds
        //and just fail here rather than retrying waiting for something that probably isn't happening
        if (Cheats.PassiveEnemies || Cheats.StaticEnemies) return EffectResponse.Failure(request.ID, StandardErrors.AlreadyInState);

        Cheats.StaticEnemies = true;
        GameStateManager.DialogMsgAsync($"{request.GetViewerDisplayName()} enabled Static Enemies", true).Forget();

        return EffectResponse.Success(request.ID);
    }

    public override EffectResponse? Stop(EffectRequest request)
    {
        //already off somehow(?), just abort with success since our job here is done
        if (!Cheats.StaticEnemies) return EffectResponse.Finished(request.ID);

        Cheats.StaticEnemies = false;
        GameStateManager.DialogMsgAsync("Static Enemies Ended", true).Forget();

        return EffectResponse.Finished(request.ID);
    }
}

*/
