using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// Protocol constants shared by transports, dispatcher, and adapters.
/// Wire format: JSON-RPC 2.0. Method table: res://automation/protocol/methods.json.
/// </summary>
public static class AutomationProtocol
{
    public const string Name = "utopia-automation";
    public const string Version = "1.0";
    public const string MethodsPath = "res://automation/protocol/methods.json";

    // JSON-RPC standard codes.
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;
    // Protocol-specific codes.
    public const int Unauthorized = -32001;
    public const int Forbidden = -32002;
    public const int RateLimited = -32003;
    public const int NotFound = -32004;
    public const int Unsupported = -32005;
    public const int NotInteractable = -32006;
    public const int Timeout = -32007;
    public const int SessionExpired = -32008;
    public const int Busy = -32009;
    public const int PayloadTooLarge = -32010;

    public static readonly IReadOnlyDictionary<int, string> ErrorKinds = new Dictionary<int, string>
    {
        [ParseError] = "parse_error", [InvalidRequest] = "invalid_request",
        [MethodNotFound] = "method_not_found", [InvalidParams] = "invalid_params",
        [InternalError] = "internal", [Unauthorized] = "unauthorized", [Forbidden] = "forbidden",
        [RateLimited] = "rate_limited", [NotFound] = "not_found", [Unsupported] = "unsupported",
        [NotInteractable] = "not_interactable", [Timeout] = "timeout",
        [SessionExpired] = "session_expired", [Busy] = "busy", [PayloadTooLarge] = "payload_too_large",
    };

    // Limits (also reported by protocol.capabilities).
    public const int MaxBodyBytes = 65536;
    public const int MaxHeaderBytes = 8192;
    public const int MaxConnections = 16;
    public const int ReadTimeoutMs = 5000;
    public const double RateBurst = 240.0;
    public const double RatePerSecond = 120.0;
    public const int SessionIdleTimeoutMs = 120000;
    public const int DefaultTokenTtlS = 3600;
    public const int RemoteMaxTokenTtlS = 900;
    public const int MinTokenLength = 32;
    public const int EventBuffer = 1000;
    public const int LogBuffer = 500;
    public const int MaxScreenshotBytes = 8 * 1024 * 1024;

    private static JsonObject _methodsCache;

    /// <summary>method name -> {"capability", "params" (schema), ...}. Treat as read-only.</summary>
    public static JsonObject Methods()
    {
        if (_methodsCache == null || _methodsCache.Count == 0)
            _methodsCache = SchemaDocument()?["methods"] as JsonObject ?? new JsonObject();
        return _methodsCache;
    }

    public static JsonObject SchemaDocument() =>
        JsonNodes.Parse(FileAccess.GetFileAsString(MethodsPath)) as JsonObject;

    public static JsonObject Error(int code, string message, JsonObject extra = null)
    {
        var data = new JsonObject { ["kind"] = ErrorKinds.TryGetValue(code, out string kind) ? kind : "error" };
        if (extra != null)
            foreach ((string key, JsonNode value) in extra)
                data[key] = JsonNodes.Copy(value);
        return new JsonObject { ["code"] = code, ["message"] = message, ["data"] = data };
    }

    public static JsonObject Response(JsonNode id, JsonNode result) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = JsonNodes.Copy(id), ["result"] = JsonNodes.Copy(result) };

    public static JsonObject ErrorResponse(JsonNode id, JsonObject error) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = JsonNodes.Copy(id), ["error"] = JsonNodes.Copy(error) };

    /// <summary>Adapter/handler success value: {"result": ...}.</summary>
    public static JsonObject Ok(JsonNode result) => new() { ["result"] = JsonNodes.Copy(result) };

    /// <summary>Adapter/handler failure value: {"error": {...}}.</summary>
    public static JsonObject Fail(int code, string message, JsonObject extra = null) => new() { ["error"] = Error(code, message, extra) };
}
