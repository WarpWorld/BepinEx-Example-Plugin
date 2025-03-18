using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using ConnectorLib.JSON;
using CrowdControl.Delegates;
using CrowdControl.Delegates.Effects;
using CrowdControl.Delegates.Metadata;
using UnityEngine;

namespace CrowdControl;

/// <summary>
/// Crowd Control client connection service object.
/// </summary>
public class ControlClient : IDisposable
{
    /// <summary>Crowd Control client IP or hostname.</summary>
    public static readonly string CV_HOST = "127.0.0.1";

    /// <summary>Crowd Control client port.</summary>
    /// <remarks>This needs to be set in the pack CS file.</remarks>
    public static readonly int CV_PORT = 51337;

    private TcpClient? m_client;
    private DelimitedStreamReader? m_streamReader;

    private readonly CCMod m_mod;

    private readonly ConcurrentQueue<EffectRequest> m_requestQueue;

    private readonly CancellationTokenSource m_quitting = new();

    //dispose of the websocket when the client is destroyed
    ~ControlClient() => Dispose(false);

    // ReSharper disable once NotAccessedField.Local
    private readonly Thread m_readLoop;
    private readonly Thread m_maintenanceLoop;

    /// <summary>Disposes of the client connection.</summary>
    public void Dispose() => Dispose(true);

