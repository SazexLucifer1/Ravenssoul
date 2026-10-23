using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using Utopia.Automation;

namespace Utopia.Tests;

/// <summary>
/// Helpers for automation tests: an in-process server on ephemeral ports,
/// direct dispatch, and raw HTTP / WebSocket clients over real sockets.
/// </summary>
public abstract class AutomationTestCase : TestCase
{
    public const string Token = "0123456789abcdef0123456789abcdef0123456789abcdef";

    public AutomationServer Server { get; private set; }

    private int _nextId = 1;

    public AutomationServer StartServer(string profile = "qa", JsonObject extra = null)
    {
        Server = new AutomationServer { Name = "Automation" };
        var config = new JsonObject
        {
            ["profile"] = profile, ["port"] = 0, ["token"] = Token, ["badge"] = false, ["write_session_file"] = false,
        };
        foreach ((string key, JsonNode value) in extra ?? new JsonObject())
            config[key] = JsonNodes.Copy(value);
        Server.Configure(config);
        Tree.Root.AddChild(Server);
        AssertEq(Server.Start(), "", "server start");
        return Server;
    }

    public override async Task AfterEach()
    {
        if (Server != null && GodotObject.IsInstanceValid(Server))
        {
            Server.Stop();
            await WaitFrames(2); // let pending handler coroutines observe the stop
            Server.QueueFree();
            await WaitFrames(1);
        }
        Server = null;
    }

    /// <summary>Dispatch without sockets. Returns the JSON-RPC response.</summary>
    public Task<JsonObject> Rpc(string method, JsonObject parameters = null)
    {
        _nextId++;
        return Server.Dispatch(new JsonObject
        {
            ["jsonrpc"] = "2.0", ["id"] = _nextId, ["method"] = method, ["params"] = parameters ?? new JsonObject(),
        }, new DispatchContext("test"));
    }

    public JsonNode ResultOf(JsonObject response)
    {
        if (response.ContainsKey("error"))
        {
            Fail($"unexpected error: {JsonNodes.Stringify(response["error"])}");
            return new JsonObject();
        }
        return response["result"];
    }

    public static string ErrorKind(JsonObject response) =>
        JsonNodes.Str((response["error"] as JsonObject)?["data"]?["kind"]);

    public readonly record struct HttpReply(int Status, JsonNode Body);

    /// <summary>Raw HTTP over a real TCP socket.</summary>
    public async Task<HttpReply> Http(string rawRequest, int port = -1)
    {
        var peer = new StreamPeerTcp();
        peer.ConnectToHost("127.0.0.1", port < 0 ? Server.Http.Port() : port);
        ulong deadline = Time.GetTicksMsec() + 5000;
        while (peer.GetStatus() == StreamPeerTcp.Status.Connecting && Time.GetTicksMsec() < deadline)
        {
            await WaitFrames(1);
            peer.Poll();
        }
        peer.PutData(Encoding.UTF8.GetBytes(rawRequest));
        var data = new List<byte>();
        while (Time.GetTicksMsec() < deadline)
        {
            await WaitFrames(1);
            peer.Poll();
            if (peer.GetStatus() != StreamPeerTcp.Status.Connected)
                break;
            int available = peer.GetAvailableBytes();
            if (available > 0)
                data.AddRange(peer.GetData(available)[1].AsByteArray());
        }
        string text = Encoding.UTF8.GetString(data.ToArray());
        int split = text.IndexOf("\r\n\r\n");
        if (split < 0)
            return new HttpReply(0, null);
        int status = int.TryParse(text.Split(' ')[1], out int code) ? code : 0;
        return new HttpReply(status, JsonNodes.Parse(text[(split + 4)..]));
    }

    /// <summary>POST /v1/rpc with default headers; a null header value removes that header.</summary>
    public Task<HttpReply> HttpPost(string body, Dictionary<string, string> headers = null)
    {
        var all = new Dictionary<string, string>
        {
            ["Host"] = $"127.0.0.1:{Server.Http.Port()}",
            ["Content-Type"] = "application/json",
            ["Authorization"] = "Bearer " + Token,
            ["Content-Length"] = Encoding.UTF8.GetByteCount(body).ToString(),
        };
        foreach ((string key, string value) in headers ?? new Dictionary<string, string>())
            all[key] = value;
        var head = new StringBuilder("POST /v1/rpc HTTP/1.1\r\n");
        foreach ((string key, string value) in all)
            if (value != null)
                head.Append($"{key}: {value}\r\n");
        return Http(head + "\r\n" + body);
    }

    public async Task<WebSocketPeer> WsConnect()
    {
        var ws = new WebSocketPeer();
        ws.ConnectToUrl($"ws://127.0.0.1:{Server.WebSocket.Port()}");
        ulong deadline = Time.GetTicksMsec() + 5000;
        while (ws.GetReadyState() == WebSocketPeer.State.Connecting && Time.GetTicksMsec() < deadline)
        {
            await WaitFrames(1);
            ws.Poll();
        }
        return ws;
    }

    public static void WsSend(WebSocketPeer ws, JsonObject payload) => ws.SendText(JsonNodes.Stringify(payload));

    /// <summary>Next message (or null on timeout/close).</summary>
    public async Task<JsonObject> WsReceive(WebSocketPeer ws, int timeoutMs = 3000)
    {
        ulong deadline = Time.GetTicksMsec() + (ulong)timeoutMs;
        while (Time.GetTicksMsec() < deadline)
        {
            ws.Poll();
            if (ws.GetAvailablePacketCount() > 0)
                return JsonNodes.Parse(ws.GetPacket().GetStringFromUtf8()) as JsonObject;
            if (ws.GetReadyState() == WebSocketPeer.State.Closed)
                return null;
            await WaitFrames(1);
        }
        return null;
    }
}
