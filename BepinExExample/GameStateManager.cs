using System.Runtime.CompilerServices;
using ConnectorLib.JSON;
using Framework;
using UnityEngine;

namespace CrowdControl;

public class GameStateManager(CrowdControlMod mod)
{
    //Everything in the game-specific region will need to be changed for each game
    
    #region Game-Specific Code
    
    //Some of the effect functions call this to display a message to the player so we've included that as a helper here
    //You can change this to use your own dialog system if you have one or remove it if you don't want to display messages
    public static async Task DialogMsgAsync(string message, bool playSound)
    {
        TutorialText text = TutorialText.Instance;
        if (!text) return;
        //prevent dialog from overwriting game dialog
        //may be a isReady check?
        //if (text.CurrentPrompt) return;

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

    //Anger Foot doesn't directly report this information, so we track it in Harmony.PlayerControlMode.cs and update it here
    //These can be added or removed as needed
    public bool IsActiveInConversation { get; set; }

    /// <summary>Checks if the game is in a state where effects can be applied.</summary>
    /// <param name="code">The effect codename the caller is intending to apply.</param>
    /// <returns>True if the game is in a state where the effect can be applied, false otherwise.</returns>
    /// <remarks>
    /// The <paramref name="code"/> parameter is not normally checked.
    /// Use this is you want to exempt certain effects from checks (e.g. debug or "fix-it" effects).
    /// </remarks>
    public bool IsReady(string code = "") => GetGameState() == ConnectorLib.JSON.GameState.Ready;

    /// <summary>Gets the current game state as it pertains to the firing of effects.</summary>
    /// <returns>The current game state.</returns>
    public ConnectorLib.JSON.GameState GetGameState()
    {
        try
        {
            GameConfig config = GameConfig.Instance;
            if (!config || !config.GetCurrentLevel())
                return ConnectorLib.JSON.GameState.WrongMode;

            if (!config.IsLevelGameplayLevel(config.GetCurrentLevel()))
                return ConnectorLib.JSON.GameState.SafeArea;
            if (mod.GameStateManager.IsActiveInConversation)
                return ConnectorLib.JSON.GameState.Cutscene;

            if (SingletonBehaviour<GameplayManager>.Instance.CurrentLevelStats.LevelTime < 1.0)
                return ConnectorLib.JSON.GameState.BadPlayerState;

            if (GameState.IsGamePausedOrNotFocused)
                return ConnectorLib.JSON.GameState.Paused;

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

    /// <summary>Reports the updated game state to the Crowd Control client.</summary>
    /// <param name="force">True to force the report to be sent, even if the state is the same as the previous state, false to only report the state if it has changed.</param>
    /// <returns>True if the data was sent successfully, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool UpdateGameState(bool force = false) => UpdateGameState(GetGameState(), force);

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
        if (force || (_last_game_state != newState))
        {
            _last_game_state = newState;
            return mod.Client.Send(new GameUpdate(newState, message));
        }

        return true;
    }

    #endregion
}