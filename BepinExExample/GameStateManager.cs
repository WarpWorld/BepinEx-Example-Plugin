using System.Runtime.CompilerServices;
using ConnectorLib.JSON;
using UnityEngine;

namespace CrowdControl;

public class GameStateManager(CrowdControlMod mod)
{
    //Everything in the game-specific region will need to be changed for each game

    #region Game-Specific Code

    /* == EXAMPLE (Anger Foot) - a helper for showing on-screen messages to the player ==
     * Some of the example effects call this to display a message using the game's own dialog system.
     * Change this to use your game's dialog/toast system, or remove it if you don't want to display messages.

    public static async Task DialogMsgAsync(string message, bool playSound)
    {
        TutorialText text = TutorialText.Instance;
        if (!text) return;

        LocalizedString localizedString = ScriptableObject.CreateInstance<LocalizedString>();
        if (!localizedString) return;
        localizedString.SetField("_englishText", message);

        TutorialPrompt prompt = new();
        if (!prompt) return;
        prompt.Text = localizedString;
        prompt.PlaySound = playSound;

        text.SetPrompt(prompt);
        await Task.Delay(2000);
        text.ClearPrompt();
    }

    //Anger Foot doesn't directly report conversation state, so we track it in Harmony\PlayerControlMode.cs and update it here
    //Flags like this can be added or removed as needed for your game
    public bool IsActiveInConversation { get; set; }

    */

    /// <summary>
    /// True to report <see cref="ConnectorLib.JSON.GameState.NotFocused"/> (blocking effects) while the game
    /// window is not in the foreground, false to ignore focus entirely.
    /// </summary>
    /// <remarks>
    /// Most games should leave this on so effects don't fire (and timed effects pause) while the player is
    /// alt-tabbed. Set this to false for games that keep running normally in the background (e.g. games with
    /// "Run In Background" enabled where streamers commonly play while interacting with chat in another window).
    /// </remarks>
    public const bool CARE_ABOUT_FOCUS = true;

    /// <summary>Checks if the game is in a state where effects can be applied.</summary>
    /// <param name="code">The effect codename the caller is intending to apply.</param>
    /// <returns>True if the game is in a state where the effect can be applied, false otherwise.</returns>
    /// <remarks>
    /// The <paramref name="code"/> parameter is not normally checked.
    /// Use this if you want to exempt certain effects from checks (e.g. debug or "fix-it" effects).
    /// </remarks>
    public bool IsReady(string code = "") => CurrentState == ConnectorLib.JSON.GameState.Ready;

    /// <summary>Computes the current game state as it pertains to the firing of effects.</summary>
    /// <returns>The current game state.</returns>
    /// <remarks>
    /// This must be called from the main game thread only - it is expected to touch game/Unity APIs.
    /// Report state changes as precisely as your game allows (Paused, NotFocused, Menu, Loading, Cutscene, ...)
    /// so the Crowd Control client and effect pack always know what's going on.
    /// Prefer reading <see cref="CurrentState"/> over calling this directly - it caches the result
    /// so the state is only computed once per game tick regardless of how many callers query it.
    /// </remarks>
    public ConnectorLib.JSON.GameState GetGameState()
    {
        try
        {
            //these two checks are game-agnostic and can usually be kept as-is
            //set CARE_ABOUT_FOCUS to false (above) if effects should keep running while the game is unfocused
#pragma warning disable CS0162 // Unreachable code detected - CARE_ABOUT_FOCUS is a compile-time constant
            if (CARE_ABOUT_FOCUS && !Application.isFocused)
                return ConnectorLib.JSON.GameState.NotFocused;
#pragma warning restore CS0162

            //most Unity games set the time scale to 0 while paused - replace this with your game's own pause flag if it has one
            if (Time.timeScale == 0f)
                return ConnectorLib.JSON.GameState.Paused;

            /* == EXAMPLE (Anger Foot) - game-specific state checks ==

            GameConfig config = GameConfig.Instance;
            if (!config || !config.GetCurrentLevel())
                return ConnectorLib.JSON.GameState.WrongMode; //title screen or non-gameplay scene

            if (!config.IsLevelGameplayLevel(config.GetCurrentLevel()))
                return ConnectorLib.JSON.GameState.SafeArea; //hub or other non-gameplay level

            if (IsActiveInConversation)
                return ConnectorLib.JSON.GameState.Cutscene; //talking to an NPC

            if (SingletonBehaviour<GameplayManager>.Instance.CurrentLevelStats.LevelTime < 1.0)
                return ConnectorLib.JSON.GameState.BadPlayerState; //level just started or player just respawned

            if (GameState.IsGamePausedOrNotFocused)
                return ConnectorLib.JSON.GameState.Paused;

            */

            //TODO: add your game-specific state checks here (see the commented example above)

            return ConnectorLib.JSON.GameState.Ready;
        }
        catch (Exception e)
        {
            CrowdControlMod.Instance.Logger.LogError($"ERROR {e}");
            return ConnectorLib.JSON.GameState.Error;
        }
    }

