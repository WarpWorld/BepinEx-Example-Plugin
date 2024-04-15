/*
 * Nane: Crowd Control BepInEx Plugin
 * Copyright: © 2021 TerribleTable, © 2024 Warp World
 * License: GNU Lesser General Public License (LGPL) v3.0
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3.0 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301
 * USA
 */

using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ConnectorLib.JSON;
using CrowdControl.Common;
using UnityEngine;
using Effect = CrowdControl.Effects.Effect;
using EffectRequest = ConnectorLib.JSON.EffectRequest;
using EffectResponse = ConnectorLib.JSON.EffectResponse;
using EffectStatus = ConnectorLib.JSON.EffectStatus;
using Socket = System.Net.Sockets.Socket;

namespace CrowdControl;

public class ControlClient : IDisposable
{
    public static readonly string CV_HOST = "127.0.0.1";
    public static readonly int CV_PORT = 51337;

    public readonly ConcurrentDictionary<string, Effect> Effects = new();

    private IPEndPoint Endpoint { get; } = new(IPAddress.Parse(CV_HOST), CV_PORT);
    private Socket? _socket;

    private readonly TestMod _mod;

    private readonly ConcurrentQueue<Action> _update_queue = new();
    private readonly ConcurrentQueue<Action> _draw_queue = new();

    private volatile bool _running;
    public bool IsRunning() => _running;

    public bool Paused { get; private set; }

    public PlayerInfo? PlayerInfo { get; private set; }

    ~ControlClient() => Dispose(false);

    public ControlClient(TestMod mod) => _mod = mod;

    public void Dispose() => Dispose(true);
    protected void Dispose(bool disposing)
    {
        _running = false;
        try { _socket?.Dispose(); }
        catch {/**/}
    }

    public void UpdateTick()
    {
        try { while (_update_queue.TryDequeue(out Action action)) action(); }
        catch (Exception e) { TestMod.LogError(e); }

        foreach (Effect effect in Effects.Values)
            try { if (effect.Active) effect.Update(); }
            catch (Exception e) { TestMod.LogError(e); }
    }

    public void DrawTick()
    {
        try { while (_draw_queue.TryDequeue(out Action action)) action(); }
        catch (Exception e) { TestMod.LogError(e); }

        foreach (Effect effect in Effects.Values)
            try { if (effect.Active) effect.Draw(); }
            catch (Exception e) { TestMod.LogError(e); }
    }

    public void ShowEffect(params string[] ids) => EffectUpdate(EffectStatus.Visible, ConnectorLib.JSON.EffectUpdate.IdentifierType.Effect, ids);
    public void ShowEffect(EffectUpdate.IdentifierType idType, params string[] ids) => EffectUpdate(EffectStatus.Visible, idType, ids);
        
    public void HideEffect(params string[] ids) => EffectUpdate(EffectStatus.NotVisible, ConnectorLib.JSON.EffectUpdate.IdentifierType.Effect, ids);
    public void HideEffect(EffectUpdate.IdentifierType idType, params string[] ids) => EffectUpdate(EffectStatus.NotVisible, idType, ids);
        
    public void EnableEffect(params string[] ids) => EffectUpdate(EffectStatus.Selectable, ConnectorLib.JSON.EffectUpdate.IdentifierType.Effect, ids);
    public void EnableEffect(EffectUpdate.IdentifierType idType, params string[] ids) => EffectUpdate(EffectStatus.Selectable, idType, ids);

    public void DisableEffect(params string[] ids) => EffectUpdate(EffectStatus.NotSelectable, ConnectorLib.JSON.EffectUpdate.IdentifierType.Effect, ids);
    public void DisableEffect(EffectUpdate.IdentifierType idType, params string[] ids) => EffectUpdate(EffectStatus.NotSelectable, idType, ids);
        
    public void EffectUpdate(EffectStatus effectStatus, params string[] ids) => EffectUpdate(effectStatus, ConnectorLib.JSON.EffectUpdate.IdentifierType.Effect, ids);
    public void EffectUpdate(EffectStatus effectStatus, EffectUpdate.IdentifierType idType, params string[] ids)
        => TrySend(new EffectUpdate
        {
            ids = ids,
            idType = idType,
            status = effectStatus
        });

