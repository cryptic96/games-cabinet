using System.Text.RegularExpressions;
using Cabinet.Domain.Cards;
using Cabinet.Domain.Collection;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Cards;

/// <summary>Proves the invented details of the samples are the same on every call and exercise every row of the card.</summary>
[Trait("Category", "Cards")]
public sealed class SampleCardDetailsTests
{
    private const int LongMechanicsList = 10;

    [Fact]
    public void A_sample_gets_the_same_details_on_every_call()
    {
        var items = ItemsOf("65");

        var first = SampleCardDetails.SnapshotFor("65", items);
        var second = SampleCardDetails.SnapshotFor("65", items);

        first.Items.Should().Equal(second.Items);
        first.Games!.Keys.Should().Equal(second.Games!.Keys);
        first.Games.Values.Zip(second.Games.Values).Should().OnlyContain(pair => pair.First.SameAs(pair.Second));
    }

    [Fact]
    public void The_review_sample_has_locations_on_some_games_and_none_on_others()
    {
        var snapshot = SampleCardDetails.SnapshotFor("65", ItemsOf("65"));

        snapshot.Items.Should().Contain(item => item.Location != null);
        snapshot.Items.Should().Contain(item => item.Location == null);
    }

    [Fact]
    public void The_review_sample_has_a_game_with_no_details_and_a_game_with_a_long_mechanics_list()
    {
        var items = ItemsOf("65");
        var snapshot = SampleCardDetails.SnapshotFor("65", items);

        items.Should().Contain(item => !snapshot.Games!.ContainsKey(item.BggId));
        snapshot.Games!.Values.Should().Contain(details => details.Mechanics.Count >= LongMechanicsList);
        snapshot.Games.Values.Select(details => details.Designers.Count).Should().Contain(1).And.Contain(count => count > 1);
    }

    [Fact]
    public void Every_sample_value_is_a_plausible_stored_value()
    {
        var snapshot = SampleCardDetails.SnapshotFor("65", ItemsOf("65"));

        snapshot.Games!.Values.Should().OnlyContain(details =>
            details.MinPlayers >= 1 && details.MaxPlayers >= details.MinPlayers
            && details.Weight >= 1.0 && details.Weight <= 4.8
            && details.Average >= 5.5 && details.Average <= 8.9
            && details.PlayingTime > 0 && details.MinAge > 0);
    }

    [Fact]
    public void The_review_sample_has_an_expansion_with_two_owned_bases_and_one_whose_base_is_not_owned()
    {
        var items = ItemsOf("65");
        var snapshot = SampleCardDetails.SnapshotFor("65", items);

        var cards = CardRecords.Build(items, snapshot, SectionDesigns.Desktop);

        cards.Should().Contain(card => card.IsExpansion && card.Bases.Count == 2 && card.Bases.All(link => link.EntryId != null));
        cards.Should().Contain(card => card.IsExpansion && card.Bases.Count > 0 && card.Bases.All(link => link.EntryId == null && link.Chip == null));
        cards.Should().Contain(card => !card.IsExpansion && card.Expansions.Count >= 2);
    }

    [Fact]
    public void The_edge_sample_has_designers_and_locations_in_several_scripts_and_an_emoji()
    {
        var snapshot = SampleCardDetails.SnapshotFor("edge", ItemsOf("edge"));
        var text = string.Concat(snapshot.Games!.Values.SelectMany(details => details.Designers))
            + string.Concat(snapshot.Items.Select(item => item.Location));

        Regex.IsMatch(text, @"\p{IsCyrillic}").Should().BeTrue();
        Regex.IsMatch(text, @"[\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]").Should().BeTrue();
        Regex.IsMatch(text, @"\p{IsThai}").Should().BeTrue();
        Regex.IsMatch(text, @"\p{IsArabic}").Should().BeTrue();
        text.Any(char.IsSurrogate).Should().BeTrue();
    }

    [Fact]
    public void Every_sample_item_has_a_stored_entry_with_the_same_identity()
    {
        var items = ItemsOf("12");

        var snapshot = SampleCardDetails.SnapshotFor("12", items);

        snapshot.Items.Select(item => (item.CollectionId, item.GameId, item.Title, item.Kind))
            .Should().Equal(items.Select(item => (item.CollectionId, item.BggId, item.Title, item.Kind)));
    }

    private static IReadOnlyList<CabinetItem> ItemsOf(string sample)
    {
        SyntheticCollections.TryGetSample(sample, out var items).Should().BeTrue();

        return items;
    }
}
