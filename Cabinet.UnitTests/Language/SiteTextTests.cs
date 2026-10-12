using System.Reflection;
using System.Text.RegularExpressions;
using Cabinet.Service.Language;
using FluentAssertions;

namespace Cabinet.UnitTests.Language;

/// <summary>Verifies the English and Dutch label tables stay in step and follow the voice rules.</summary>
public partial class SiteTextTests
{
    private static readonly string[] SameInBothLanguages = [nameof(SiteText.EnglishName), nameof(SiteText.DutchName), nameof(SiteText.LogoAlt)];

    [Fact]
    public void Every_label_in_both_tables_has_text()
    {
        foreach (var (_, english, dutch) in Labels())
        {
            english.Should().NotBeNullOrWhiteSpace();
            dutch.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void The_format_strings_hold_the_same_placeholders_in_both_languages()
    {
        Placeholders(SiteText.English.VersionFormat).Should().Equal(Placeholders(SiteText.Dutch.VersionFormat));
        Placeholders(SiteText.English.LastSyncedFormat).Should().Equal(Placeholders(SiteText.Dutch.LastSyncedFormat));
        Placeholders(SiteText.English.VersionFormat).Should().Equal("{0}", "{1}");
        Placeholders(SiteText.English.LastSyncedFormat).Should().Equal("{0}");
    }

    [Fact]
    public void The_dutch_labels_use_je_and_never_the_formal_u()
    {
        foreach (var (name, _, dutch) in Labels())
        {
            FormalYou().IsMatch(dutch).Should().BeFalse($"{name} must use je");
        }
    }

    [Fact]
    public void No_label_uses_the_ellipsis_character()
    {
        foreach (var (name, english, dutch) in Labels())
        {
            english.Should().NotContain("…", name);
            dutch.Should().NotContain("…", name);
        }
    }

    [Fact]
    public void English_and_dutch_differ_for_every_label_except_the_names_that_are_the_same_by_design()
    {
        foreach (var (name, english, dutch) in Labels())
        {
            if (SameInBothLanguages.Contains(name))
            {
                english.Should().Be(dutch, name);
            }
            else
            {
                english.Should().NotBe(dutch, name);
            }
        }
    }

    [Fact]
    public void The_footer_and_exact_time_lines_are_filled_in()
    {
        SiteText.English.Version("1.2.3", "abc1234").Should().Be("Version 1.2.3 (abc1234)");
        SiteText.Dutch.Version("1.2.3", "abc1234").Should().Be("Versie 1.2.3 (abc1234)");
        SiteText.English.LastSynced("9 October 2026 at 12:32 UTC").Should().Be("Last synced 9 October 2026 at 12:32 UTC");
        SiteText.Dutch.LastSynced("9 oktober 2026 om 12:32 UTC").Should().Be("Laatst gesynchroniseerd op 9 oktober 2026 om 12:32 UTC");
    }

    [Fact]
    public void The_dutch_culture_writes_dutch_month_names()
    {
        SiteLanguage.Dutch.Culture.DateTimeFormat.GetMonthName(10).Should().Be("oktober");
        SiteLanguage.English.Culture.DateTimeFormat.GetMonthName(10).Should().Be("October");
        SiteLanguage.EnsureAvailable();
    }

    [Fact]
    public void The_brand_credit_alt_text_is_never_translated()
    {
        SiteText.English.LogoAlt.Should().Be("Powered by BGG");
        SiteText.Dutch.LogoAlt.Should().Be("Powered by BGG");
    }

    private static IEnumerable<(string Name, string English, string Dutch)> Labels() =>
        typeof(SiteText)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => (
                property.Name,
                (string)property.GetValue(SiteText.English)!,
                (string)property.GetValue(SiteText.Dutch)!));

    private static IEnumerable<string> Placeholders(string format) =>
        Placeholder().Matches(format).Select(match => match.Value).OrderBy(value => value, StringComparer.Ordinal);

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])[uU](?![\p{L}\p{N}])")]
    private static partial Regex FormalYou();
}
