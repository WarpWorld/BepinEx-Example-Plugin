using ConnectorLib.JSON;
using UnityEngine;

//Everything in the Effects namespace is free-form and just needs to have static methods with the Effect attribute
//non-effect helper methods are allowed and encouraged - kat
namespace CrowdControl.Delegates.Effects;

/// <summary>
/// Contains the effect delegates.
/// </summary>
/// <remarks>This entire file is game-specific and everything here (including the class itself) can be renamed or removed.</remarks>
public class EffectDelegates
{
    private static async Task DialogMsgAsync(string message, bool playSound)
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

    [Effect("level_complete")]
    public static EffectResponse CompleteLevel(ControlClient client, EffectRequest req)
    {

        EffectStatus status = EffectStatus.Success;
        string message = "";

        try
        {

            GameState.CompleteLevel();

        }
        catch (Exception e)
        {
            status = EffectStatus.Retry;
            CCMod.Instance.Logger.LogInfo($"Crowd Control Error: {e}");
        }

        return new(req.ID, status, message);
    }

    [Effect("level_restart")]
    public static EffectResponse RestartLevel(ControlClient client, EffectRequest req)
    {

        EffectStatus status = EffectStatus.Success;
        string message = "";

        try
        {
            DialogMsgAsync("Restart Level!", false);
            GameState.RestartCurrentLevel();

        }
        catch (Exception e)
        {
            status = EffectStatus.Retry;
            CCMod.Instance.Logger.LogInfo($"Crowd Control Error: {e}");
        }

        return new(req.ID, status, message);
    }

    [Effect("god_mode")]
    public static EffectResponse GodMode(ControlClient client, EffectRequest req)
    {
        long dur = req.duration > 0 ? req.duration.Value : 30_000;

        if (Cheats.GodMode) return new(req.ID, EffectStatus.Retry, "");
        if (TimedThread.IsRunning(TimedType.GOD_MODE)) return new(req.ID, EffectStatus.Retry, "");

        new Thread(new TimedThread(client, req.ID, TimedType.GOD_MODE, dur).Run).Start();
        return new(req.ID, EffectStatus.Success, dur);
    }

    [Effect("infinite_ammo")]
    public static EffectResponse InfiniteAmmo(ControlClient client, EffectRequest req)
    {
        long dur = req.duration > 0 ? req.duration.Value : 30_000;

        if (Cheats.InfiniteAmmo) return new(req.ID, EffectStatus.Retry, "");
        if (TimedThread.IsRunning(TimedType.INFINITE_AMMO)) return new(req.ID, EffectStatus.Retry, "");

        new Thread(new TimedThread(client, req.ID, TimedType.INFINITE_AMMO, dur).Run).Start();
        return new(req.ID, EffectStatus.Success, dur);
    }

    [Effect("passive_enemies")]
    public static EffectResponse PassiveEnemies(ControlClient client, EffectRequest req)
    {
        long dur = req.duration > 0 ? req.duration.Value : 30_000;

        if (TimedThread.IsRunning(TimedType.PASSIVE_ENEMIES)) return new(req.ID, EffectStatus.Retry, "");
        if (TimedThread.IsRunning(TimedType.STATIC_ENEMIES)) return new(req.ID, EffectStatus.Retry, "");
        if (Cheats.PassiveEnemies) return new(req.ID, EffectStatus.Retry, "");

        new Thread(new TimedThread(client, req.ID, TimedType.PASSIVE_ENEMIES, dur).Run).Start();
        return new(req.ID, EffectStatus.Success, dur);
    }

    [Effect("camera_drunk")]
    public static EffectResponse DrunkCamera(ControlClient client, EffectRequest req)
    {
        long dur = req.duration > 0 ? req.duration.Value : 30_000;

        if (TimedThread.IsRunning(TimedType.DRUNK_CAMERA)) return new(req.ID, EffectStatus.Retry, "");

        new Thread(new TimedThread(client, req.ID, TimedType.DRUNK_CAMERA, dur).Run).Start();
        return new(req.ID, EffectStatus.Success, dur);
    }
}