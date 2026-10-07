using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using Cabinet.Service.Collection;
using Cabinet.Service.Layout;
using Cabinet.Service.Pages;
using Cabinet.Service.Prototype;
using Cabinet.Service.Sync;
using Cabinet.UnitTests.Sync;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.UnitTests.Prototype;

/// <summary>
/// Verifies an invented collection is generated at most once per process: validating a layout request, serving a cached
/// layout, answering a revalidation and showing the page's item count never generate a sample again.
/// </summary>
public class SampleGenerationTests
{
    [Fact]
    public void The_catalog_generates_a_sample_once_however_often_its_items_are_asked_for()
    {
        var generator = new CountingGenerator();
        var catalog = new SampleCatalog(true, generator.Generate);

        var first = catalog.ItemCount("65");
        var second = catalog.ItemCount("65");
        var items = catalog.ItemsOf("65");

        first.Should().Be(65);
        second.Should().Be(65);
        items.Should().HaveCount(65);
        generator.CallsFor("65").Should().Be(1);
    }

    [Fact]
    public void An_unknown_name_is_not_honoured_and_never_generated()
    {
        var generator = new CountingGenerator();
        var catalog = new SampleCatalog(true, generator.Generate);

        catalog.TryResolve("64", out _).Should().BeFalse();
        catalog.Invoking(known => known.ItemsOf("64")).Should().Throw<ArgumentException>();

        generator.Names.Should().BeEmpty();
    }

    [Fact]
    public void Repeated_page_views_generate_the_sample_once()
    {
        var generator = new CountingGenerator();
        var catalog = new SampleCatalog(true, generator.Generate);

        var store = new CollectionStore();
        var options = new SyncOptions(false, TimeSpan.FromHours(1), TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(120), TimeSpan.FromHours(3));
        var coordinator = new SyncCoordinator(new InMemorySyncStateStore(), options, TimeProvider.System);
        var status = new SyncStatusService(coordinator, store, options, TimeProvider.System);

        for (var view = 0; view < 3; view++)
        {
            var page = new IndexModel(catalog, store, status);
            page.OnGet("400");
            page.ItemCount.Should().Be(400);
        }

        generator.CallsFor("400").Should().Be(1);
    }

    [Theory]
    [InlineData("65", "tablet")]
    [InlineData(null, "tablet")]
    [InlineData("65", null)]
    public void A_request_with_a_bad_profile_is_refused_without_generating_anything(string? sample, string? profile)
    {
        var generator = new CountingGenerator();
        var services = Services(new SampleCatalog(true, generator.Generate));

        var result = LayoutEndpoint.Handle(sample, profile, Request(services));

        result.Should().BeOfType<NotFound>();
        generator.Names.Should().BeEmpty();
    }

    [Theory]
    [InlineData("64")]
    [InlineData(null)]
    [InlineData("")]
    public void A_request_without_an_honoured_sample_gets_the_synced_layout_without_generating_anything(string? sample)
    {
        var generator = new CountingGenerator();
        var services = Services(new SampleCatalog(true, generator.Generate));

        var result = LayoutEndpoint.Handle(sample, SectionDesigns.DesktopName, Request(services));

        result.Should().BeOfType<ContentHttpResult>();
        generator.Names.Should().BeEmpty();
    }

    [Fact]
    public void A_disabled_catalog_ignores_a_known_sample_and_generates_nothing()
    {
        var generator = new CountingGenerator();
        var services = Services(new SampleCatalog(false, generator.Generate));
        var request = Request(services);

        var result = LayoutEndpoint.Handle("65", SectionDesigns.DesktopName, request);

        result.Should().BeOfType<ContentHttpResult>();
        request.Response.Headers.ETag.ToString().Should().Contain("-collection-empty-desktop");
        generator.Names.Should().BeEmpty();
    }

    [Fact]
    public void Cached_layouts_and_revalidations_never_generate_the_sample_again()
    {
        var generator = new CountingGenerator();
        var services = Services(new SampleCatalog(true, generator.Generate));

        var firstRequest = Request(services);
        var first = LayoutEndpoint.Handle("65", SectionDesigns.DesktopName, firstRequest);
        var eTag = firstRequest.Response.Headers.ETag.ToString();
        var again = LayoutEndpoint.Handle("65", SectionDesigns.DesktopName, Request(services));
        var revalidation = Request(services);
        revalidation.Request.Headers.IfNoneMatch = eTag;
        var notModified = LayoutEndpoint.Handle("65", SectionDesigns.DesktopName, revalidation);
        var phone = LayoutEndpoint.Handle("65", SectionDesigns.PhoneName, Request(services));

        first.Should().BeOfType<ContentHttpResult>();
        again.Should().BeOfType<ContentHttpResult>();
        notModified.Should().BeOfType<StatusCodeHttpResult>().Which.StatusCode.Should().Be(StatusCodes.Status304NotModified);
        phone.Should().BeOfType<ContentHttpResult>();
        generator.CallsFor("65").Should().Be(1, "the second profile reuses the items the first one generated");
    }

    private static ServiceProvider Services(SampleCatalog catalog) =>
        new ServiceCollection()
            .AddSingleton(LayoutOptions.Default)
            .AddSingleton(catalog)
            .AddSingleton<LayoutCache>()
            .AddSingleton<CollectionStore>()
            .BuildServiceProvider();

    private static DefaultHttpContext Request(IServiceProvider services) => new() { RequestServices = services };

    /// <summary>Generates the invented collections like the catalog does, and records every name it was asked for.</summary>
    private sealed class CountingGenerator
    {
        private readonly List<string> _names = [];

        /// <summary>Every name generated so far, in order.</summary>
        public IReadOnlyList<string> Names => _names;

        /// <summary>How many times the named sample was generated.</summary>
        public int CallsFor(string name) => _names.Count(called => called == name);

        /// <summary>Generates the named invented collection.</summary>
        public IReadOnlyList<CabinetItem> Generate(string name)
        {
            _names.Add(name);

            return SyntheticCollections.TryGetSample(name, out var items)
                ? items
                : throw new ArgumentException("The sample is not on the allowlist.", nameof(name));
        }
    }
}
