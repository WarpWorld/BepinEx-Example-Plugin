using System.Collections.Concurrent;
using UnityEngine;

namespace CrowdControl.Effects;

public abstract class Effect
{
    private static int _next_id = 0;
    private uint LocalID { get; } = unchecked((uint)Interlocked.Increment(ref _next_id));

    public abstract string Code { get; }

    protected bool _active = false;
    private readonly object _activity_lock = new();

    public SITimeSpan Elapsed { get; set; }

    public virtual EffectType Type => EffectType.Instant;

    public virtual SITimeSpan Duration => SITimeSpan.Zero;

    public virtual Type[] ParameterTypes => System.Type.EmptyTypes;

    protected object[] Parameters { get; private set; } = Array.Empty<object>();

    public virtual string Group { get; }

    public virtual string[] Mutex { get; } = Array.Empty<string>();

    private static readonly ConcurrentDictionary<string, bool> _mutexes = new();

    private static bool TryGetMutexes(IEnumerable<string> mutexes)
    {
        List<string> captured = new();
        bool result = true;
        foreach (string mutex in mutexes)
        {
            if (_mutexes.TryAdd(mutex, true)) captured.Add(mutex);
            else
            {
                result = false;
                break;
            }
        }
        if (!result) FreeMutexes(captured);
        return result;
    }

    public static void FreeMutexes(IEnumerable<string> mutexes)
    {
        foreach (string mutex in mutexes) _mutexes.TryRemove(mutex, out _);
    }

    public enum EffectType : byte
    {
        Instant = 0,
        Timed = 1,
        [Obsolete("Bid wars are not supported in Crowd Control 2.")]
        BidWar = 2
    }

    public bool Active
    {
        get => _active;
        private set
        {
            if (_active == value) { return; }
            _active = value;
            if (value)
            {
                Elapsed = TimeSpan.Zero;
                Start();
            }
            else { End(); }
        }
    }

    public virtual void Load() => TestMod.LogDebug($"{GetType().Name} was loaded. [{LocalID}]");

    public virtual void Unload() => TestMod.LogDebug($"{GetType().Name} was unloaded. [{LocalID}]");

    public virtual void Start() => TestMod.LogDebug($"{GetType().Name} was started. [{LocalID}]");

    public virtual void End() => TestMod.LogDebug($"{GetType().Name} was stopped. [{LocalID}]");

    public virtual void Update()
    {
        if (Active && IsReady()) Elapsed += Time.fixedDeltaTime;
    }

    public virtual void Draw() { }

    public virtual bool IsReady() => true;

    public bool TryStart() => TryStart(Array.Empty<object>());

    public bool TryStart(object[] parameters)
    {
        lock (_activity_lock)
        {
            if (Active || (!IsReady())) { return false; }
            if (!TryGetMutexes(Mutex)) { return false; }
            Parameters = parameters;
            Elapsed = SITimeSpan.Zero;
            Active = true;
            return true;
        }
    }

    public bool TryStop()
    {
        lock (_activity_lock)
        {
            if (!Active) { return false; }
            FreeMutexes(Mutex);
            Active = false;
            return true;
        }
    }
}