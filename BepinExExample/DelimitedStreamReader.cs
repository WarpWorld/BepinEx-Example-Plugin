﻿using System.Net.Sockets;
using System.Text;

namespace CrowdControl;

/// <summary>
/// Reads data from a socket until a null terminator is encountered.
/// </summary>
public class DelimitedStreamReader(NetworkStream stream) : IDisposable
{
    private readonly MemoryStream _memory_stream = new();

    ~DelimitedStreamReader() => Dispose(false);

    public void Dispose() => Dispose(true);

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        try { _memory_stream.Dispose(); }
        catch { /**/ }
    }

    /// <summary>
    /// Reads data from the socket until a null terminator is encountered.
    /// </summary>
    /// <returns>The data read from the socket.</returns>
    public string ReadUntilNullTerminator()
    {
        int byteRead;

        while ((byteRead = stream.ReadByte()) != -1)
        {
            if (byteRead == 0x00) // null terminator
                break;

            _memory_stream.WriteByte((byte)byteRead);
        }

        if (byteRead == -1)
            throw new EndOfStreamException("Reached end of stream without finding a null terminator.");

        string result = Encoding.UTF8.GetString(_memory_stream.ToArray());
        _memory_stream.SetLength(0);
        return result;
    }
}
