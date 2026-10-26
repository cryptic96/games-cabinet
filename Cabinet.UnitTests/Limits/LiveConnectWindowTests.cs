using Cabinet.Service.Limits;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Cabinet.UnitTests.Limits;

/// <summary>
/// Verifies which requests on the live route count as a new connection: a negotiate in any spelling endpoint routing
/// accepts, and any request that carries no connection identifier; never a request that carries one, and nothing off the
/// live route.
/// </summary>
public class LiveConnectWindowTests
{
    [Theory]
    [InlineData("POST", "/cabinet/live/negotiate", "?negotiateVersion=1", true)]
    [InlineData("POST", "/cabinet/live/negotiate/", "?negotiateVersion=1", true)]
    [InlineData("POST", "/cabinet/live/negotiate//", "?negotiateVersion=1", true)]
    [InlineData("POST", "/cabinet/live/NEGOTIATE", "?negotiateVersion=1", true)]
    [InlineData("POST", "/Cabinet/Live/Negotiate/", "?negotiateVersion=1", true)]
    [InlineData("POST", "/cabinet/live//negotiate", "?negotiateVersion=1", true)]
    [InlineData("POST", "/cabinet/live//negotiate/", "", true)]
    [InlineData("GET", "/cabinet/live/negotiate/", "", true)]
    [InlineData("OPTIONS", "/cabinet/live/negotiate/", "", true)]
    [InlineData("POST", "/cabinet/live/negotiate/", "?id=abc", true)]
    [InlineData("GET", "/cabinet/live", "", true)]
    [InlineData("GET", "/cabinet/live/", "", true)]
    [InlineData("POST", "/cabinet/live", "", true)]
    [InlineData("DELETE", "/cabinet/live", "", true)]
    [InlineData("PUT", "/cabinet/live/", "", true)]
    [InlineData("GET", "/cabinet/live", "?id=abc", false)]
    [InlineData("GET", "/cabinet/live/", "?ID=abc", false)]
    [InlineData("POST", "/cabinet/live", "?id=abc", false)]
    [InlineData("DELETE", "/cabinet/live", "?id=abc", false)]
    [InlineData("GET", "/", "", false)]
    [InlineData("POST", "/negotiate", "", false)]
    [InlineData("GET", "/cabinet/livelier", "", false)]
    [InlineData("GET", "/cabinet", "", false)]
    public void A_request_counts_as_a_new_connection_by_what_it_asks_for_not_by_how_its_path_is_spelled(
        string method, string path, string query, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);

        LiveConnectWindow.CountsAsNewConnection(context.Request).Should().Be(expected);
    }
}