    public async Task DoEvents()
    {
        try
        {
            _running = true;
            await Task.WhenAll(
                Task.Factory.StartNew(NetworkLoop, TaskCreationOptions.LongRunning),
                Task.Factory.StartNew(KeepAlive, TaskCreationOptions.LongRunning)
            );
        }
        catch (Exception e) { TestMod.LogError(e); }
        finally { _running = false; }
    }

    public void Stop() => _running = false;

    #region Networking

    public async Task KeepAlive()
    {
        const int KEEPALIVE_INTERVAL = 2500; //2.5s
        while (_running)
        {
            TrySend(SimpleJSONResponse.KeepAlive);
            await Task.Delay(KEEPALIVE_INTERVAL);
        }
    }

    /// <remarks>
    /// This function will never throw any exceptions.
    /// </remarks>
    private async Task<bool> TrySend(SimpleJSONResponse message)
    {
        try
        {
            if (_socket == null) return false;

            string json = JsonConvert.SerializeObject(message); //unavoidable intermediary string alloc here
            int bufSize = Encoding.UTF8.GetByteCount(json) + 1;

            byte[] outData = new byte[bufSize];
            Encoding.UTF8.GetBytes(json, outData);
            await _socket.SendAsync(outData, SocketFlags.None);

            return true;
        }
        catch (Exception e)
        {
            TestMod.LogError(e);
            return false;
        }
    }

    public async Task Recieve()
    {
        //this could potentially get called a lot, so we try to keep down on allocations here
        const int TCP_MAX_PACKET = 65536; //64kb - do not change this value

        byte[] tcpBuf = new byte[TCP_MAX_PACKET];
        List<byte> messageBuf = [];
        int lastIndex = 0;

        try
        {
            while (_running)
            {
                int read = await _socket.ReceiveAsync(tcpBuf, SocketFlags.None);
                if (read <= 0) return;
                messageBuf.AddRange(new ArraySegment<byte>(tcpBuf, 0, read));
                // looping on this is a lot faster than looping on the socket
                int i = lastIndex;
                for (; i < messageBuf.Count; i++)
                {
                    if (messageBuf[i] != 0) continue;
                    //at this point we have to alloc because it's time to build the message
                    try
                    {
                        byte[] jBuf = new byte[i];
                        messageBuf.CopyTo(0, jBuf, 0, i);
                        SimpleJSONRequest? request = SimpleJSONRequest.Parse(Encoding.UTF8.GetString(jBuf));
                        HandleRequest(request).Forget();
                    }
                    catch (Exception e) { TestMod.LogError(e); }
                    finally
                    {
                        messageBuf.RemoveRange(0, i + 1);
                        i = 0;
                    }
                }
                lastIndex = i;
            }
        }
        catch (Exception e) { TestMod.LogError(e); }
    }

