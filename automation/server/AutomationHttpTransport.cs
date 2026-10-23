using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// Minimal HTTP/1.1 server for the automation protocol.
///   GET  /v1/health  -> {"ok": true, "protocol", "version"}   (no token; Host/Origin checked)
///   POST /v1/rpc     -> JSON-RPC 2.0 (Authorization: Bearer &lt;token&gt;)
/// One request per connection (Connection: close). Enforces header/body size
/// limits, a read timeout, and a connection cap.
/// </summary>
public partial class AutomationHttpTransport : Node
{
    public static readonly IReadOnlyDictionary<int, string> StatusText = new Dictionary<int, string>
    {
        [200] = "OK", [400] = "Bad Request", [401] = "Unauthorized", [403] = "Forbidden", [404] = "Not Found",
        [405] = "Method Not Allowed", [408] = "Request Timeout", [413] = "Payload Too Large",
        [415] = "Unsupported Media Type", [429] = "Too Many Requests", [431] = "Request Header Fields Too Large",
        [503] = "Service Unavailable",
    };

    /// <summary>Result of parsing a buffered request: incomplete, rejected (Status), or complete (Method).</summary>
    public sealed class ParsedRequest
    {
        public int Status { get; init; }
        public string Reason { get; init; } = "";
        public string Method { get; init; } = "";
        public string Path { get; init; } = "";
        public Dictionary<string, string> Headers { get; init; } = new();
        public string Body { get; init; } = "";
        public bool IsIncomplete => Status == 0 && Method.Length == 0;
    }

    private sealed class Connection
    {
        public StreamPeerTcp Peer;
        public readonly List<byte> Buffer = new();
        public ulong Started;
        public bool Busy;
    }

    public AutomationServer Server { get; set; }

    private readonly TcpServer _tcp = new();
    private readonly List<Connection> _connections = new();

    public Error Listen(int port, string bindAddress)
    {
        ProcessMode = ProcessModeEnum.Always;
        return _tcp.Listen((ushort)port, bindAddress);
    }

    public int Port() => _tcp.GetLocalPort();

    public int ConnectionCount() => _connections.Count;

    public void Stop()
    {
        foreach (Connection conn in _connections)
            conn.Peer.DisconnectFromHost();
        _connections.Clear();
        _tcp.Stop();
    }

    public override void _Process(double delta)
    {
        while (_tcp.IsListening() && _tcp.IsConnectionAvailable())
        {
            StreamPeerTcp peer = _tcp.TakeConnection();
            if (_connections.Count >= AutomationProtocol.MaxConnections)
            {
                SendRaw(peer, 503, new JsonObject { ["error"] = "too many connections" });
                continue;
            }
            peer.SetNoDelay(true);
            _connections.Add(new Connection { Peer = peer, Started = Time.GetTicksMsec() });
        }
        foreach (Connection conn in _connections.ToList())
        {
            if (conn.Busy)
                continue;
            StreamPeerTcp peer = conn.Peer;
            peer.Poll();
            if (peer.GetStatus() != StreamPeerTcp.Status.Connected)
            {
                _connections.Remove(conn);
                continue;
            }
            int available = peer.GetAvailableBytes();
            if (available > 0)
            {
                Godot.Collections.Array chunk = peer.GetPartialData(available);
                if ((Error)chunk[0].AsInt32() == Error.Ok)
                    conn.Buffer.AddRange(chunk[1].AsByteArray());
            }
            ParsedRequest parsed = TryParse(conn.Buffer.ToArray());
            if (parsed.Status != 0)
            {
                Finish(conn, parsed.Status, new JsonObject { ["error"] = parsed.Reason });
            }
            else if (!parsed.IsIncomplete)
            {
                conn.Busy = true;
                _ = Handle(conn, parsed);
            }
            else if (Time.GetTicksMsec() - conn.Started > AutomationProtocol.ReadTimeoutMs)
            {
                Finish(conn, 408, new JsonObject { ["error"] = "request not completed in time" });
            }
        }
    }

