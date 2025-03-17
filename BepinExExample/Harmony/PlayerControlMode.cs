using HarmonyLib;

namespace CrowdControl.Harmony;

[HarmonyPatch]
public static class PlayerControlMode_Harmony
{
    // Patch for getting the Gameplay mode
    [HarmonyPatch(typeof(PlayerControlMode), "get_Gameplay"), HarmonyPostfix]
    public static void GetGameplay_Postfix(PlayerControlMode? __result)
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
    [HarmonyPatch(typeof(PlayerControlMode), "get_Conversation"), HarmonyPostfix]
    public static void GetConversation_Postfix(PlayerControlMode? __result)
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