using System.Text;
using Utopia.Automation;

namespace Utopia.Tests.Unit.Automation;

public class TestHttpParsing : TestCase
{
    private static AutomationHttpTransport.ParsedRequest Parse(string raw) =>
        AutomationHttpTransport.TryParse(Encoding.UTF8.GetBytes(raw));

    public void TestCompleteRequest()
    {
        AutomationHttpTransport.ParsedRequest parsed = Parse("POST /v1/rpc HTTP/1.1\r\nHost: x\r\nContent-Length: 2\r\n\r\n{}");
        AssertEq(parsed.Method, "POST");
        AssertEq(parsed.Path, "/v1/rpc");
        AssertEq(parsed.Headers["host"], "x");
        AssertEq(parsed.Body, "{}");
    }

    public void TestIncompleteRequestsWaitForMoreData()
    {
        AssertTrue(Parse("POST /v1/rpc HTTP/1.1\r\nContent-Le").IsIncomplete);
        AssertTrue(Parse("POST /v1/rpc HTTP/1.1\r\nContent-Length: 10\r\n\r\n{}").IsIncomplete);
    }

    public void TestMalformedRequestsAreRejected()
    {
        AssertEq(Parse("GARBAGE\r\n\r\n").Status, 400);
        AssertEq(Parse("POST /v1/rpc HTTP/1.1\r\nno-colon-header\r\n\r\n").Status, 400);
        AssertEq(Parse("POST /v1/rpc HTTP/1.1\r\nContent-Length: -5\r\n\r\n").Status, 400);
        AssertEq(Parse("POST /v1/rpc HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n").Status, 400);
    }

    public void TestSizeLimits()
    {
        AssertEq(Parse("POST /v1/rpc HTTP/1.1\r\nContent-Length: 999999\r\n\r\n").Status, 413);
        AssertEq(Parse($"POST /v1/rpc HTTP/1.1\r\nX: {new string('a', 9000)}\r\n\r\n").Status, 431);
        AssertEq(Parse($"POST /v1/rpc HTTP/1.1\r\nX: {new string('a', 9000)}").Status, 431, "no header end yet");
    }
}
