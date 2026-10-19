// One TCP connection to the game server: frames and sends client packets, and reads server packets on a background
// thread into a queue the driver takes from with a deadline.
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;

namespace DaocServer.HeadlessClient;

public sealed class Connection : IDisposable
{
    public sealed record Packet(int Code, byte[] Data);        // Code -1 = the connection ended (Data = why)

    readonly TcpClient _tcp;
    readonly NetworkStream _stream;
    readonly BlockingCollection<Packet> _inbox = new();
    readonly object _sendLock = new();
    ushort _sequence;
    public int SessionId;

    public Connection(string host, int port, TimeSpan connectTimeout)
    {
        _tcp = new TcpClient { NoDelay = true };
        if (!_tcp.ConnectAsync(host, port).Wait(connectTimeout))
            throw new TimeoutException($"no answer from {host}:{port} within {connectTimeout.TotalSeconds:0} s");
        _stream = _tcp.GetStream();
        new Thread(ReadLoop) { IsBackground = true, Name = "HeadlessClient reader" }.Start();
    }

    public void Send(byte code, byte[] body)
    {
        lock (_sendLock)
        {
            byte[] buf = Packets.Frame(code, _sequence++, SessionId, body);
            _stream.Write(buf, 0, buf.Length);
        }
    }

    // The next server packet, or null when the deadline passes first.
    public Packet? Next(DateTime deadline)
    {
        var left = deadline - DateTime.UtcNow;
        if (left < TimeSpan.Zero) left = TimeSpan.Zero;
        return _inbox.TryTake(out var p, left) ? p : null;
    }

    // Runs until the socket closes; every iteration reads one packet or ends the loop.
    void ReadLoop()
    {
        var head = new byte[Packets.ServerHeaderSize];
        try
        {
            while (true)
            {
                if (!ReadExactly(head)) break;
                var data = new byte[(head[0] << 8) | head[1]];
                if (!ReadExactly(data)) break;
                _inbox.Add(new Packet(head[2], data));
            }
            _inbox.Add(new Packet(-1, Packets.Text1252.GetBytes("the server closed the connection")));
        }
        catch (Exception e) when (e is System.IO.IOException or ObjectDisposedException or InvalidOperationException)
        {
            _inbox.Add(new Packet(-1, Packets.Text1252.GetBytes(e.Message)));
        }
    }

    bool ReadExactly(byte[] buf)
    {
        int got = 0;
        while (got < buf.Length)
        {
            int n = _stream.Read(buf, got, buf.Length - got);
            if (n <= 0) return false;
            got += n;
        }
        return true;
    }

    public void Dispose()
    {
        try { _tcp.Close(); } catch (Exception) { }
    }
}
