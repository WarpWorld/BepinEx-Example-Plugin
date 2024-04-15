using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CrowdControl;

/// <summary>
/// The main plugin object. Try to instantiate only one of these.
/// </summary>
[BepInPlugin(modGUID, modName, modVersion)]
public class TestMod : BaseUnityPlugin
{
    // Mod Details
    private const string modGUID = "WarpWorld.CrowdControl";
    private const string modName = "Crowd Control";
    private const string modVersion = "1.0.0.0";

    private readonly Harmony harmony = new(modGUID);

    private ControlClient m_client = null!;

    private static ManualLogSource? mls;
    public static void Log(LogLevel level, object data) => mls?.Log(level, data);
    public static void LogDebug(object data) => mls?.LogDebug(data);
    public static void LogError(object data) => mls?.LogError(data);
    public static void LogFatal(object data) => mls?.LogFatal(data);
    public static void LogInfo(object data) => mls?.LogInfo(data);
    public static void LogMessage(object data) => mls?.LogMessage(data);
    public static void LogWarning(object data) => mls?.LogWarning(data);

    void Awake()
    {
        //Instance = this;

        mls ??= BepInEx.Logging.Logger.CreateLogSource("Crowd Control");
        mls.LogInfo($"Loaded {modGUID}. Patching.");
        harmony.PatchAll(typeof(TestMod));

        mls.LogInfo("Initializing Crowd Control");

        try
        {
            m_client = new ControlClient(this);
            m_client.DoEvents().Forget();
        }
        catch (Exception e) { mls.LogInfo($"CC Init Error: {e}"); }

        mls.LogInfo("Crowd Control Initialized");
        //mls = Logger;
    }

    void FixedUpdate() => m_client.UpdateTick(); // this is correct, these use the XNA names which are Update & Draw rather than FixedUpdate and Update

    void Update() => m_client.DrawTick(); // this is correct, these use the XNA names which are Update & Draw rather than FixedUpdate and Update
}