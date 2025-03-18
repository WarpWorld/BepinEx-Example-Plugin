using HarmonyLib;

namespace CrowdControl.Harmony;

/// <summary>
/// This is a game-specific example Harmony patch for Anger Foot's PlayerControlMode class.
/// This patch is used to track the game's current state and update the GameStateManager accordingly.
/// </summary>
/// <remarks>
/// These functions are usually called on the main game thread, depending on implementation.
/// Blocking here may cause lag or crash the game entirely.
/// </remarks>
[HarmonyPatch]
public static class PlayerControlMode
{
    // Patch for getting the Gameplay mode
    [HarmonyPatch(typeof(global::PlayerControlMode), "get_Gameplay"), HarmonyPostfix]
    public static void GetGameplay_Postfix(global::PlayerControlMode? __result)
    {
        if (__result != null && __result.name == "Gameplay")
        {
            if (CCMod.Instance.GameStateManager.IsActiveInConversation)
            {
                CCMod.Instance.GameStateManager.IsActiveInConversation = false;
            }
        }
    }

    // Patch for getting the Conversation mode
    [HarmonyPatch(typeof(global::PlayerControlMode), "get_Conversation"), HarmonyPostfix]
    public static void GetConversation_Postfix(global::PlayerControlMode? __result)
    {
        if (__result != null && __result.name == "Conversation")
        {
            if (!CCMod.Instance.GameStateManager.IsActiveInConversation)
            {
                CCMod.Instance.GameStateManager.IsActiveInConversation = true;
            }
        }
    }

}