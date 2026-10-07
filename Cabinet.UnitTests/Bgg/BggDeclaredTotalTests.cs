using System.Collections.Concurrent;
using Cabinet.Domain.Collection;
using Cabinet.FakeBgg;
using Cabinet.FakeBgg.Testing;
using Cabinet.Repository.Bgg;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Cabinet.UnitTests.Bgg;

/// <summary>
/// Proves a collection answer is accepted only when it declares a total that matches what was read, and that an entry
/// without a usable identifier is tolerated, counted and reported by number only.
/// </summary>
[Trait("Category", "Bgg")]
public class BggDeclaredTotalTests
{
    private const string EmptyExpansions = "<?xml version=\"1.0\"?><items totalitems=\"0\"></items>";

    [Fact]
    public async Task An_answer_without_a_declared_total_is_a_bad_answer()
    {
        var result = await Fetch(Items(null, Entry("10", "100", "Alpha"), Entry("11", "101", "Beta")));

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.BadAnswer);
    }

    [Fact]
    public async Task An_answer_with_a_total_that_is_not_a_number_is_a_bad_answer()
    {
        var result = await Fetch(Items("many", Entry("10", "100", "Alpha")));

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.BadAnswer);
    }

    [Fact]
    public async Task An_empty_collection_that_declares_zero_is_accepted()
    {
        var result = await Fetch(Items("0"));

        result.Should().BeOfType<CollectionFetchResult.Fetched>().Which.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task An_entry_without_a_usable_identifier_is_left_out_while_the_rest_of_a_complete_collection_is_kept()
    {
        var result = await Fetch(Items("3", Entry("10", "100", "Alpha"), Entry("not-a-number", "101", "Beta"), Entry("12", "102", "Gamma")));

        result.Should().BeOfType<CollectionFetchResult.Fetched>().Which.Items.Select(item => item.CollectionId).Should().Equal(10, 12);
    }

    [Fact]
    public async Task A_total_that_even_the_left_out_entries_do_not_explain_is_a_bad_answer()
    {
        var result = await Fetch(Items("5", Entry("10", "100", "Alpha"), Entry("not-a-number", "101", "Beta")));

        result.Should().BeOfType<CollectionFetchResult.Failed>().Which.Failure.Should().Be(SyncFailure.BadAnswer);
    }

    [Fact]
    public async Task The_number_of_left_out_entries_is_logged_without_a_title_or_an_identifier()
    {
        var logger = new CapturingLogger();

        await Fetch(
            Items("3", Entry("10", "100", "Distinctive Title One"), Entry("bad-collection-id", "101", "Distinctive Title Two"), Entry("12", "102", "Gamma")),
            logger);

        var line = logger.Lines.Should().ContainSingle().Which;
        line.Level.Should().Be(LogLevel.Warning);
        line.Message.Should().Contain("1 ").And.NotContain("Distinctive").And.NotContain("bad-collection-id").And.NotContain("101");
    }

    [Fact]
    public async Task A_complete_answer_logs_nothing()
    {
        var logger = new CapturingLogger();

        await Fetch(Items("2", Entry("10", "100", "Alpha"), Entry("11", "101", "Beta")), logger);

        logger.Lines.Should().BeEmpty();
    }

    private static string Items(string? total, params string[] entries) =>
        $"<?xml version=\"1.0\"?><items{(total is null ? string.Empty : $" totalitems=\"{total}\"")}>{string.Concat(entries)}</items>";

    private static string Entry(string collectionId, string gameId, string name) =>
        $"<item objecttype=\"thing\" objectid=\"{gameId}\" subtype=\"boardgame\" collid=\"{collectionId}\"><name sortindex=\"1\">{name}</name><status own=\"1\"/></item>";

    private static async Task<CollectionFetchResult> Fetch(string baseAnswer, ILogger<BggClient>? logger = null)
    {
        var handler = new ScriptedBggHandler(request =>
            ScriptedResponse.Xml(BggTestKit.IsBaseCall(request) ? baseAnswer : EmptyExpansions));
        var http = new HttpClient(handler) { BaseAddress = BggOptions.DefaultBaseUri };
        var client = new BggClient(http, BggTestKit.Options(), new ImmediatePacer(), TimeProvider.System, logger: logger);

        return await client.FetchOwnedAsync(TestContext.Current.CancellationToken);
    }

    private sealed class CapturingLogger : ILogger<BggClient>
    {
        private readonly ConcurrentQueue<(LogLevel Level, string Message)> _lines = new();

        public IReadOnlyList<(LogLevel Level, string Message)> Lines => [.. _lines];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _lines.Enqueue((logLevel, formatter(state, exception)));
    }
}
