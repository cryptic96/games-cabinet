using System.Net;
using System.Web;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Bgg;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves the BGG token and username exist only on the server: they reach BGG and nothing else, never appear in a log line or
/// in anything a visitor receives, and a visitor can neither steer the request nor cause one.
/// </summary>
[Trait("Category", "Secrets")]
public class SecretsStayServerSideTests
{
    private const string Username = "sentinel-user-name";
    private const string Token = "sentinel-token-value";
    private const string SyncPath = "/cabinet/sync";
    private const string LayoutPath = "/cabinet/layout?profile=desktop";
    private const string BeingFilled = "The cabinet is being filled.";
    private const string NotConfiguredWarning = "BGG username or token is not configured; syncs are skipped until both are set.";

    [Fact]
    public async Task After_a_good_and_a_failing_sync_no_log_line_and_no_public_response_carries_either_secret()
    {
        var logs = new CapturingLoggerProvider();
        var good = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5));
        var failing = new ScriptedBggHandler(_ => ScriptedResponse.Empty(HttpStatusCode.InternalServerError));
        var responses = new List<string>();

        await using (var factory = CreateFactory(good, logs))
        {
            using var client = factory.CreatePublicClient();
            var initial = await Get(client, LayoutPath);
            responses.Add(await PostSync(client));
            await WaitForLayoutChange(client, initial.ETag);
            responses.Add((await Get(client, "/")).Everything);
            responses.Add((await Get(client, LayoutPath)).Everything);
        }

        await using (var factory = CreateFactory(failing, logs))
        {
            using var client = factory.CreatePublicClient();
            responses.Add(await PostSync(client));
            await WaitForLine(logs, "BGG sync failed");
            responses.Add((await Get(client, "/")).Everything);
            responses.Add((await Get(client, LayoutPath)).Everything);
        }

        good.Requests.Should().HaveCount(2);
        logs.Lines.Should().NotBeEmpty();
        logs.Lines.Should().OnlyContain(line => !line.Contains(Token, StringComparison.Ordinal) && !line.Contains(Username, StringComparison.Ordinal));
        responses.Should().OnlyContain(text => !text.Contains(Token, StringComparison.Ordinal) && !text.Contains(Username, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_request_to_bgg_carries_the_token_and_an_honest_user_agent()
    {
        var logs = new CapturingLoggerProvider();
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5));
        await using var factory = CreateFactory(handler, logs);
        using var client = factory.CreatePublicClient();
        var initial = await Get(client, LayoutPath);

        await PostSync(client);
        await WaitForLayoutChange(client, initial.ETag);

        handler.Requests.Should().NotBeEmpty();
        handler.Requests.Should().OnlyContain(request =>
            request.Uri.Host == BggOptions.ApiHost
            && request.Uri.Scheme == "https"
            && request.AuthorizationScheme == "Bearer"
            && request.AuthorizationParameter == Token
            && request.UserAgent!.StartsWith("GamesCabinet/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_production_transport_does_not_follow_a_redirect_to_another_address()
    {
        await using var target = await LoopbackListener.Start(context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;

            return Task.CompletedTask;
        });
        await using var redirector = await LoopbackListener.Start(context =>
        {
            context.Response.StatusCode = StatusCodes.Status301MovedPermanently;
            context.Response.Headers.Location = target.BaseAddress + "xmlapi2/collection";

            return Task.CompletedTask;
        });
        var options = SyncHarness.Options() with { BaseUri = redirector.BaseAddress };
        await using var factory = new CabinetWebApplicationFactory(
            new Dictionary<string, string?>(),
            services =>
            {
                services.AddSingleton(options);
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
            });
        using var client = factory.CreatePublicClient();

        using var press = await client.PostAsync(SyncPath, content: null, TestContext.Current.CancellationToken);
        await SyncHarness.WaitForRunToEnd(client);

        press.StatusCode.Should().Be(HttpStatusCode.Accepted);
        (await SyncHarness.ReadStatus(client)).LastResult.Should().Be("failed");
        redirector.RequestCount.Should().Be(1);
        target.RequestCount.Should().Be(0);
    }

    [Fact]
    public async Task A_redirect_answer_ends_the_sync_as_a_failure_and_the_client_does_not_chase_it()
    {
        var logs = new CapturingLoggerProvider();
        var handler = new ScriptedBggHandler(_ => new ScriptedResponse(
            HttpStatusCode.MovedPermanently,
            "text/html",
            string.Empty,
            Headers: new Dictionary<string, string> { ["Location"] = "https://www.boardgamegeek.com/xmlapi2/collection" }));
        await using var factory = CreateFactory(handler, logs);
        using var client = factory.CreatePublicClient();

        await PostSync(client);
        await WaitForLine(logs, "BGG sync failed");

        handler.Requests.Should().ContainSingle();
        handler.Requests.Single().Uri.Host.Should().Be(BggOptions.ApiHost);
    }

    [Fact]
    public async Task A_press_with_a_username_in_the_query_and_the_body_still_asks_bgg_only_for_the_configured_one()
    {
        var logs = new CapturingLoggerProvider();
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5));
        await using var factory = CreateFactory(handler, logs);
        using var client = factory.CreatePublicClient();
        var initial = await Get(client, LayoutPath);
        using var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "someone-else",
            ["token"] = "visitor-token",
            ["Bgg:Username"] = "someone-else",
        });

        using var press = await client.PostAsync($"{SyncPath}?username=other&Bgg:Username=other", body, TestContext.Current.CancellationToken);
        await WaitForLayoutChange(client, initial.ETag);

        press.StatusCode.Should().Be(HttpStatusCode.Accepted);
        handler.Requests.Should().HaveCount(2);
        handler.Requests.Should().OnlyContain(request => HttpUtility.ParseQueryString(request.Uri.Query)["username"] == Username);
        handler.Requests.Select(request => request.Uri.ToString()).Should().OnlyContain(
            address => !address.Contains("other", StringComparison.Ordinal) && !address.Contains("visitor", StringComparison.Ordinal));
        handler.Requests.Should().OnlyContain(request => request.AuthorizationParameter == Token);
    }

    [Fact]
    public async Task Visitors_reading_the_page_and_the_layout_cause_no_bgg_request()
    {
        var logs = new CapturingLoggerProvider();
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5));
        await using var factory = CreateFactory(handler, logs);
        using var client = factory.CreatePublicClient();
        var before = handler.Requests.Count;

        for (var visit = 0; visit < 10; visit++)
        {
            (await Get(client, "/")).Status.Should().Be(HttpStatusCode.OK);
            (await Get(client, LayoutPath)).Status.Should().Be(HttpStatusCode.OK);
        }

        handler.Requests.Count.Should().Be(before);
    }

    [Fact]
    public async Task Without_a_token_start_up_warns_once_a_press_asks_bgg_nothing_and_the_site_stays_healthy()
    {
        var logs = new CapturingLoggerProvider();
        var handler = ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(5));
        await using var factory = CreateFactory(handler, logs, token: null);
        using var client = factory.CreatePublicClient();
        using var ops = factory.CreateOpsClient();
        var warningsAtStartUp = CountWarnings(logs);

        using var press = await client.PostAsync(SyncPath, content: null, TestContext.Current.CancellationToken);
        await WaitForLine(logs, "BGG sync failed: NotConfigured");
        var page = await Get(client, "/");
        using var health = await ops.GetAsync("/health", TestContext.Current.CancellationToken);

        warningsAtStartUp.Should().BeGreaterThan(0);
        CountWarnings(logs).Should().Be(warningsAtStartUp);
        press.StatusCode.Should().Be(HttpStatusCode.Accepted);
        handler.Requests.Should().BeEmpty();
        page.Body.Should().Contain(BeingFilled);
        health.StatusCode.Should().Be(HttpStatusCode.OK);
        (await health.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("Healthy");
        logs.Lines.Should().OnlyContain(line => !line.Contains(Username, StringComparison.Ordinal));
    }

    private static int CountWarnings(CapturingLoggerProvider logs) => logs.Lines.Count(line => line == NotConfiguredWarning);

    private static CabinetWebApplicationFactory CreateFactory(ScriptedBggHandler handler, CapturingLoggerProvider logs, string? token = Token) =>
        new(
            new Dictionary<string, string?>
            {
                ["Bgg:Username"] = Username,
                ["Bgg:Token"] = token,
            },
            services =>
            {
                services.AddSingleton<ILoggerProvider>(logs);
                services.AddSingleton<IRequestPacer>(new NoWaitPacer());
                services.AddHttpClient<ICollectionSource, BggClient>().ConfigurePrimaryHttpMessageHandler(() => handler);
            });

    private static async Task<string> PostSync(HttpClient client)
    {
        using var response = await client.PostAsync(SyncPath, content: null, TestContext.Current.CancellationToken);

        return await Describe(response);
    }

    private static async Task<(HttpStatusCode Status, string? ETag, string Body, string Everything)> Get(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        return (response.StatusCode, response.Headers.ETag?.Tag, body, await Describe(response));
    }

    private static async Task<string> Describe(HttpResponseMessage response) =>
        response.ToString() + await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static async Task WaitForLayoutChange(HttpClient client, string? initialETag)
    {
        await WaitUntil(async () => (await Get(client, LayoutPath)).ETag != initialETag);
    }

    private static async Task WaitForLine(CapturingLoggerProvider logs, string fragment)
    {
        await WaitUntil(() => Task.FromResult(logs.Lines.Any(line => line.Contains(fragment, StringComparison.Ordinal))));
    }

    private static async Task WaitUntil(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The condition did not hold within fifteen seconds.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }
    }
}
