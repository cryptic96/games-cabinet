using System.Security.Cryptography;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>Pins the third-party attribution logo to the exact bytes that were taken from the official logo pack, and checks its provenance notice.</summary>
[Trait("Category", "Configuration")]
public class BrandAssetTests
{
    private const string LogoSha256 = "b577fd17bd5f84ea575fb10ff1f7fa3895b54727728bfd4a4ed11300a722bc43";

    [Fact]
    public void The_attribution_logo_is_the_official_file_byte_for_byte()
    {
        var bytes = File.ReadAllBytes(Path.Combine(ImageDirectory(), "powered-by-bgg.svg"));

        Convert.ToHexStringLower(SHA256.HashData(bytes)).Should().Be(LogoSha256);
    }

    [Fact]
    public void The_notice_records_the_same_hash_and_the_origin_and_terms_of_the_logo()
    {
        var notice = File.ReadAllText(Path.Combine(ImageDirectory(), "NOTICE.md"));

        notice.Should().Contain(LogoSha256);
        notice.Should().Contain("official");
        notice.Should().Contain("Powered by BGG");
        notice.Should().Contain("reversed");
        notice.Should().Contain("logo pack");
        notice.Should().Contain("XML API terms of use");
        notice.Should().Contain("unchanged");
    }

    private static string ImageDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Cabinet.Service", "wwwroot", "img");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The image directory was not found above the test output directory.");
    }
}