    #endregion

    //Everything from here down is the same for every game - you probably don't need to change it

    #region General Code

    //caches the result of GetGameState() for the duration of one game tick - the state is computed at most
    //once per tick no matter how many effects and requests query it, keeping GetGameState() cheap to call
    private ConnectorLib.JSON.GameState? m_cachedState;

    /// <summary>Gets the current game state, computing it at most once per game tick.</summary>
    /// <remarks>
    /// Must be called from the main game thread only.
    /// The cache is invalidated at the start of every tick by <see cref="CrowdControlMod.FixedUpdate"/>
    /// (and by the focus/pause handlers) - callers always see a value from the current tick.
    /// </remarks>
    public ConnectorLib.JSON.GameState CurrentState => m_cachedState ??= GetGameState();

    /// <summary>Discards the cached game state so the next query recomputes it.</summary>
    public void InvalidateStateCache() => m_cachedState = null;

    //set (from any thread) when a full state report needs to be sent regardless of whether the state changed,
    //e.g. right after the Crowd Control client (re)connects
    private volatile bool m_stateResendRequested;

    /// <summary>
    /// Requests that the next game state report be sent even if the state hasn't changed.
    /// </summary>
    /// <remarks>This is safe to call from any thread. The report itself is sent from the game thread.</remarks>
    public void RequestStateResend() => m_stateResendRequested = true;

    /// <summary>Reports the updated game state to the Crowd Control client.</summary>
    /// <param name="force">True to force the report to be sent, even if the state is the same as the previous state, false to only report the state if it has changed.</param>
    /// <returns>True if the data was sent successfully, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool UpdateGameState(bool force = false) => UpdateGameState(CurrentState, force);

    /// <summary>Reports the updated game state to the Crowd Control client.</summary>
    /// <param name="newState">The new game state to report.</param>
    /// <param name="force">True to force the report to be sent, even if the state is the same as the previous state, false to only report the state if it has changed.</param>
    /// <returns>True if the data was sent successfully, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool UpdateGameState(ConnectorLib.JSON.GameState newState, bool force) => UpdateGameState(newState, null, force);

    private ConnectorLib.JSON.GameState? _last_game_state;

    /// <summary>Reports the updated game state to the Crowd Control client.</summary>
    /// <param name="newState">The new game state to report.</param>
    /// <param name="message">The message to attach to the state report.</param>
    /// <param name="force">True to force the report to be sent, even if the state is the same as the previous state, false to only report the state if it has changed.</param>
    /// <returns>True if the data was sent successfully, false otherwise.</returns>
    public bool UpdateGameState(ConnectorLib.JSON.GameState newState, string? message = null, bool force = false)
    {
        if (m_stateResendRequested)
        {
            m_stateResendRequested = false;
            force = true;
        }

        if (force || (_last_game_state != newState))
        {
            _last_game_state = newState;
            return mod.Client.Send(new GameUpdate(newState, message));
        }

        return true;
    }

    #endregion
}
