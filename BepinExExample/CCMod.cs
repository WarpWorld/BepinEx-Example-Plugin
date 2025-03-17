using BepInEx;
using BepInEx.Logging;
using ConnectorLib.JSON;

namespace CrowdControl;

/// <summary>
/// The main Crowd Control mod class.
/// </summary>
[BepInPlugin(modGUID, modName, modVersion)]
public class CCMod : BaseUnityPlugin
{
    // Mod Details
    public const string modGUID = "WarpWorld.CrowdControl";
    public const string modName = "Crowd Control";
    public const string modVersion = "1.0.0.0";

    private readonly HarmonyLib.Harmony harmony = new(modGUID);

    public static ManualLogSource mls;

    internal static CCMod Instance;

    public GameStateManager GameStateManager;

    /// <summary>
    /// Gets a value indicating whether the client is connected.
    /// </summary>
    public bool ClientConnected => m_client.Connected;
    private ControlClient? m_client;

    /// <summary>
    /// Called when the mod is awakened.
    /// </summary>
    void Awake()
    {
        Instance = this;
        mls = BepInEx.Logging.Logger.CreateLogSource("Crowd Control");

        mls.LogInfo($"Loaded {modGUID}. Patching.");
        harmony.PatchAll(typeof(CCMod));
        harmony.PatchAll();

        mls.LogInfo("Initializing Crowd Control");

        try
        {
            GameStateManager = new(this);
            m_client = new(this);
        }
        catch (Exception e)
        {
            mls.LogError($"CC Init Error: {e}");
        }

        mls.LogInfo("Crowd Control Initialized");

        mls = Logger;
    }

    /// <summary>
    /// Called every fixed frame update.
    /// </summary>
    void FixedUpdate()
    {
        if (ActionQueue.Count > 0)
        {
            Action action = ActionQueue.Dequeue();
            action.Invoke();
        }

        lock (TimedThread.threads)
        {
            foreach (var thread in TimedThread.threads.Values)
                thread.Tick();
        }

        m_client?.FixedUpdate();
    }

    /***** == ONLY USE THIS IF FixedUpdate() ISN'T ALREADY BEING CALLED EVERY TICK == *****/
    //attach this to some game class with a function that runs every frame like the player's Update()
    //[HarmonyPatch(typeof(Player), "FixedUpdate"), HarmonyPrefix]
    //private static void FixedUpdate_Harmony() => Instance.FixedUpdate();

    /// <summary>
    /// The action queue for executing actions.
    /// </summary>
    public static Queue<Action> ActionQueue = new();

    /// <summary>
    /// Enables the specified effects.
    /// </summary>
    /// <param name="ids">The IDs of the effects to enable.</param>
    /// <returns><c>true</c> if the effects were enabled successfully; otherwise, <c>false</c>.</returns>
    public bool EnableEffects(params IEnumerable<string> ids) => m_client?.EnableEffects(ids) ?? false;

    /// <summary>
    /// Disables the specified effects.
    /// </summary>
    /// <param name="ids">The IDs of the effects to disable.</param>
    /// <returns><c>true</c> if the effects were disabled successfully; otherwise, <c>false</c>.</returns>
    public bool DisableEffects(params IEnumerable<string> ids) => m_client?.DisableEffects(ids) ?? false;

    /// <summary>
    /// Sends a simple JSON response.
    /// </summary>
    /// <param name="response">The response to send.</param>
    /// <returns><c>true</c> if the response was sent successfully; otherwise, <c>false</c>.</returns>
    public bool Send(SimpleJSONResponse response) => m_client?.Send(response) ?? false;
}
