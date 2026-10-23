using System.Collections.Generic;
using System.Text;
using Godot;

namespace Utopia.Automation;

/// <summary>
/// Bearer-token authentication with expiry, plus request-origin checks that
/// block browser-based attacks on localhost (DNS rebinding, CSRF).
/// </summary>
public class AutomationAuth
{
    public string Token { get; }
    public long ExpiresAtUnix { get; }
    public string BindAddress { get; }
    public List<string> AllowedHosts { get; } = new();

    public AutomationAuth(string token, int ttlSeconds, string bindAddress, int port)
    {
        Token = token;
        ExpiresAtUnix = (long)Time.GetUnixTimeFromSystem() + ttlSeconds;
        BindAddress = bindAddress;
        foreach (string host in new[] { "127.0.0.1", "localhost", "[::1]" })
        {
            AllowedHosts.Add($"{host}:{port}");
            AllowedHosts.Add($"{host}:{port + 1}");
        }
        if (!IsLoopback(bindAddress))
        {
            AllowedHosts.Add($"{bindAddress}:{port}");
            AllowedHosts.Add($"{bindAddress}:{port + 1}");
        }
    }

    public static string GenerateToken() => System.Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static bool IsLoopback(string address) => address is "127.0.0.1" or "::1" or "localhost";

    public bool IsExpired() => Time.GetUnixTimeFromSystem() >= ExpiresAtUnix;

    /// <summary>Returns 0 when authorized, else an AutomationProtocol error code.</summary>
    public int CheckToken(string presented)
    {
        presented ??= "";
        if (presented.Length != Token.Length ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(Token)))
            return AutomationProtocol.Unauthorized;
        if (IsExpired())
            return AutomationProtocol.SessionExpired;
        return 0;
    }

    /// <summary>
    /// Browsers always send Origin on cross-site requests; automation clients don't.
    /// A wrong Host header means DNS rebinding. Both are rejected.
    /// </summary>
    public string CheckHttpHeaders(IReadOnlyDictionary<string, string> headers)
    {
        if (headers.ContainsKey("origin"))
            return "browser origins are not allowed";
        string host = headers.TryGetValue("host", out string value) ? value : "";
        if (!AllowedHosts.Contains(host))
            return "unexpected host header";
        return "";
    }

    public static string BearerFrom(IReadOnlyDictionary<string, string> headers)
    {
        string value = headers.TryGetValue("authorization", out string found) ? found : "";
        return value.StartsWith("Bearer ") ? value[7..].Trim() : "";
    }
}
