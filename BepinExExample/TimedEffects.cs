using System.Collections.Concurrent;
using ConnectorLib.JSON;
using UnityEngine;

namespace CrowdControl;

/// <summary>
/// Manages the execution of a timed effect.
/// </summary>
public class TimedThread
{
    private readonly ControlClient client;
    public static ConcurrentDictionary<uint, TimedThread> threads = new();

    public readonly Timed effect;
    public long duration;
    public long timeRemaining;
    public uint id;
    public bool paused;

    public bool running;

    /// <summary>
    /// Checks if a timed effect of the specified type is currently running.
    /// </summary>
    /// <param name="t">The type of the timed effect.</param>
    /// <returns><c>true</c> if a timed effect of the specified type is running; otherwise, <c>false</c>.</returns>
    public static bool IsRunning(TimedType t)
    {
        foreach (var thread in threads.Values)
            if (thread.effect.type == t) return true;
        return false;
    }

    /// <summary>
    /// Pauses all running timed effects.
    /// </summary>
    public static void PauseAll()
    {
        foreach (var thread in threads.Values) thread.Pause();
    }

    /// <summary>
    /// Pauses the current timed effect.
    /// </summary>
    public void Pause()
    {
        if (!running) return;
        client.Send(new EffectResponse(id, EffectStatus.Paused, timeRemaining));
        paused = true;
    }

    /// <summary>
    /// Unpauses all paused timed effects.
    /// </summary>
    public static void UnpauseAll()
    {
        foreach (var thread in threads.Values) thread.Unpause();
    }

    /// <summary>
    /// Unpauses the current timed effects.
    /// </summary>
    public void Unpause()
    {
        if (!running) return;
        client.Send(new EffectResponse(id, EffectStatus.Resumed, timeRemaining));
        paused = false;
    }

    /// <summary>
    /// Advances the time of the current timed effect and executes the effect.
    /// </summary>
    public void Tick()
    {
        if (paused || (!running)) return;
        if (timeRemaining > 0) effect.tick();
        else
        {
            effect.removeEffect();
            threads.TryRemove(id, out _);
            client.Send(new EffectResponse(id, EffectStatus.Finished, 0));
            running = false;
        }

        timeRemaining -= (long)(Time.fixedDeltaTime * 1000f);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TimedThread"/> class.
    /// </summary>
    /// <param name="client">The control client.</param>
    /// <param name="id">The ID of the timed effect.</param>
    /// <param name="type">The type of the timed effect.</param>
    /// <param name="duration">The duration of the timed effect.</param>
    public TimedThread(ControlClient client, uint id, TimedType type, long duration)
    {
        this.client = client;
        effect = new(type);
        this.duration = duration;
        timeRemaining = duration;
        this.id = id;
        paused = false;
        running = false;

        threads[id] = this;
    }

    /// <summary>
    /// Runs the timed thread and starts the execution of the effect.
    /// </summary>
    public void Run()
    {
        effect.addEffect();
        timeRemaining = duration;
        running = true;
    }
}
