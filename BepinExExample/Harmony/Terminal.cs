using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CrowdControl.Harmony;

[HarmonyPatch]
public static class Terminal_Harmony
{
    [HarmonyPatch(typeof(Terminal), "ExecuteCommand"), HarmonyPrefix]
    static bool Prefix(ref string command, ref string[] parameters)
    {
        if (command == "crowdcontrol")
        {
            Type terminalType = typeof(Terminal);
            MethodInfo addLineMethod = terminalType.GetMethod("AddLine", BindingFlags.NonPublic | BindingFlags.Static);
            if (addLineMethod != null)
            {
                object[] ccVersion = { CCMod.modName + " " + CCMod.modVersion, Color.green };
                Color statusColor = CCMod.Instance.ClientConnected ? Color.green : Color.red;
                object[] ccStatus = { "Connected:" + CCMod.Instance.ClientConnected, statusColor };
                addLineMethod.Invoke(null, ccVersion);
                addLineMethod.Invoke(null, ccStatus);
            }
            return false;
        }

        if (command == "crowdcontrol-reset")
        {
            Type terminalType = typeof(Terminal);
            MethodInfo addLineMethod = terminalType.GetMethod("AddLine", BindingFlags.NonPublic | BindingFlags.Static);
            if (addLineMethod != null)
            {
                object[] addLineParams = { "State Reset!", Color.red };
                addLineMethod.Invoke(null, addLineParams);
            }


            CCMod.Instance.GameStateManager.EnableAllEffects();
            return false;
        }
        return true;
    }
}