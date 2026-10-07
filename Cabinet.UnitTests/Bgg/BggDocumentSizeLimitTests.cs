using System.Text;
using System.Xml;
using Cabinet.Domain.Layout;
using Cabinet.Repository.Bgg;
using FluentAssertions;

namespace Cabinet.UnitTests.Bgg;

/// <summary>Proves the reader refuses a document with more than about twenty million characters and accepts one just under.</summary>
[Trait("Category", "Bgg")]
public class BggDocumentSizeLimitTests
{
    private const int JustUnderTheLimit = 19_900_000;
    private const int JustOverTheLimit = 20_100_000;

    [Fact]
    public void A_document_just_over_the_character_limit_is_refused_as_malformed()
    {
        var parse = () => Parse(JustOverTheLimit);

        parse.Should().Throw<XmlException>();
    }

    [Fact]
    public void A_document_just_under_the_character_limit_is_read()
    {
        var parsed = Parse(JustUnderTheLimit);

        parsed.TotalItems.Should().Be(0);
        parsed.Items.Should().BeEmpty();
    }

    private static ParsedCollection Parse(int paddingCharacters)
    {
        var builder = new StringBuilder("<items totalitems=\"0\" termsofuse=\"https://example.com/terms\"><!--", paddingCharacters + 100);
        builder.Append('x', paddingCharacters);
        builder.Append("--></items>");
        using var body = new MemoryStream(Encoding.UTF8.GetBytes(builder.ToString()));

        return BggCollectionParser.Parse(body, ItemKind.Base, includePrivateInfo: false);
    }
}
