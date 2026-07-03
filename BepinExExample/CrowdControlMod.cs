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
    // Mod Details - ALWAYS SET THESE FOR A NEW GAME!
    // (these must be compile-time constants for the [BepInPlugin] attribute, so they can't live in the
    // csproj - the DLL name is set separately via the GameName property in BepinExExample.csproj)
    public const string MOD_GUID = "WarpWorld.CrowdControl"; //unique BepInEx plugin ID - fine to leave as-is since only one Crowd Control mod is installed per game
    public const string MOD_NAME = "Crowd Control for Anger Foot"; //display name shown in the BepInEx log - put your game's name here
    public const string MOD_VERSION = "1.0.0.0"; //bump this with each release of your mod
    
    /// <summary>The real-time duration of the current tick, used to advance timed effect countdowns.</summary>
    /// <remarks>
    /// Dividing by the time scale makes timed effects count down in real time even during slow motion.
    /// The time scale is checked to avoid Infinity/NaN corrupting effect timers in games that
    /// run FixedUpdate with the time scale at (or below) zero.
    /// Change this to use Time.deltaTime if ticking from Update instead of FixedUpdate.
    /// </remarks>
    public static float DeltaTime => (Time.timeScale > 0f) ? (Time.fixedDeltaTime / Time.timeScale) : 0f;

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

    public NetworkClient Client { get; private set; } = null!;
    
    public Scheduler Scheduler { get; private set; } = null!;

    private const double MANUAL_RECONNECT_COOLDOWN_SECONDS = 5.0;
    private DateTime m_nextManualReconnectAllowedUtc = DateTime.MinValue;

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

    void OnApplicationQuit()
    {
        try
        {
            Client?.Stop();
            Client?.Dispose();
        }
        catch {/**/}
    }

    void OnDestroy()
    {
        try
        {
            Client?.Stop();
            Client?.Dispose();
        }
        catch {/**/}
    }

    /// <summary>Called every fixed frame (physics) update.</summary>
    /// <remarks>This function is called on the main game thread. Blocking here may cause lag or crash the game entirely.</remarks>
    void FixedUpdate()
    {
        if (GameStateManager == null) return; //initialization failed - do nothing rather than throw every tick

        //recompute the game state once per tick (everything else this tick reads the cached value)
        //and report it if it changed - state changes reach the Crowd Control client within one tick
        GameStateManager.InvalidateStateCache();
        GameStateManager.UpdateGameState();

        Scheduler?.Tick();
    }

    /// <summary>Called every rendered frame.</summary>
    void Update()
    {
        HandleManualReconnectHotkey();
    }

    private void HandleManualReconnectHotkey()
    {
        if (!Input.GetKeyDown(KeyCode.F9))
            return;

        DateTime now = DateTime.UtcNow;
        if (now < m_nextManualReconnectAllowedUtc)
            return;

        m_nextManualReconnectAllowedUtc = now.AddSeconds(MANUAL_RECONNECT_COOLDOWN_SECONDS);
        Logger.LogInfo("F9 pressed - manual Crowd Control reconnect requested.");

        if (Client?.RequestReconnect() == true)
        {
            ShowGameUiMessage("Reconnecting to Crowd Control...");
            Logger.LogInfo("Manual Crowd Control reconnect queued.");
        }
        else
        {
            ShowGameUiMessage("Crowd Control client not found.");
            Logger.LogInfo("Manual Crowd Control reconnect skipped because the Crowd Control client was not found.");
        }
    }

    /// <summary>
    /// Displays a message to the player using the game's UI/toast system.
    /// </summary>
    /// <remarks>
    /// This is intentionally a no-op in the example pack because UI/toast APIs are game-specific.
    /// Wire this to your game's toast, subtitle, HUD message, or dialog system when available.
    /// </remarks>
    public void ShowGameUiMessage(string message)
    {
        //TODO: Replace this with your game's UI/toast call, e.g. ToastManager.Show(message).
    }

    /// <summary>Called by Unity when the application gains or loses focus.</summary>
    /// <remarks>
    /// This pushes a game state update immediately rather than waiting for the next polling interval,
    /// which matters because FixedUpdate may stop running entirely while the game is unfocused.
    /// </remarks>
    void OnApplicationFocus(bool hasFocus)
    {
        try
        {
            GameStateManager?.InvalidateStateCache(); //the cached state predates the focus change
            GameStateManager?.UpdateGameState();
        }
        catch {/**/}
    }

    /// <summary>Called by Unity when the application is paused or resumed by the OS.</summary>
    /// <remarks><inheritdoc cref="OnApplicationFocus" path="/remarks"/></remarks>
    void OnApplicationPause(bool isPaused)
    {
        try
        {
            GameStateManager?.InvalidateStateCache(); //the cached state predates the pause change
            GameStateManager?.UpdateGameState();
        }
        catch {/**/}
    }

    /***** == ONLY USE THIS IF FixedUpdate() ISN'T ALREADY BEING CALLED EVERY TICK == *****/
    //attach this to some game class with a function that runs every frame like the player's Update()
    //[HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.FixedUpdate))]
    //private class PlayerMovement_FixedUpdate { static void Prefix() => Instance.FixedUpdate(); }
}
