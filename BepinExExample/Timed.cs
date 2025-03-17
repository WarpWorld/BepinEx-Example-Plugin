using System.Reflection;
using UnityEngine;
using Object = System.Object;

namespace CrowdControl;

public class Timed(TimedType t)
{
    public TimedType type = t;
    float old;
    int _orginalFOV = 120;

    public void addEffect()
    {
        switch (type)
        {

            case TimedType.GOD_MODE:
            {

                CCMod.ActionQueue.Enqueue(async () =>
                {
                    Cheats.GodMode = true;
                    await DialogMsgAsync("God Mode Enabled", true);
                });
                break;
            }
            case TimedType.INFINITE_AMMO:
            {

                CCMod.ActionQueue.Enqueue(async () =>
                {
                    Cheats.InfiniteAmmo = true;
                    await DialogMsgAsync("Infinte Ammo Enabled", true);
                });
                break;
            }
            case TimedType.NO_MUSIC:
            {

                CCMod.ActionQueue.Enqueue(() =>
                {
                    Cheats.NoMusic = true;
                });
                break;
            }
            case TimedType.NO_UI:
            {

                CCMod.ActionQueue.Enqueue(() =>
                {
                    Cheats.NoUI = true;
                });
                break;
            }
            case TimedType.PASSIVE_ENEMIES:
            {

                CCMod.ActionQueue.Enqueue(async () =>
                {
                    Cheats.PassiveEnemies = true;
                    await DialogMsgAsync("Passive Enemies Enabled", true);

                });
                break;
            }
            case TimedType.STATIC_ENEMIES:
            {

                CCMod.ActionQueue.Enqueue(async () =>
                {
                    Cheats.StaticEnemies = true;
                    await DialogMsgAsync("Static Enemies Enabled", true);
                });
                break;
            }
            case TimedType.INSANE_FOV:
            {

                CCMod.ActionQueue.Enqueue(() =>
                {
                    _orginalFOV = GameSettings.FieldOfView;
                    GameSettings.FieldOfView = 160;
                });
                break;
            }
            case TimedType.SMALL_FOV:
            {

                CCMod.ActionQueue.Enqueue(() =>
                {
                    _orginalFOV = GameSettings.FieldOfView;
                    GameSettings.FieldOfView = 20;
                });
                break;
            }
            case TimedType.DRUNK_CAMERA:
            {

                CCMod.ActionQueue.Enqueue(() =>
                {



                });
                break;
            }
        }
    }

    public void removeEffect()
    {
        switch (type)
        {
            case TimedType.GOD_MODE:
            {

                CCMod.ActionQueue.Enqueue(async () =>
                {
                    Cheats.GodMode = false;
                    await DialogMsgAsync("God Mode Ended", true);
                });
                break;
            }
            case TimedType.INFINITE_AMMO:
            {
                CCMod.ActionQueue.Enqueue(async () =>
                {
                    Cheats.InfiniteAmmo = false;
                    await DialogMsgAsync("Infinte Ammo Ended", true);
                });
                break;
            }
            case TimedType.NO_MUSIC:
            {

                CCMod.ActionQueue.Enqueue(() =>
                {
                    Cheats.NoMusic = false;
                });
                break;
            }
            case TimedType.NO_UI:
            {

                CCMod.ActionQueue.Enqueue(() =>
                {
                    Cheats.NoUI = false;
                });
                break;
            }
            case TimedType.PASSIVE_ENEMIES:
            {

                CCMod.ActionQueue.Enqueue(async () =>
                {
                    Cheats.PassiveEnemies = false;
                    await DialogMsgAsync("Passive Enemies Ended", true);
                });
                break;
            }
            case TimedType.STATIC_ENEMIES:
            {

                CCMod.ActionQueue.Enqueue(async () =>
                {
                    Cheats.StaticEnemies = true;
                    await DialogMsgAsync("Static Enemies Ended", true);
                });
                break;
            }
            case TimedType.INSANE_FOV:
            {

                CCMod.ActionQueue.Enqueue(() =>
                {
                    GameSettings.FieldOfView = _orginalFOV;
                });
                break;
            }
            case TimedType.SMALL_FOV:
            {

                CCMod.ActionQueue.Enqueue(() =>
                {
                    GameSettings.FieldOfView = _orginalFOV;
                });
                break;
            }
        }
    }

    private static async Task DialogMsgAsync(string message, bool playSound)
    {
        TutorialText text = TutorialText.Instance;
        if (!text) return;
        //prevent dialog from overwriting game dialog
        //may be a isReady check?
        //if (text.CurrentPrompt) return;

        LocalizedString localizedString = ScriptableObject.CreateInstance<LocalizedString>();
        if (!localizedString) return;
        setProperty(localizedString, "_englishText", message);

        TutorialPrompt prompt = new();
        if (!prompt) return;
        prompt.Text = localizedString;
        prompt.PlaySound = playSound;

        text.SetPrompt(prompt);
        await Task.Delay(2000);
        text.ClearPrompt();
    }

    public static void setProperty(Object a, string prop, Object val)
    {
        var f = a.GetType().GetField(prop, BindingFlags.Instance | BindingFlags.NonPublic);
        f.SetValue(a, val);
    }

    public void tick()
    {
        switch (type)
        {
            case TimedType.FORCE_KICK:
            {

                CCMod.ActionQueue.Enqueue(() =>
                {
                    PlayerInput playerInput = GameObject.FindObjectOfType<PlayerInput>();
                    //MethodInfo forceKickMethod = playerInput.GetType().GetMethod("ForceKick", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    playerInput.ForceKick();
                            
                });
                break;
            }

        }
    }
}