    public static ParsedRequest TryParse(byte[] buffer)
    {
        int headerEnd = FindHeaderEnd(buffer);
        if (headerEnd < 0)
        {
            if (buffer.Length > AutomationProtocol.MaxHeaderBytes)
                return new ParsedRequest { Status = 431, Reason = "headers too large" };
            return new ParsedRequest();
        }
        if (headerEnd > AutomationProtocol.MaxHeaderBytes)
            return new ParsedRequest { Status = 431, Reason = "headers too large" };
        string head = Encoding.ASCII.GetString(buffer, 0, headerEnd);
        string[] lines = head.Split("\r\n");
        string[] requestLine = lines[0].Split(' ');
        if (requestLine.Length != 3 || !requestLine[2].StartsWith("HTTP/1."))
            return new ParsedRequest { Status = 400, Reason = "malformed request line" };
        var headers = new Dictionary<string, string>();
        for (int i = 1; i < lines.Length; i++)
        {
            int colon = lines[i].IndexOf(':');
            if (colon <= 0)
                return new ParsedRequest { Status = 400, Reason = "malformed header" };
            headers[lines[i][..colon].Trim().ToLowerInvariant()] = lines[i][(colon + 1)..].Trim();
        }
        int length = 0;
        if (headers.TryGetValue("content-length", out string lengthText))
        {
            if (!int.TryParse(lengthText, out length) || length < 0)
                return new ParsedRequest { Status = 400, Reason = "invalid content-length" };
        }
        if (headers.ContainsKey("transfer-encoding"))
            return new ParsedRequest { Status = 400, Reason = "chunked bodies are not supported" };
        if (length > AutomationProtocol.MaxBodyBytes)
            return new ParsedRequest { Status = 413, Reason = $"body larger than {AutomationProtocol.MaxBodyBytes} bytes" };
        int bodyStart = headerEnd + 4;
        if (buffer.Length - bodyStart < length)
            return new ParsedRequest();
        return new ParsedRequest
        {
            Method = requestLine[0],
            Path = requestLine[1],
            Headers = headers,
            Body = Encoding.UTF8.GetString(buffer, bodyStart, length),
        };
    }

    private static int FindHeaderEnd(byte[] buffer)
    {
        for (int i = 0; i < buffer.Length - 3; i++)
            if (buffer[i] == 13 && buffer[i + 1] == 10 && buffer[i + 2] == 13 && buffer[i + 3] == 10)
                return i;
        return -1;
    }

    private async Task Handle(Connection conn, ParsedRequest request)
    {
        string rejection = Server.Auth.CheckHttpHeaders(request.Headers);
        if (rejection.Length > 0)
        {
            Finish(conn, 403, new JsonObject { ["error"] = rejection });
            return;
        }
        switch (request.Path)
        {
            case "/v1/health":
                if (request.Method != "GET")
                {
                    Finish(conn, 405, new JsonObject { ["error"] = "use GET" });
                    return;
                }
                Finish(conn, 200, new JsonObject { ["ok"] = true, ["protocol"] = AutomationProtocol.Name, ["version"] = AutomationProtocol.Version });
                return;
            case "/v1/rpc":
                if (request.Method != "POST")
                {
                    Finish(conn, 405, new JsonObject { ["error"] = "use POST" });
                    return;
                }
                if (!request.Headers.TryGetValue("content-type", out string contentType) || !contentType.StartsWith("application/json"))
                {
                    Finish(conn, 415, new JsonObject { ["error"] = "content-type must be application/json" });
                    return;
                }
                int authCode = Server.Authenticate(AutomationAuth.BearerFrom(request.Headers));
                if (authCode != 0)
                {
                    Finish(conn, 401, AutomationProtocol.ErrorResponse(null, AutomationProtocol.Error(authCode,
                        authCode == AutomationProtocol.Unauthorized ? "missing or invalid token" : "token expired")));
                    return;
                }
                JsonNode body = JsonNodes.Parse(request.Body);
                if (body == null)
                {
                    Finish(conn, 400, AutomationProtocol.ErrorResponse(null, AutomationProtocol.Error(AutomationProtocol.ParseError, "body is not valid JSON")));
                    return;
                }
                JsonObject response = await Server.Dispatch(body, new DispatchContext("http"));
                int code = JsonNodes.Int((response["error"] as JsonObject)?["code"]);
                Finish(conn, code == AutomationProtocol.RateLimited ? 429 : 200, response);
                return;
            default:
                Finish(conn, 404, new JsonObject { ["error"] = "not found" });
                return;
        }
    }

    private void Finish(Connection conn, int status, JsonObject payload)
    {
        SendRaw(conn.Peer, status, payload);
        _connections.Remove(conn);
    }

    private static void SendRaw(StreamPeerTcp peer, int status, JsonObject payload)
    {
        if (peer.GetStatus() != StreamPeerTcp.Status.Connected)
            return;
        byte[] body = Encoding.UTF8.GetBytes(JsonNodes.Stringify(payload));
        string statusText = StatusText.TryGetValue(status, out string text) ? text : "Error";
        string head = $"HTTP/1.1 {status} {statusText}\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\n" +
            "Cache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n";
        peer.PutData(Encoding.ASCII.GetBytes(head));
        peer.PutData(body);
        peer.DisconnectFromHost();
    }
}
