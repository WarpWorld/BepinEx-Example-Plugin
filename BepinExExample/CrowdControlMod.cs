using BepInEx;
using BepInEx.Logging;
using CrowdControl.Delegates.Effects;
using UnityEngine;

namespace CrowdControl;

/// <summary>
/// The main Crowd Control mod class.
/// </summary>
[BepInPlugin(MOD_GUID, MOD_NAME, MOD_VERSION)]
public class CrowdControlMod : BaseUnityPlugin
{
    // Mod Details
    public const string MOD_GUID = "WarpWorld.CrowdControl";
    public const string MOD_NAME = "Crowd Control";
    public const string MOD_VERSION = "1.0.0.0";

    private readonly HarmonyLib.Harmony harmony = new(MOD_GUID);

    /// <summary>The logger for the mod.</summary>
    public new ManualLogSource Logger => base.Logger;

    /// <summary>The singleton instance of the game mod.</summary>
    internal static CrowdControlMod Instance { get; private set; } = null!;

    /// <summary>The game state manager object.</summary>
    public GameStateManager GameStateManager { get; private set; } = null!;
    
    /// <summary>The effect class loader.</summary>
    public EffectLoader EffectLoader { get; private set; } = null!;

    /// <summary>
    /// Gets a value indicating whether the client is connected.
    /// </summary>
    public bool ClientConnected => Client.Connected;

    public NetworkClient Client { get; private set; }
    
    public Scheduler Scheduler { get; private set; }
    
    private const float GAME_STATUS_UPDATE_INTERVAL = 1f;
    private float m_gameStatusUpdateTimer;

    /// <summary>
    /// Called when the mod is awakened.
    /// </summary>
    void Awake()
    {
        Instance = this;

        Logger.LogInfo($"Loaded {MOD_GUID}. Patching.");
        harmony.PatchAll();

        Logger.LogInfo("Initializing Crowd Control");

        try
        {
            GameStateManager = new(this);
            Client = new(this);
            EffectLoader = new(this, Client);
            Scheduler = new(this, Client);
        }
        catch (Exception e)
        {
            Logger.LogError($"Crowd Control Init Error: {e}");
        }

        Logger.LogInfo("Crowd Control Initialized");
    }

    /// <summary>Called every fixed frame (physics) update.</summary>
    /// <remarks>This function is called on the main game thread. Blocking here may cause lag or crash the game entirely.</remarks>
    void FixedUpdate()
    {
        m_gameStatusUpdateTimer += Time.fixedDeltaTime;
        if (m_gameStatusUpdateTimer >= GAME_STATUS_UPDATE_INTERVAL)
        {
            GameStateManager.UpdateGameState();
            m_gameStatusUpdateTimer = 0f;
        }
        
        Scheduler?.Tick();
    }

    /***** == ONLY USE THIS IF FixedUpdate() ISN'T ALREADY BEING CALLED EVERY TICK == *****/
    //attach this to some game class with a function that runs every frame like the player's Update()
    //[HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.FixedUpdate))]
    //private class PlayerMovement_FixedUpdate { static void Prefix() => Instance.FixedUpdate(); }
}
