using Cabinet.Domain.Collection;
using Cabinet.Repository.Images;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves the store that holds a previewed picture: tokens are random 32-character lowercase hexadecimal texts, an item is found
/// until its lifetime has passed on the injected clock, a ninth item drops the oldest, and a text that has any other shape is
/// never looked up.
/// </summary>
[Trait("Category", "Owner")]
public sealed class StagedItemsTests
{
    private static readonly DateTimeOffset Moment = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_staged_item_is_found_by_its_token_and_the_token_is_32_lowercase_hex_characters()
    {
        var (items, _) = Create();

        var token = items.Stage("first");

        StagedItems<string>.IsToken(token).Should().BeTrue();
        token.Should().HaveLength(32);
        items.TryGet(token, out var found).Should().BeTrue();
        found.Should().Be("first");
    }

    [Fact]
    public void Two_staged_items_get_different_tokens()
    {
        var (items, _) = Create();

        items.Stage("one").Should().NotBe(items.Stage("two"));
    }

    [Fact]
    public void An_item_is_found_until_fifteen_minutes_have_passed_and_not_after()
    {
        var (items, clock) = Create();
        var token = items.Stage("first");

        clock.Advance(StagedItems<string>.Lifetime - TimeSpan.FromTicks(1));
        var before = items.TryGet(token, out _);
        clock.Advance(TimeSpan.FromTicks(1));
        var after = items.TryGet(token, out _);

        StagedItems<string>.Lifetime.Should().Be(TimeSpan.FromMinutes(15));
        before.Should().BeTrue();
        after.Should().BeFalse();
    }

    [Fact]
    public void A_ninth_item_drops_the_oldest_and_keeps_the_other_eight()
    {
        var (items, clock) = Create();
        var tokens = Enumerable.Range(0, StagedItems<string>.Capacity + 1)
            .Select(index =>
            {
                clock.Advance(TimeSpan.FromSeconds(1));

                return items.Stage($"item {index}");
            })
            .ToList();

        items.TryGet(tokens[0], out _).Should().BeFalse();
        tokens.Skip(1).Select(token => items.TryGet(token, out _)).Should().AllSatisfy(found => found.Should().BeTrue());
        StagedItems<string>.Capacity.Should().Be(8);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("0123456789abcdef0123456789abcdef0")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    [InlineData("../../0123456789abcdef01234567")]
    public void A_text_that_is_not_the_shape_of_a_token_is_never_found_or_removed(string? text)
    {
        var (items, _) = Create();
        items.Stage("first");

        StagedItems<string>.IsToken(text).Should().BeFalse();
        items.TryGet(text, out _).Should().BeFalse();
        items.Remove(text).Should().BeFalse();
    }

    [Fact]
    public void Removing_an_item_forgets_it_and_removing_it_again_does_nothing()
    {
        var (items, _) = Create();
        var token = items.Stage("first");

        var first = items.Remove(token);
        var second = items.Remove(token);

        first.Should().BeTrue();
        second.Should().BeFalse();
        items.TryGet(token, out _).Should().BeFalse();
    }

    [Fact]
    public void Links_staged_for_two_copies_of_one_game_are_kept_apart_and_each_names_its_copy()
    {
        var clock = new FakeTimeProvider(Moment);
        var items = new StagedItems<StagedLink>(clock);
        var first = items.Stage(Link(7001));
        var second = items.Stage(Link(7002));

        items.TryGet(first, out var forFirst).Should().BeTrue();
        items.TryGet(second, out var forSecond).Should().BeTrue();
        items.Remove(first).Should().BeTrue();

        first.Should().NotBe(second);
        forFirst.GameId.Should().Be(forSecond.GameId);
        forFirst.CollectionId.Should().Be(7001);
        forSecond.CollectionId.Should().Be(7002);
        items.TryGet(second, out var stillThere).Should().BeTrue();
        stillThere.CollectionId.Should().Be(7002);
    }

    private static StagedLink Link(long collectionId)
    {
        var record = new ImageRecord("https://cf.example.org/link.jpg", ImageStatus.Ok, Moment, [new ArtFile(240, 320, "0123456789abcdef-240.webp")], null);
        var art = new EncodedArt(240, 320, "0123456789abcdef-240.webp", [1, 2, 3]);

        return new StagedLink(100, collectionId, new ProcessedLink(record.SourceUrl, [art], record), art);
    }

    private static (StagedItems<string> Items, FakeTimeProvider Clock) Create()
    {
        var clock = new FakeTimeProvider(Moment);

        return (new StagedItems<string>(clock), clock);
    }
}
