using System.Collections;
using System.Collections.Concurrent;
using ConnectorLib.JSON;
using CrowdControl.Delegates.Effects;

namespace CrowdControl;

public class Scheduler(CrowdControlMod mod, NetworkClient networkClient)
{
    private CrowdControlMod m_mod = mod;
    private NetworkClient m_networkClient = networkClient;
    
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
                    break;
                case TimedEffectState.EffectState.Running:
                    m_enumerator ??= TimedEffectState.Tick();
                    break;
                case TimedEffectState.EffectState.Finished:
                    return false;
            }

            return m_enumerator?.MoveNext() ?? false;
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

    private readonly ConcurrentQueue<RequestState> m_requestQueue = new();
    private readonly ConcurrentDictionary<uint, RequestState> m_runningEffects = new();

    /// <summary>Checks if a timed effect of the specified type is currently running.</summary>
    /// <param name="id">The ID of the timed effect.</param>
    /// <returns>True if a timed effect of the specified type is running, false otherwise.</returns>
    public bool IsRunning(string id)
    {
        foreach (TimedEffectState thread in m_requestQueue.Select(p => p.TimedEffectState).OfType<TimedEffectState>())
            if (thread.Effect.EffectAttribute.IDs.Contains(id))
                return true;
        return false;
    }
    
    /// <summary>Processes and starts an effect request.</summary>
    /// <param name="request">The effect request object.</param>
    public void ProcessRequest(SimpleJSONRequest? request)
    {
        switch (request?.type)
            {
                case RequestType.EffectTest when (request is EffectRequest er):
                    {
                        er.code ??= string.Empty;
                        if (!m_mod.EffectLoader.Effects.ContainsKey(er.code))
                        {
                            m_networkClient.Send(new EffectResponse(er.id, EffectStatus.Unavailable, StandardErrors.UnknownEffect));
                            CrowdControlMod.Instance.Logger.LogError(StandardErrors.UnknownEffect);
                            return;
                        }
                        m_networkClient.Send(new EffectResponse(er.id, m_mod.GameStateManager.IsReady(er.code) ? EffectStatus.Success : EffectStatus.Failure));
                    }
                    break;
                case RequestType.EffectStart when (request is EffectRequest er):
                    {
                        er.code ??= string.Empty;
                        if (!m_mod.EffectLoader.Effects.TryGetValue(er.code, out Effect effect))
                        {
                            m_networkClient.Send(new EffectResponse(er.id, EffectStatus.Unavailable, StandardErrors.UnknownEffect));
                            CrowdControlMod.Instance.Logger.LogError(StandardErrors.UnknownEffect);
                            return;
                        }
                        m_requestQueue.Enqueue(new(er, effect));
                    }
                    break;
                case RequestType.EffectStop when (request is EffectRequest er):
                    {
                        if (!m_runningEffects.TryGetValue(er.id, out RequestState state))
                        {
                            m_networkClient.Send(new EffectResponse(er.id, EffectStatus.Failure, StandardErrors.AlreadyFinished));
                            CrowdControlMod.Instance.Logger.LogError(StandardErrors.AlreadyFinished);
                            return;
                        }
                        state.Stop();
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

    public void Tick()
    {
        while (m_requestQueue.TryDequeue(out RequestState pReq))
        {
            if (!m_mod.GameStateManager.IsReady(pReq.Request.code!))
            {
                m_networkClient.SendAsync(new EffectResponse(pReq.Request.id, EffectStatus.Retry)).Forget();
                return;
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
                    CrowdControlMod.Instance.Logger.LogError(e.Message);
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