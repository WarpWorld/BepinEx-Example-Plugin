using System.Net.Sockets;
using System.Text;

namespace CrowdControl;

/// <summary>Reads data from a socket until a null terminator is encountered.</summary>
/// <param name="stream">The network stream to read from.</param>
/// <remarks>
/// Many network stream parser implementations either don't properly handle multibyte UTF-8 characters or perform wasteful microallocations.
/// This class avoids both problems and is recommended for use in production code when possible.
/// </remarks>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class DelimitedStreamReader(NetworkStream stream) : IDisposable
{
    private readonly MemoryStream _memory_stream = new();

    ~DelimitedStreamReader() => Dispose(false);

    /// <summary>Disposes the internal memory buffer.</summary>
    public void Dispose() => Dispose(true);

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing) return;
        try { _memory_stream.Dispose(); }
        catch { /**/ }
    }

    /// <summary>
    /// The maximum allowed size of a single message, in bytes.
    /// Crowd Control messages are small (well under a kilobyte), so anything approaching this size
    /// means the stream is corrupt or the peer isn't a Crowd Control client. Aborting the read keeps
    /// a bad peer from growing the buffer without bound; the caller will drop and retry the connection.
    /// </summary>
    private const int MAX_MESSAGE_SIZE = 1024 * 1024;

    /// <summary>Reads data from the socket until a null terminator is encountered.</summary>
    /// <returns>The data read from the socket.</returns>
    public string ReadUntilNullTerminator()
    {
        int byteRead;

        while ((byteRead = stream.ReadByte()) != -1)
        {
            if (byteRead == 0x00) // null terminator
                break;

            if (_memory_stream.Length >= MAX_MESSAGE_SIZE)
            {
                _memory_stream.SetLength(0);
                throw new InvalidDataException("Message exceeded the maximum allowed size without a null terminator. Dropping the connection.");
            }

            _memory_stream.WriteByte((byte)byteRead);
        }

        if (byteRead == -1)
            throw new EndOfStreamException("Reached end of stream without finding a null terminator.");

        string result = Encoding.UTF8.GetString(_memory_stream.ToArray());
        _memory_stream.SetLength(0);
        return result;
    }
}
