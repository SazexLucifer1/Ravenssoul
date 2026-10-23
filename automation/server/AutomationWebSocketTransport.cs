using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// WebSocket transport (port = HTTP port + 1). The first message must be
///   {"jsonrpc": "2.0", "id": 1, "method": "session.authenticate", "params": {"token": "..."}}
/// within AuthTimeoutMs; afterwards the socket carries JSON-RPC requests and
/// server-pushed notifications {"jsonrpc": "2.0", "method": "event", "params": &lt;event&gt;}
/// for types registered with events.subscribe.
/// </summary>
public partial class AutomationWebSocketTransport : Node
{
    public const int AuthTimeoutMs = 3000;
    public const int CloseUnauthorized = 4401;

    /// <summary>One connected client. Subscriptions are set by events.subscribe.</summary>
    public sealed class Peer
    {
        public WebSocketPeer Socket;
        public bool Authed;
        public ulong Created;
        public List<string> Subscriptions = new();
        public long CloseAt = -1;
    }

    public AutomationServer Server { get; set; }

    private readonly TcpServer _tcp = new();
    private readonly List<Peer> _peers = new();

    public Error Listen(int port, string bindAddress)
    {
        ProcessMode = ProcessModeEnum.Always;
        return _tcp.Listen((ushort)port, bindAddress);
    }

    public int Port() => _tcp.GetLocalPort();

    public int PeerCount() => _peers.Count;

    public void Stop()
    {
        foreach (Peer peer in _peers)
            peer.Socket.Close(1001, "server stopping");
        _peers.Clear();
        _tcp.Stop();
        if (Server != null)
            Server.Events.EventAdded -= OnEvent;
    }

    public override void _Ready()
    {
        Server.Events.EventAdded += OnEvent;
    }

    public override void _Process(double delta)
    {
        while (_tcp.IsListening() && _tcp.IsConnectionAvailable())
        {
            StreamPeerTcp tcp = _tcp.TakeConnection();
            if (_peers.Count >= AutomationProtocol.MaxConnections)
            {
                tcp.DisconnectFromHost();
                continue;
            }
            var ws = new WebSocketPeer
            {
                InboundBufferSize = AutomationProtocol.MaxBodyBytes,
                OutboundBufferSize = AutomationProtocol.MaxScreenshotBytes * 2,
            };
            ws.AcceptStream(tcp);
            _peers.Add(new Peer { Socket = ws, Created = Time.GetTicksMsec() });
        }
        foreach (Peer peer in _peers.ToList())
        {
            WebSocketPeer ws = peer.Socket;
            ws.Poll();
            WebSocketPeer.State state = ws.GetReadyState();
            if (state == WebSocketPeer.State.Closed)
            {
                _peers.Remove(peer);
                continue;
            }
            if (peer.CloseAt >= 0)
            {
                // Rejected peer: the error frame was queued; close once it has been flushed.
                if ((long)Time.GetTicksMsec() >= peer.CloseAt)
                    ws.Close(CloseUnauthorized, "unauthorized");
                continue;
            }
            if (!peer.Authed && Time.GetTicksMsec() - peer.Created > AuthTimeoutMs)
            {
                ws.Close(CloseUnauthorized, "authentication required");
                continue;
            }
            while (state == WebSocketPeer.State.Open && ws.GetAvailablePacketCount() > 0)
                _ = OnMessage(peer, ws.GetPacket().GetStringFromUtf8());
        }
    }

    private async Task OnMessage(Peer peer, string text)
    {
        if (peer.CloseAt >= 0)
            return; // rejected; ignore anything else it sends
        JsonNode message = JsonNodes.Parse(text);
        if (message == null)
        {
            Send(peer, AutomationProtocol.ErrorResponse(null, AutomationProtocol.Error(AutomationProtocol.ParseError, "message is not valid JSON")));
            return;
        }
        if (!peer.Authed)
        {
            string token = "";
            if (message is JsonObject request && JsonNodes.Str(request["method"]) == "session.authenticate" && request["params"] is JsonObject parameters)
                token = JsonNodes.Str(parameters["token"]);
            int code = Server.Authenticate(token);
            JsonNode id = (message as JsonObject)?["id"];
            if (code != 0)
            {
                Send(peer, AutomationProtocol.ErrorResponse(id, AutomationProtocol.Error(code, "authenticate first with a valid token")));
                peer.CloseAt = (long)Time.GetTicksMsec() + 100;
                return;
            }
            peer.Authed = true;
            Send(peer, AutomationProtocol.Response(id, new JsonObject { ["authenticated"] = true }));
            return;
        }
        JsonObject response = await Server.Dispatch(message, new DispatchContext("ws", peer));
        Send(peer, response);
    }

    private void OnEvent(JsonObject entry)
    {
        string type = JsonNodes.Str(entry["type"]);
        foreach (Peer peer in _peers)
        {
            if (peer.Authed && peer.Subscriptions.Count > 0 && (peer.Subscriptions.Contains(type) || peer.Subscriptions.Contains("*")))
                Send(peer, new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "event", ["params"] = entry.DeepClone() });
        }
    }

    private static void Send(Peer peer, JsonObject payload)
    {
        if (peer.Socket.GetReadyState() == WebSocketPeer.State.Open)
            peer.Socket.SendText(JsonNodes.Stringify(payload));
    }
}

/// <summary>Where a request came from (transport name, and the WebSocket peer when there is one).</summary>
public sealed record DispatchContext(string Transport, AutomationWebSocketTransport.Peer Peer = null);
