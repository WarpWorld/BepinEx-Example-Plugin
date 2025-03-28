﻿using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace CrowdControl.Harmony;

/// <summary>
/// This is a game-specific example Harmony patch for Anger Foot's Terminal class.
/// </summary>
/// <remarks>
/// These functions are usually called on the main game thread, depending on implementation.
/// Blocking here may cause lag or crash the game entirely.
/// </remarks>
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