using System.Net;
using Cabinet.Domain.Collection;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Repository.Bgg;
using Cabinet.Repository.Images;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>Verifies a test host can never reach BGG or its image host through a client a test forgot to script.</summary>
public class RealNetworkGuardTests
{
    /// <summary>The names the program registers its outgoing clients under, one per source it talks to.</summary>
    public static TheoryData<string> ProgramClients => new()
    {
        nameof(ICollectionSource),
        nameof(IEnrichmentSource),
        nameof(IArtSource),
    };

    [Theory]
    [MemberData(nameof(ProgramClients))]
    public async Task An_unscripted_program_client_is_refused_before_connecting_and_fails_the_host(string clientName)
    {
        var factory = CreateFactoryWithCredentials();
        var bggAddress = new Uri(BggOptions.DefaultBaseUri, "xmlapi2/collection");

        using (var client = CreateClient(factory, clientName))
        {
            await client.Invoking(c => c.GetAsync(bggAddress, TestContext.Current.CancellationToken))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*may not reach the real network*");
        }

        factory.NetworkGuard.Refused.Should().ContainSingle().Which.Should().Contain(bggAddress.Host);
        await factory.Invoking(f => f.DisposeAsync().AsTask())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"*tried to reach the real network*{bggAddress.Host}*");
    }

    [Fact]
    public async Task A_loopback_request_on_a_real_connection_client_goes_through()
    {
        await using var factory = CreateFactoryWithCredentials();
        using var client = CreateClient(factory, nameof(ICollectionSource));

        using var response = await client.GetAsync(
            new Uri($"http://127.0.0.1:{factory.PublicPort}/"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.NetworkGuard.Refused.Should().BeEmpty();
    }

    [Fact]
    public async Task A_scripted_client_answers_from_its_script_and_is_not_refused()
    {
        await using var factory = SyncHarness.CreateFactory(SyncHarness.Refusing(), SyncHarness.NewClock());
        using var client = CreateClient(factory, nameof(ICollectionSource));

        using var response = await client.GetAsync(
            new Uri(BggOptions.DefaultBaseUri, "xmlapi2/collection"),
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        factory.NetworkGuard.Refused.Should().BeEmpty();
    }

    private static CabinetWebApplicationFactory CreateFactoryWithCredentials() =>
        new(new Dictionary<string, string?>(), services => services.AddSingleton(SyncHarness.Options()));

    private static HttpClient CreateClient(CabinetWebApplicationFactory factory, string clientName) =>
        factory.ServingServices.GetRequiredService<IHttpClientFactory>().CreateClient(clientName);
}
