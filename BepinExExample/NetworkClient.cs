using System.Diagnostics;
using System.Globalization;
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

    //dispose of the websocket when the client is destroyed
    ~NetworkClient() => Dispose(false);

    // ReSharper disable NotAccessedField.Local
    private readonly Thread m_readLoop;
    private readonly Thread m_maintenanceLoop;
    // ReSharper restore NotAccessedField.Local

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
    /// <param name="mod">The Crowd Control game mod object.</param>
    public NetworkClient(CrowdControlMod mod)
    {
        m_mod = mod;

        (m_readLoop = new Thread(NetworkLoop)).Start();
        (m_maintenanceLoop = new Thread(MaintenanceLoop)).Start();
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
                Thread.Sleep((TimeSpan)TIMEOUT_NO_PROCESS);
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
                CrowdControlMod.Instance.Logger.LogError(e);
                CrowdControlMod.Instance.Logger.LogError("Failed to connect to Crowd Control");
            }
            finally
            {
                try { m_client?.Close(); }
                catch {/**/}
            }
            Thread.Sleep((TimeSpan)TIMEOUT_NO_CONNECTION);
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
            catch { /**/ }
            Thread.Sleep(2000);
        }
    }

    /// <summary>Reads from the network stream and processes messages.</summary>
    private void ClientLoop()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture; //do not remove this - kat
        
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
            CrowdControlMod.Instance.Logger.LogInfo("Disconnected from Crowd Control");
            m_client?.Close();
        }
        catch (Exception e)
        {
            CrowdControlMod.Instance.Logger.LogError(e);
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
            int accessibleProcesses = 0;
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
                    accessibleProcesses++;
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
            if (response == null) return false;
            if (!Connected) return false;
            byte[] bytes = [.. Encoding.UTF8.GetBytes(response.Serialize()), 0];
            m_client!.GetStream().Write(bytes, 0, bytes.Length);
            return true;
        }
        catch (Exception e)
        {
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
        if (message != null)
        {
            Send(new MessageResponse()
            {
                type = ResponseType.Disconnect,
                message = message
            });
        }
        m_client?.Close();
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