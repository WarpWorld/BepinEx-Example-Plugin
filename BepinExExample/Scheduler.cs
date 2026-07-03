using System.Collections;
using System.Collections.Concurrent;
using ConnectorLib.JSON;
using CrowdControl.Delegates.Effects;
using CrowdControl.Delegates.Metadata;

namespace CrowdControl;

public class Scheduler(CrowdControlMod mod, NetworkClient networkClient)
{
    private readonly CrowdControlMod m_mod = mod;
    private readonly NetworkClient m_networkClient = networkClient;

    private class RequestState
    {
        public EffectRequest Request { get; }
        public Effect Effect { get; }
        public TimedEffectState? TimedEffectState { get; }
        private IEnumerator? m_enumerator;

        public bool MoveNext()
        {
            if (m_enumerator != null)
            {
                if (m_enumerator.MoveNext()) return true;
                (m_enumerator as IDisposable)?.Dispose();
                m_enumerator = null;
            }

            switch (TimedEffectState?.State)
            {
                case TimedEffectState.EffectState.NotStarted:
                    m_enumerator ??= TimedEffectState.Start();
                    m_enumerator.MoveNext();
                    return true;
                case TimedEffectState.EffectState.Running:
                case TimedEffectState.EffectState.Paused:
                    m_enumerator ??= TimedEffectState.Tick();
                    m_enumerator.MoveNext();
                    return true;
                //case TimedEffectState.EffectState.Errored:
                //case TimedEffectState.EffectState.Finished:
                default:
                    return false;
            }
        }

        public void Pause()
        {
            (m_enumerator as IDisposable)?.Dispose();
            m_enumerator = null;

            switch (TimedEffectState?.State)
            {
                case TimedEffectState.EffectState.Running:
                    m_enumerator = TimedEffectState.Pause();
                    break;
            }
        }

        public void Resume()
        {
            (m_enumerator as IDisposable)?.Dispose();
            m_enumerator = null;

            switch (TimedEffectState?.State)
            {
                case TimedEffectState.EffectState.Paused:
                    m_enumerator = TimedEffectState.Resume();
                    break;
            }
        }

        public void Stop()
        {
            (m_enumerator as IDisposable)?.Dispose();
            m_enumerator = null;

            switch (TimedEffectState?.State)
            {
                case TimedEffectState.EffectState.NotStarted:
                case TimedEffectState.EffectState.Running:
                case TimedEffectState.EffectState.Paused:
                    m_enumerator = TimedEffectState.Stop();
                    break;
            }
        }

        public RequestState(EffectRequest request, Effect effect)
        {
            Request = request;
            Effect = effect;
            if (Effect.IsTimed) TimedEffectState = new(effect, request, SITimeSpan.FromMilliseconds(request.duration ?? 0));
        }
    }

    //messages arrive on the network read thread and are drained on the game thread in Tick()
    //this keeps all game (Unity) API access on the main thread, which is required for stability
    private readonly ConcurrentQueue<SimpleJSONRequest> m_messageQueue = new();
    private readonly ConcurrentQueue<RequestState> m_requestQueue = new();
    private readonly ConcurrentDictionary<uint, RequestState> m_runningEffects = new();

    /// <summary>Checks if an effect with the specified ID is currently running or queued to run.</summary>
    /// <param name="id">The ID of the effect.</param>
    /// <returns>True if an effect with the specified ID is running or queued, false otherwise.</returns>
    public bool IsRunning(string id) =>
        m_runningEffects.Values.Any(r => r.Effect.EffectAttribute.IDs.Contains(id)) ||
        m_requestQueue.Any(r => r.Effect.EffectAttribute.IDs.Contains(id));

    /// <summary>Checks if the supplied effect conflicts with any currently-running effect.</summary>
    /// <param name="effect">The effect that is about to be started.</param>
    /// <returns>True if a running effect conflicts with the supplied effect, false otherwise.</returns>
    private bool HasConflict(Effect effect)
    {
        EffectAttribute pending = effect.EffectAttribute;
        foreach (RequestState running in m_runningEffects.Values)
        {
            EffectAttribute active = running.Effect.EffectAttribute;
            if (pending.Conflicts.Intersect(active.IDs).Any()) return true;
            if (active.Conflicts.Intersect(pending.IDs).Any()) return true;
        }
        return false;
    }

    /// <summary>Queues an incoming client message for processing on the game thread.</summary>
    /// <param name="request">The request object.</param>
    /// <remarks>This is safe to call from any thread. The actual processing happens in <see cref="Tick"/>.</remarks>
    public void ProcessRequest(SimpleJSONRequest? request)
    {
        if (request == null) return;
        m_messageQueue.Enqueue(request);
    }