    public async Task NetworkLoop()
    {
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        while (_running)
        {

            TestMod.LogInfo("Attempting to connect to Crowd Control");
            try
            {
                _socket = new Socket(Endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                await _socket.ConnectAsync(Endpoint);
                await Recieve();
                _socket.Close();
            }
            catch (Exception e)
            {
                _socket = null;
                TestMod.LogInfo(e.GetType().Name);
                TestMod.LogInfo("Failed to connect to Crowd Control");
            }

            Thread.Sleep(10000);
        }
    }

    private async Task<bool> Respond(EffectRequest request, EffectStatus status, SITimeSpan? timeRemaining = null, string message = "")
    {
        try
        {
            return await TrySend(new EffectResponse
            {
                id = request.id,
                status = status,
                timeRemaining = ((long?)timeRemaining?.TotalMilliseconds) ?? 0L,
                message = message,
                type = ResponseType.EffectRequest
            });
        }
        catch (Exception e)
        {
            TestMod.LogError(e);
            return false;
        }
    }

    #endregion

    #region Handlers

    private async Task<bool> HandleRequest(SimpleJSONRequest request)
    {
        if (request.IsKeepAlive) return true;
        switch (request.type) 
        {
            case RequestType.Test:
                return await OnTest((EffectRequest)request);
            case RequestType.Start:
                return await OnRequest((EffectRequest)request);
            case RequestType.Stop:
                return await OnStop((EffectRequest)request);
            case RequestType.PlayerInfo:
                PlayerInfo = (PlayerInfo)request;
                return true;
        }
        return false;
    }

    private async Task<bool> OnTest(EffectRequest request)
    {
        TestMod.LogDebug($"Got an effect request [{request.id}:{request.code}].");
        if (string.IsNullOrWhiteSpace(request.code)) return false;

        if (!Effects.TryGetValue(request.code, out Effect effect))
        {
            TestMod.LogError($"Effect {request.code} not found.");
            //could not find the effect
            return await Respond(request, EffectStatus.Unavailable);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (effect.Type == Effect.EffectType.BidWar)
            return await Respond(request, EffectStatus.Unavailable, null, "Bid wars are not supported in Crowd Control 2.");
#pragma warning restore CS0618 // Type or member is obsolete

        if (!effect.IsReady())
            return await Respond(request, EffectStatus.Retry);

        TestMod.LogDebug($"Effect {request.code} started.");
        return await Respond(request, EffectStatus.Success, ((effect.Type == Effect.EffectType.Timed) ? effect.Duration : (SITimeSpan?)null));
    }

    private async Task<bool> OnStop(EffectRequest request)
    {
        TestMod.LogDebug($"Got an effect request [{request.id}:{request.code}].");
        if (string.IsNullOrWhiteSpace(request.code))
        {
            StopAllEffects();
            return true;
        }

        if (!Effects.TryGetValue(request.code, out Effect effect))
        {
            TestMod.LogError($"Effect {request.code} not found.");
            //could not find the effect
            return await Respond(request, EffectStatus.Unavailable);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (effect.Type == Effect.EffectType.BidWar)
            return await Respond(request, EffectStatus.Unavailable, null, "Bid wars are not supported in Crowd Control 2.");
#pragma warning restore CS0618 // Type or member is obsolete

        if (!effect.TryStop())
            return await Respond(request, EffectStatus.Retry);

        TestMod.LogDebug($"Effect {request.code} started.");
        return await Respond(request, EffectStatus.Success, ((effect.Type == Effect.EffectType.Timed) ? effect.Duration : (SITimeSpan?)null));
    }

    private async Task<bool> OnRequest(EffectRequest request)
    {
        TestMod.LogDebug($"Got an effect request [{request.id}:{request.code}].");
        if (string.IsNullOrWhiteSpace(request.code)) return false;

        if (!Effects.TryGetValue(request.code, out Effect effect))
        {
            TestMod.LogError($"Effect {request.code} not found.");
            //could not find the effect
            return await Respond(request, EffectStatus.Unavailable);
        }

        int len = effect.ParameterTypes.Length;
        ParameterResults? parameters = request.parameters as ParameterResults;

        if ((parameters?.Count ?? 0) < len)
        {
            return await Respond(request, EffectStatus.Failure);
        }

        object[] p = new object[len];
        for (int i = 0; i < len; i++)
        {
            p[i] = Convert.ChangeType(parameters[parameters[i]].Value, effect.ParameterTypes[i]);
        }

#pragma warning disable CS0618 // Type or member is obsolete
        if (effect.Type == Effect.EffectType.BidWar)
        {
            return await Respond(request, EffectStatus.Unavailable, null, "Bid wars are not supported in Crowd Control 2.");
        }
#pragma warning restore CS0618 // Type or member is obsolete

        if (!effect.TryStart(p))
        {
            //Log.Debug($"Effect {request.code} could not start.");
            return await Respond(request, EffectStatus.Retry);
        }

        TestMod.LogDebug($"Effect {request.code} started.");
        return await Respond(request, EffectStatus.Success, ((effect.Type == Effect.EffectType.Timed) ? effect.Duration : (SITimeSpan?)null));
    }

    public void StopAllEffects()
    {
        foreach (Effect effect in Effects.Values) effect.TryStop();
    }

    #endregion
}