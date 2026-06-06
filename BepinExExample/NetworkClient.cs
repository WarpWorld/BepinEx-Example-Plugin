using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using ConnectorLib.JSON;

namespace CrowdControl;

/// <summary>
/// Crowd Control client connection service object.
/// </summary>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public partial class NetworkClient : IDisposable
{
    private const bool PROCESS_LOOKUP_FALLBACK = true;
    
    private static readonly SITimeSpan TIMEOUT_NO_PROCESS = 5;
    private static readonly SITimeSpan TIMEOUT_NO_CONNECTION = 2;
    
    /// <summary>Crowd Control client IP or hostname.</summary>
    public static readonly string CV_HOST = "127.0.0.1";

    /// <summary>Crowd Control client port.</summary>
    /// <remarks>This needs to be set in the pack CS file.</remarks>
    public static readonly int CV_PORT = 51337;

    private TcpClient? m_client;
    private DelimitedStreamReader? m_streamReader;

    private readonly CrowdControlMod m_mod;

    private readonly CancellationTokenSource m_quitting = new();
    private readonly object m_shutdownLock = new();
    private readonly object m_sendLock = new();
    private volatile bool m_disposed;

    ~NetworkClient() => Dispose(false);

    // ReSharper disable NotAccessedField.Local
    private readonly Thread m_readLoop;
    private readonly Thread m_maintenanceLoop;
    // ReSharper restore NotAccessedField.Local

    /// <summary>Disposes of the client connection.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Closes the Crowd Control socket and stops background threads (call from <c>OnApplicationQuit</c>).</summary>
    /// <param name="disposing">True when releasing managed resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        lock (m_shutdownLock)
        {
            if (m_disposed)
                return;
            m_disposed = true;
        }

        try { m_quitting.Cancel(); }
        catch {/**/}

        CloseConnection();
    }

    private void CloseConnection()
    {
        lock (m_sendLock)
        {
            try
            {
                m_streamReader?.Dispose();
            }
            catch {/**/}
            finally
            {
                m_streamReader = null;
            }

            try
            {
                if (m_client?.Connected == true)
                    m_client.Client.Shutdown(SocketShutdown.Both);

                m_client?.Close();
                m_client?.Dispose();
            }
            catch {/**/}
            finally
            {
                m_client = null;
            }
        }
    }

    private static bool IsBenignShutdownException(Exception e, bool quitting) =>
        quitting
        || e is ThreadAbortException
        || e is ObjectDisposedException
        || e is OperationCanceledException
        || (e is IOException io && io.InnerException is SocketException se && IsBenignSocketError(se.SocketErrorCode))
        || (e is SocketException se2 && IsBenignSocketError(se2.SocketErrorCode));

    private static bool IsBenignSocketError(SocketError code) =>
        code is SocketError.ConnectionAborted
            or SocketError.ConnectionReset
            or SocketError.Interrupted
            or SocketError.OperationAborted
            or SocketError.Shutdown;

    /// <summary>True if the game is connected to the Crowd Control client, false otherwise.</summary>
    public bool Connected => m_client?.Connected ?? false;

    /// <summary>Creates a new Crowd Control client connection service object.</summary>
    /// <param name="mod">The Crowd Control game mod object.</param>
    public NetworkClient(CrowdControlMod mod)
    {
        m_mod = mod;

        m_readLoop = new Thread(NetworkLoop) { IsBackground = true, Name = "CrowdControl.NetworkRead" };
        m_maintenanceLoop = new Thread(MaintenanceLoop) { IsBackground = true, Name = "CrowdControl.NetworkMaintenance" };
        m_readLoop.Start();
        m_maintenanceLoop.Start();
    }

    /// <summary>Maintains a connection to the network stream. Passes control to <see cref="ClientLoop"/> while connected.</summary>
    private void NetworkLoop()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; //do not remove this - kat
        while (!m_quitting.IsCancellationRequested)
        {
#pragma warning disable CS0162 // Unreachable code detected
            // Check if CrowdControl process is running before attempting to connect
            if ((!IsCrowdControlSemaphorePresent()) &&
                (!(PROCESS_LOOKUP_FALLBACK && IsCrowdControlProcessRunning())))
            {
                CrowdControlMod.Instance.Logger.LogMessage("No CrowdControl process found, skipping connection attempt...");
                m_quitting.Token.WaitHandle.WaitOne((TimeSpan)TIMEOUT_NO_PROCESS);
                continue;
            }
#pragma warning restore CS0162 // Unreachable code detected
            
            CrowdControlMod.Instance.Logger.LogInfo("Attempting to connect to Crowd Control");

            try
            {
                m_client = new();
                m_client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                m_client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                if (m_client.BeginConnect(CV_HOST, CV_PORT, null, null).AsyncWaitHandle.WaitOne(2000, true) &&
                    m_client.Connected)
                    ClientLoop();
                else
                    CrowdControlMod.Instance.Logger.LogInfo("Failed to connect to Crowd Control");
            }
            catch (Exception e)
            {
                if (!IsBenignShutdownException(e, m_quitting.IsCancellationRequested))
                {
                    CrowdControlMod.Instance.Logger.LogError(e);
                    CrowdControlMod.Instance.Logger.LogError("Failed to connect to Crowd Control");
                }
            }
            finally
            {
                CloseConnection();
            }

            if (m_quitting.IsCancellationRequested)
                break;

            m_quitting.Token.WaitHandle.WaitOne((TimeSpan)TIMEOUT_NO_CONNECTION);
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
                if (!m_disposed && (m_client?.Connected ?? false))
                    KeepAlive();
            }
            catch { /**/ }

            m_quitting.Token.WaitHandle.WaitOne(1000);
        }
    }

    /// <summary>Reads from the network stream and processes messages.</summary>
    private void ClientLoop()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; //do not remove this - kat

        try
        {
            m_streamReader = new(m_client!.GetStream());
            CrowdControlMod.Instance.Logger.LogInfo("Connected to Crowd Control");

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
                if (!m_quitting.IsCancellationRequested)
                    CrowdControlMod.Instance.Logger.LogInfo("Disconnected from Crowd Control");
            }
            catch (Exception e)
            {
                if (!IsBenignShutdownException(e, m_quitting.IsCancellationRequested))
                {
                    CrowdControlMod.Instance.Logger.LogError(e);
                    CrowdControlMod.Instance.Logger.LogError("Disconnected from Crowd Control");
                }
            }
        }
        finally
        {
            CloseConnection();
        }
    }

    /// <summary>Processes a single network message.</summary>
    /// <param name="message">A JSON-formatted message body.</param>
    private void OnMessage(string message)
    {
        if (m_disposed || m_quitting.IsCancellationRequested)
            return;
        if (string.IsNullOrWhiteSpace(message)) return;
        try
        {
            if (!SimpleJSONRequest.TryParse(message, out SimpleJSONRequest? req)) return;
            m_mod.Scheduler.ProcessRequest(req!);
        }
        catch (Exception ex)
        {
            CrowdControlMod.Instance.Logger.LogError(ex);
        }
    }
    
    /// <summary>Checks if the CrowdControl semaphore is present, indicating that the CrowdControl client is running.</summary>
    /// <returns>True if the semaphore is present, false otherwise.</returns>
    private static bool IsCrowdControlSemaphorePresent() => Semaphore.TryOpenExisting("CrowdControl", out _);

    /// <summary>
    /// Checks if any CrowdControl process is running.
    /// Looks for processes with names containing "crowdcontrol" (case-insensitive).
    /// Handles cases where processes might be running with different privilege levels.
    /// </summary>
    /// <returns>True if a CrowdControl process is found, false otherwise.</returns>
    private static bool IsCrowdControlProcessRunning()
    {
        try
        {
            Process[] processes = Process.GetProcesses();
            int inaccessibleProcesses = 0;
            
            foreach (Process process in processes)
            {
                try
                {
                    if (process.ProcessName.IndexOf("crowdcontrol", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        CrowdControlMod.Instance.Logger.LogMessage($"Found CrowdControl process: {process.ProcessName} (PID: {process.Id})");
                        return true;
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Process is running with different privileges (e.g., admin vs regular user)
                    inaccessibleProcesses++;
                }
                catch (Exception ex)
                {
                    // Other access issues
                    CrowdControlMod.Instance.Logger.LogMessage($"Could not access process: {ex.Message}");
                    inaccessibleProcesses++;
                }
            }
            
            // If we have inaccessible processes, it's possible CrowdControl is running with different privileges
            if (inaccessibleProcesses > 0)
            {
                CrowdControlMod.Instance.Logger.LogMessage($"Found {inaccessibleProcesses} inaccessible processes (possibly running with different privileges). Attempting connection anyway.");
                // This handles the case where CrowdControl is running as admin but game is not
                return true;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            CrowdControlMod.Instance.Logger.LogMessage($"Error checking for CrowdControl processes: {ex.Message}");
            // If we can't check processes at all, assume CrowdControl might be running and attempt connection
            return true;
        }
    }

    /// <summary>Sends a response message to the Crowd Control client.</summary>
    /// <param name="response">The response object to send.</param>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool Send(SimpleJSONResponse? response)
    {
        try
        {
            if (response == null || m_disposed || m_quitting.IsCancellationRequested)
                return false;

            byte[] bytes = [.. Encoding.UTF8.GetBytes(response.Serialize()), 0];

            lock (m_sendLock)
            {
                if (m_client?.Connected != true)
                    return false;

                m_client.GetStream().Write(bytes, 0, bytes.Length);
                return true;
            }
        }
        catch (Exception e)
        {
            if (!IsBenignShutdownException(e, m_quitting.IsCancellationRequested))
                CrowdControlMod.Instance.Logger.LogError($"Error sending a message to the Crowd Control client: {e}");
            return false;
        }
    }

    /// <inheritdoc cref="Send"/>
    /// <summary>Asynchronously sends a response message to the Crowd Control client.</summary>
    public Task<bool> SendAsync(SimpleJSONResponse? response) => Task.Run(() => Send(response));
    
    /// <summary>Closes the connection to the Crowd Control client.</summary>
    /// <param name="message">An optional reason message to send to the client prior to disconnection.</param>
    public void Stop(string? message = null)
    {
        if (m_disposed)
            return;

        if (message != null)
        {
            Send(new MessageResponse()
            {
                type = ResponseType.Disconnect,
                message = message
            });
        }

        try { m_quitting.Cancel(); }
        catch {/**/}

        CloseConnection();
    }

    /// <inheritdoc cref="Stop"/>
    /// <summary>Asynchronously closes the connection to the Crowd Control client.</summary>
    public Task StopAsync(string? message = null) => Task.Run(() => Stop(message));

    private static readonly EmptyResponse KEEPALIVE = new() { type = ResponseType.KeepAlive };

    /// <summary>Sends a keepalive message to the Crowd Control client.</summary>
    /// <returns>True if the message was sent successfully, false otherwise.</returns>
    public bool KeepAlive() => Send(KEEPALIVE);

    /// <inheritdoc cref="KeepAlive"/>
    /// <summary>Asynchronously sends a keepalive message to the Crowd Control client.</summary>
    public Task<bool> KeepAliveAsync() => Task.Run(KeepAlive);
}
