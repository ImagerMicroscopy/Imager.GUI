using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ImagerAvalonia.Tests.Communication;

/// <summary>
/// Minimal stand-in for Imager.Core's SimpleJSONServer: one request per TCP connection,
/// the client sends a bare JSON object and the server answers with a little-endian
/// int32 length prefix (payload length + 4) followed by the payload.
/// </summary>
internal sealed class FakeImagerCoreServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Func<JObject, byte[]> _respond;
    private readonly Task _acceptLoop;

    public ConcurrentQueue<JObject> ReceivedRequests { get; } = new();

    public int Port { get; }

    /// <summary>When set, the response is written in chunks of this many bytes.</summary>
    public int? ChunkSize { get; set; }

    /// <summary>When set, the server waits this long before answering.</summary>
    public TimeSpan ResponseDelay { get; set; } = TimeSpan.Zero;

    /// <summary>When set, replaces the computed length prefix (to simulate bad framing).</summary>
    public int? LengthPrefixOverride { get; set; }

    public FakeImagerCoreServer(Func<JObject, byte[]> respond)
    {
        _respond = respond;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public static FakeImagerCoreServer RespondingWithJson(Func<JObject, string> respond) =>
        new(req => Encoding.UTF8.GetBytes(respond(req)));

    public static FakeImagerCoreServer RespondingWithJson(string json) =>
        RespondingWithJson(_ => json);

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch
            {
                return;
            }

            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                var request = await ReadJsonObjectAsync(stream);
                ReceivedRequests.Enqueue(request);

                if (ResponseDelay > TimeSpan.Zero)
                    await Task.Delay(ResponseDelay, _cts.Token);

                byte[] payload = _respond(request);
                int prefix = LengthPrefixOverride ?? payload.Length + sizeof(int);

                var framed = new byte[sizeof(int) + payload.Length];
                BitConverter.GetBytes(prefix).CopyTo(framed, 0);
                payload.CopyTo(framed, sizeof(int));

                int chunk = ChunkSize ?? framed.Length;
                for (int offset = 0; offset < framed.Length; offset += chunk)
                {
                    int count = Math.Min(chunk, framed.Length - offset);
                    await stream.WriteAsync(framed.AsMemory(offset, count), _cts.Token);
                    await stream.FlushAsync(_cts.Token);
                    if (ChunkSize != null)
                        await Task.Delay(5, _cts.Token);
                }
            }
            catch
            {
                // Client went away or test was torn down.
            }
        }
    }

    // The client does not frame its requests, so read until a complete JSON object parses.
    private async Task<JObject> ReadJsonObjectAsync(NetworkStream stream)
    {
        var buffer = new List<byte>();
        var chunk = new byte[4096];

        while (true)
        {
            int read = await stream.ReadAsync(chunk, _cts.Token);
            if (read == 0)
                throw new IOException("Client closed the connection before sending a request.");

            buffer.AddRange(chunk.AsSpan(0, read).ToArray());

            try
            {
                return JObject.Parse(Encoding.UTF8.GetString(buffer.ToArray()));
            }
            catch (JsonReaderException)
            {
                // Incomplete, keep reading.
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Stop();
        try { _acceptLoop.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }
}
