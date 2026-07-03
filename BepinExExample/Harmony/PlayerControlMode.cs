namespace CrowdControl.Harmony;

/* == EXAMPLE (Anger Foot) - a Harmony patch used to track game state ==
 * Anger Foot doesn't directly expose whether the player is in a conversation, so this patch
 * observes the game's own control-mode lookups and records the result on the GameStateManager,
 * where GetGameState() uses it to report the Cutscene state.
 *
 * These functions are usually called on the main game thread, depending on implementation.
 * Blocking here may cause lag or crash the game entirely.
 *
 * Uncomment and adapt this for your game (the IsActiveInConversation property it writes to
 * is part of the commented example code in GameStateManager.cs).

using HarmonyLib;

[HarmonyPatch]
public static class PlayerControlMode
{
    // Patch for getting the Gameplay mode
    [HarmonyPatch(typeof(global::PlayerControlMode), "get_Gameplay"), HarmonyPostfix]
    public static void GetGameplay_Postfix(global::PlayerControlMode? __result)
    {
        if (__result != null && __result.name == "Gameplay")
        {
            if (CrowdControlMod.Instance.GameStateManager.IsActiveInConversation)
            {
                CrowdControlMod.Instance.GameStateManager.IsActiveInConversation = false;
            }
        }
    }

    // Patch for getting the Conversation mode
    [HarmonyPatch(typeof(global::PlayerControlMode), "get_Conversation"), HarmonyPostfix]
    public static void GetConversation_Postfix(global::PlayerControlMode? __result)
    {
        if (__result != null && __result.name == "Conversation")
        {
            if (!CrowdControlMod.Instance.GameStateManager.IsActiveInConversation)
            {
                CrowdControlMod.Instance.GameStateManager.IsActiveInConversation = true;
            }
        }
    }
}

*/
