using Cabinet.Service.Limits;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Cabinet.UnitTests.Limits;

/// <summary>
/// Verifies the origin comparison on the live route: the request scheme decides the default port and whether the origin must
/// be secure, with the WebSocket schemes counting as their HTTP counterparts, and everything unclear is refused.
/// </summary>
public class LiveOriginGuardTests
{
    [Theory]
    [InlineData("https", "cabinet.example.com", "https://cabinet.example.com", true)]
    [InlineData("wss", "cabinet.example.com", "https://cabinet.example.com", true)]
    [InlineData("WSS", "cabinet.example.com", "https://cabinet.example.com", true)]
    [InlineData("http", "cabinet.example.com", "http://cabinet.example.com", true)]
    [InlineData("ws", "cabinet.example.com", "http://cabinet.example.com", true)]
    [InlineData("wss", "cabinet.example.com", "http://cabinet.example.com", false)]
    [InlineData("https", "cabinet.example.com", "http://cabinet.example.com", false)]
    [InlineData("ws", "cabinet.example.com", "https://cabinet.example.com", false)]
    [InlineData("http", "cabinet.example.com", "https://cabinet.example.com", false)]
    [InlineData("wss", "cabinet.example.com", "https://cabinet.example.com:8443", false)]
    [InlineData("wss", "cabinet.example.com", "https://elsewhere.example.org", false)]
    [InlineData("wss", "cabinet.example.com:8443", "https://cabinet.example.com:8443", true)]
    [InlineData("wss", "cabinet.example.com:8443", "http://cabinet.example.com:8443", false)]
    [InlineData("ws", "cabinet.example.com:8080", "http://cabinet.example.com:8080", true)]
    [InlineData("wss", "cabinet.example.com", "null", false)]
    [InlineData("wss", "cabinet.example.com", "ftp://cabinet.example.com", false)]
    public void The_origin_must_match_the_request_host_scheme_and_port(
        string scheme, string host, string origin, bool expected)
    {
        var request = RequestFor(scheme, host, origin);

        LiveOriginGuard.IsAllowed(request).Should().Be(expected);
    }

    [Theory]
    [InlineData("wss")]
    [InlineData("ws")]
    [InlineData("https")]
    public void A_request_without_an_origin_is_allowed_on_any_scheme(string scheme)
    {
        var request = RequestFor(scheme, "cabinet.example.com", origin: null);

        LiveOriginGuard.IsAllowed(request).Should().BeTrue();
    }

    [Fact]
    public void Repeated_origin_headers_are_refused()
    {
        var request = RequestFor("wss", "cabinet.example.com", origin: null);
        request.Headers["Origin"] = new StringValues(["https://cabinet.example.com", "https://cabinet.example.com"]);

        LiveOriginGuard.IsAllowed(request).Should().BeFalse();
    }

    private static HttpRequest RequestFor(string scheme, string host, string? origin)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = scheme;
        context.Request.Host = HostString.FromUriComponent(host);
        if (origin is not null)
        {
            context.Request.Headers["Origin"] = origin;
        }

        return context.Request;
    }
}
