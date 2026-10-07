using System.Net;
using Cabinet.Repository.Bgg;
using FluentAssertions;

namespace Cabinet.UnitTests.Bgg;

/// <summary>Proves the token goes only to the BGG API host over HTTPS and the connection never follows a redirect.</summary>
[Trait("Category", "Bgg")]
public class BggTransportTests
{
    private const string Token = "sentinel-token-value";

    [Theory]
    [InlineData("https://boardgamegeek.com/xmlapi2/collection", true)]
    [InlineData("https://BoardGameGeek.com/xmlapi2/collection", true)]
    [InlineData("http://boardgamegeek.com/xmlapi2/collection", false)]
    [InlineData("https://www.boardgamegeek.com/xmlapi2/collection", false)]
    [InlineData("https://boardgamegeek.com.example.org/xmlapi2/collection", false)]
    [InlineData("https://example.org/xmlapi2/collection", false)]
    [InlineData("http://127.0.0.1:5000/xmlapi2/collection", false)]
    public async Task The_token_is_attached_only_to_https_requests_for_the_api_host(string address, bool expected)
    {
        var recorder = new RecordingHandler();

        await Send(new BggAuthHandler(Options(Token)) { InnerHandler = recorder }, address);

        recorder.Authorization.Should().Be(expected ? $"Bearer {Token}" : null);
    }

    [Fact]
    public async Task A_credential_a_caller_put_on_a_request_for_another_host_is_removed()
    {
        var recorder = new RecordingHandler();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.org/");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer something-else");

        using var invoker = new HttpMessageInvoker(new BggAuthHandler(Options(Token)) { InnerHandler = recorder });
        await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        recorder.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task No_token_is_sent_when_none_is_configured()
    {
        var recorder = new RecordingHandler();

        await Send(new BggAuthHandler(Options(null)) { InnerHandler = recorder }, "https://boardgamegeek.com/xmlapi2/collection");

        recorder.Authorization.Should().BeNull();
    }

    [Fact]
    public void The_primary_handler_never_follows_redirects()
    {
        using var handler = BggTransport.CreatePrimaryHandler();

        handler.Should().BeOfType<SocketsHttpHandler>().Which.AllowAutoRedirect.Should().BeFalse();
    }

    [Fact]
    public void The_user_agent_names_the_product_and_adds_the_contact_address_only_when_one_is_set()
    {
        BggTransport.UserAgent(Options(Token)).Should().Be("GamesCabinet/1.2.3");
        BggTransport.UserAgent(Options(Token) with { ContactUrl = "https://example.com/repository" })
            .Should().Be("GamesCabinet/1.2.3 (+https://example.com/repository)");
    }

    [Fact]
    public void Printing_the_options_never_reveals_the_token_or_the_username()
    {
        var text = Options(Token).ToString();

        text.Should().NotContain(Token).And.NotContain("sentinel-user-name");
    }

    private static BggOptions Options(string? token) =>
        new(BggOptions.DefaultBaseUri, "sentinel-user-name", token, null, BggOptions.MinimumRequestGap, false, "1.2.3");

    private static async Task Send(DelegatingHandler handler, string address)
    {
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, address);

        await invoker.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
