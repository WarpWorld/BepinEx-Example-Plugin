namespace CrowdControl.Harmony;

/* == EXAMPLE (Anger Foot) - a Harmony patch adding debug commands to the game's dev console ==
 * This adds two commands to Anger Foot's built-in terminal:
 *   crowdcontrol       - prints the mod version and connection status
 *   crowdcontrol-reset - re-enables all effects on the Crowd Control menu
 * This is a development/debugging convenience only and is not required for the mod to work.
 *
 * These functions are usually called on the main game thread, depending on implementation.
 * Blocking here may cause lag or crash the game entirely.
 *
 * Uncomment and adapt this for your game if it has a similar developer console.

using System.Reflection;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
public static class Terminal
{
    [HarmonyPatch(typeof(global::Terminal), "ExecuteCommand"), HarmonyPrefix]
    static bool Prefix(ref string command, ref string[] parameters)
    {
        if (command == "crowdcontrol")
        {
            Type terminalType = typeof(global::Terminal);
            MethodInfo addLineMethod = terminalType.GetMethod("AddLine", BindingFlags.NonPublic | BindingFlags.Static);
            if (addLineMethod != null)
            {
                object[] ccVersion = { CrowdControlMod.MOD_NAME + " " + CrowdControlMod.MOD_VERSION, Color.green };
                Color statusColor = CrowdControlMod.Instance.ClientConnected ? Color.green : Color.red;
                object[] ccStatus = { "Connected:" + CrowdControlMod.Instance.ClientConnected, statusColor };
                addLineMethod.Invoke(null, ccVersion);
                addLineMethod.Invoke(null, ccStatus);
            }
            return false;
        }

        if (command == "crowdcontrol-reset")
        {
            Type terminalType = typeof(global::Terminal);
            MethodInfo addLineMethod = terminalType.GetMethod("AddLine", BindingFlags.NonPublic | BindingFlags.Static);
            if (addLineMethod != null)
            {
                object[] addLineParams = { "State Reset!", Color.red };
                addLineMethod.Invoke(null, addLineParams);
            }

            CrowdControlMod.Instance.Client.EnableAllEffects();
            return false;
        }
        return true;
    }
}

*/