    /// <summary>Processes a single client message. Must be called on the game thread.</summary>
    /// <param name="request">The request object.</param>
    private void HandleMessage(SimpleJSONRequest request)
    {
        switch (request.type)
        {
            case RequestType.EffectTest when (request is EffectRequest er):
                {
                    er.code ??= string.Empty;
                    if (!m_mod.EffectLoader.Effects.ContainsKey(er.code))
                    {
                        m_networkClient.Send(new EffectResponse(er.id, EffectStatus.Unavailable, StandardErrors.EffectUnknown));
                        m_mod.Logger.LogError($"Effect test requested for unknown effect \"{er.code}\".");
                        return;
                    }
                    //respond as if the effect would start, but don't actually start it
                    m_networkClient.Send(new EffectResponse(er.id, m_mod.GameStateManager.IsReady(er.code) ? EffectStatus.Success : EffectStatus.Retry));
                }
                break;
            case RequestType.EffectStart when (request is EffectRequest er):
                {
                    er.code ??= string.Empty;
                    if (!m_mod.EffectLoader.Effects.TryGetValue(er.code, out Effect effect))
                    {
                        m_networkClient.Send(new EffectResponse(er.id, EffectStatus.Unavailable, StandardErrors.EffectUnknown));
                        m_mod.Logger.LogError($"Effect start requested for unknown effect \"{er.code}\".");
                        return;
                    }
                    m_requestQueue.Enqueue(new(er, effect));
                }
                break;
            case RequestType.EffectStop when (request is EffectRequest er):
                {
                    //stop requests may reference the original request ID or an effect code (stops all instances)
                    bool found = false;
                    foreach (RequestState state in m_runningEffects.Values)
                    {
                        if ((state.Request.id == er.id) ||
                            ((er.code != null) && state.Effect.EffectAttribute.IDs.Contains(er.code)))
                        {
                            state.Stop();
                            found = true;
                        }
                    }
                    if (!found)
                        m_networkClient.Send(new EffectResponse(er.id, EffectStatus.Failure, StandardErrors.AlreadyFinished));
                }
                break;
            case RequestType.DataRequest when (request is DataRequest dr):
                {
                    DataResponse response;
                    if ((dr.key != null) && MetadataLoader.Metadata.TryGetValue(dr.key, out MetadataDelegate? del))
                    {
                        try { response = del.Invoke(m_mod); }
                        catch (Exception e)
                        {
                            m_mod.Logger.LogError(e);
                            response = DataResponse.Failure(dr.key, StandardErrors.ExceptionThrown.ToString());
                        }
                    }
                    else
                        response = DataResponse.Failure(dr.key ?? string.Empty, "Unknown metadata key.");
                    response.id = dr.id;
                    m_networkClient.Send(response);
                }
                break;
            case RequestType.GameUpdate:
                m_mod.GameStateManager.UpdateGameState(true);
                break;
        }
    }

    public void Enqueue(EffectRequest request, Effect effect)
    {
        m_requestQueue.Enqueue(new RequestState(request, effect));
    }

    /// <summary>Pauses all running timed effects.</summary>
    public void PauseAll()
    {
        foreach (KeyValuePair<uint, RequestState> kvp in m_runningEffects) kvp.Value.Pause();
    }

    /// <summary>Resumes all paused timed effects.</summary>
    public void ResumeAll()
    {
        foreach (KeyValuePair<uint, RequestState> kvp in m_runningEffects) kvp.Value.Resume();
    }

    /// <summary>Processes queued messages and advances all running effects. Must be called on the game thread.</summary>
    public void Tick()
    {
        while (m_messageQueue.TryDequeue(out SimpleJSONRequest message))
        {
            try { HandleMessage(message); }
            catch (Exception e) { m_mod.Logger.LogError(e); }
        }

        while (m_requestQueue.TryDequeue(out RequestState pReq))
        {
            if (!m_mod.GameStateManager.IsReady(pReq.Request.code!))
            {
                m_networkClient.SendAsync(new EffectResponse(pReq.Request.id, EffectStatus.Retry)).Forget();
                continue;
            }

            //locally enforce declared effect conflicts (including self-conflicts)
            if (HasConflict(pReq.Effect))
            {
                m_networkClient.SendAsync(new EffectResponse(pReq.Request.id, EffectStatus.Retry, StandardErrors.ConflictingEffectRunning)).Forget();
                continue;
            }

            if (pReq.TimedEffectState != null)
            {
                m_runningEffects.TryAdd(pReq.Request.id, pReq);
            }
            else
            {
                EffectResponse response;
                try { response = pReq.Effect.Start(pReq.Request); }
                catch (Exception e)
                {
                    response = EffectResponse.Failure(pReq.Request.id, StandardErrors.ExceptionThrown);
                    m_mod.Logger.LogError(e);
                }
                m_networkClient.AttachMetadata(response);
                m_networkClient.SendAsync(response).Forget();
            }
        }

        ConsumeEnumerators();
    }

    private void ConsumeEnumerators()
    {
        foreach (KeyValuePair<uint, RequestState> kvp in m_runningEffects)
        {
            if (kvp.Value.MoveNext())
                continue;
            m_runningEffects.TryRemove(kvp.Key, out _);
        }
    }
}