    /// <summary>Disposes of the client connection.</summary>
    /// <param name="disposing">True if this is being called from a disposer, false if the call is from a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        try { m_client?.Dispose(); }
        catch {/**/}
        try { m_quitting.Cancel(); }
        catch {/**/}
        GC.SuppressFinalize(this);
    }

    /// <summary>True if the game is connected to the Crowd Control client, false otherwise.</summary>
    public bool Connected => m_client?.Connected ?? false;

    /// <summary>Creates a new Crowd Control client connection service object.</summary>
    /// <param name="mod"></param>
    public ControlClient(CCMod mod)
    {
        m_mod = mod;
        m_requestQueue = new();

        (m_readLoop = new Thread(NetworkLoop)).Start();
        (m_maintenanceLoop = new Thread(MaintenanceLoop)).Start();
    }

    /// <summary>Maintains a connection to the network stream. Passes control to <see cref="ClientLoop"/> while connected.</summary>
    private void NetworkLoop()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; //do not remove this - kat
        while (!m_quitting.IsCancellationRequested)
        {
            CCMod.Instance.Logger.LogInfo("Attempting to connect to Crowd Control");

            try
            {
                m_client = new();
                m_client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                m_client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                if (m_client.BeginConnect(CV_HOST, CV_PORT, null, null).AsyncWaitHandle.WaitOne(2000, true) &&
                    m_client.Connected)
                    ClientLoop();
                else
                    CCMod.Instance.Logger.LogInfo("Failed to connect to Crowd Control");
            }
            catch (Exception e)
            {
                CCMod.Instance.Logger.LogError(e);
                CCMod.Instance.Logger.LogError("Failed to connect to Crowd Control");
            }
            finally
            {
                try { m_client?.Close(); }
                catch {/**/}
            }
            Thread.Sleep(2000);
        }
    }

    /// <summary>Performs connection maintenance tasks.</summary>
    private void MaintenanceLoop()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; //do not remove this - kat
        while (!m_quitting.IsCancellationRequested)
        {
            try
            {
                if (m_client?.Connected ?? false)
                    KeepAlive();
            }
            catch (Exception e) { CCMod.Instance.Logger.LogError(e); }
            Thread.Sleep(2000);
        }
    }

    /// <summary>Reads from the network stream and processes messages.</summary>
    private void ClientLoop()
    {
        m_streamReader = new(m_client!.GetStream());
        CCMod.Instance.Logger.LogInfo("Connected to Crowd Control");

        try
        {
            while (!m_quitting.IsCancellationRequested)
            {
                string message = m_streamReader.ReadUntilNullTerminator();
                OnMessage(message.Trim());
            }
        }
        catch (EndOfStreamException)
        {
            CCMod.Instance.Logger.LogInfo("Disconnected from Crowd Control");
            m_client?.Close();
        }
        catch (Exception e)
        {
            CCMod.Instance.Logger.LogError(e);
            m_client?.Close();
        }
    }

    /// <summary>Processes a single network message.</summary>
    /// <param name="message">A JSON-formatted message body.</param>
    private void OnMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        try
        {
            if (!SimpleJSONRequest.TryParse(message, out SimpleJSONRequest? req)) return;
            switch (req?.type)
            {
                case RequestType.EffectTest when (req is EffectRequest er):
                    {
                        er.code ??= string.Empty;
                        if (!EffectLoader.Delegates.ContainsKey(er.code))
                        {
                            Send(new EffectResponse(er.id, EffectStatus.Unavailable, StandardErrors.UnknownEffect));
                            CCMod.Instance.Logger.LogError(StandardErrors.UnknownEffect);
                            return;
                        }
                        Send(new EffectResponse(er.id, m_mod.GameStateManager.IsReady(er.code) ? EffectStatus.Success : EffectStatus.Failure));
                    }
                    break;
                case RequestType.EffectStart when (req is EffectRequest er):
                    {
                        er.code ??= string.Empty;
                        if (!EffectLoader.Delegates.ContainsKey(er.code))
                        {
                            Send(new EffectResponse(er.id, EffectStatus.Unavailable, StandardErrors.UnknownEffect));
                            CCMod.Instance.Logger.LogError(StandardErrors.UnknownEffect);
                            return;
                        }
                        m_requestQueue.Enqueue(er);
                    }
                    break;
                case RequestType.EffectStop:
                    break;
                case RequestType.GameUpdate:
                    m_mod.GameStateManager.UpdateGameState(true);
                    break;
            }
        }
        catch (Exception ex)
        {
            CCMod.Instance.Logger.LogError(ex);
        }
    }

    /// <summary>Performs all game state updates including the firing of effects.</summary>
    /// <remarks>This function is called on the main game thread. Blocking here may cause lag or crash the game entirely.</remarks>
    public void FixedUpdate()
    {
        //Log.Message(_game_status_update_timer);
        _game_status_update_timer += Time.fixedDeltaTime;
        if (_game_status_update_timer >= GAME_STATUS_UPDATE_INTERVAL)
        {
            m_mod.GameStateManager.UpdateGameState();
            _game_status_update_timer = 0f;
        }

        while (m_requestQueue.TryDequeue(out var er))
        {
            if (!m_mod.GameStateManager.IsReady(er.code!))
            {
                Send(new EffectResponse(er.id, EffectStatus.Retry));
                return;
            }

            if (!EffectLoader.Delegates.TryGetValue(er.code!, out var eDel)) continue;

            EffectResponse res = eDel.Invoke(this, er);
            res.metadata = new();
            foreach (string key in MetadataLoader.CommonMetadata)
            {
                if (MetadataLoader.Metadata.TryGetValue(key, out MetadataDelegate? del))
                    res.metadata.Add(key, del.Invoke(this));
                else
                    CCMod.Instance.Logger.LogError($"Metadata delegate \"{key}\" could not be found. Available delegates: {string.Join(", ", MetadataLoader.Metadata.Keys)}");
            }
            Send(res);
        }
    }

    /// <summary>
    /// Sends a response message to the Crowd Control client.
    /// </summary>
    /// <param name="response">The response object to send.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool Send(SimpleJSONResponse response)
    {
        try
        {
            if (!Connected) return false;
            byte[] bytes = [.. Encoding.UTF8.GetBytes(response.Serialize()), 0];
            m_client!.GetStream().Write(bytes, 0, bytes.Length);
            return true;
        }
        catch (Exception e)
        {
            CCMod.Instance.Logger.LogError($"Error sending a message to the Crowd Control client: {e}");
            return false;
        }
    }

    /// <summary>
    /// Hides the specified effects on the menu.
    /// </summary>
    /// <param name="codes">The effect IDs to hide.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool HideEffects(params string[] codes)
    {
        EffectUpdate res = new(codes, EffectStatus.NotVisible);
        return Send(res);
    }

    /// <summary>
    /// Shows the specified effects on the menu.
    /// </summary>
    /// <param name="codes">The effect IDs to show.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool ShowEffects(params string[] codes)
    {
        EffectUpdate res = new(codes, EffectStatus.Visible);
        return Send(res);
    }

    /// <summary>
    /// Makes the specified effects unselectable on the menu.
    /// </summary>
    /// <param name="codes">The effect IDs to make unselectable.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool DisableEffects(params IEnumerable<string> codes)
    {
        EffectUpdate res = new(codes, EffectStatus.NotSelectable);
        return Send(res);
    }

    /// <summary>
    /// Makes the specified effects selectable on the menu.
    /// </summary>
    /// <param name="codes">The effect IDs to make selectable.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool EnableEffects(params IEnumerable<string> codes)
    {
        EffectUpdate res = new(codes, EffectStatus.Selectable);
        return Send(res);
    }

    /// <summary>
    /// Closes the connection to the Crowd Control client.
    /// </summary>
    public void Stop() => m_client?.Close();

    /// <summary>
    /// Closes the connection to the Crowd Control client.
    /// </summary>
    /// <param name="message">The reason message to send to the client prior to disconnection.</param>
    public void Stop(string message)
    {
        Send(new MessageResponse()
        {
            type = ResponseType.Disconnect,
            message = message
        });
        m_client?.Close();
    }

    private static readonly EmptyResponse KEEPALIVE = new() { type = ResponseType.KeepAlive };

    /// <summary>
    /// Sends a keepalive message to the Crowd Control client.
    /// </summary>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool KeepAlive() => Send(KEEPALIVE);

    private const float GAME_STATUS_UPDATE_INTERVAL = 1f;
    private float _game_status_update_timer;
}