using System.Security.Cryptography;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>Pins the third-party attribution logo to the exact bytes that were taken from the official logo pack, and checks its provenance notice.</summary>
[Trait("Category", "Configuration")]
public class BrandAssetTests
{
    private const string LogoSha256 = "b577fd17bd5f84ea575fb10ff1f7fa3895b54727728bfd4a4ed11300a722bc43";
    private const string DarkLogoSha256 = "527357eff019297fa1a1e81002723c1eb5bbbf8ae80e5a31fdae17bf8f82d8cc";

    [Fact]
    public void The_attribution_logo_is_the_official_file_byte_for_byte()
    {
        var bytes = File.ReadAllBytes(Path.Combine(ImageDirectory(), "powered-by-bgg.svg"));

        Convert.ToHexStringLower(SHA256.HashData(bytes)).Should().Be(LogoSha256);
    }

    [Fact]
    public void The_dark_attribution_logo_is_the_official_file_byte_for_byte()
    {
        var bytes = File.ReadAllBytes(Path.Combine(ImageDirectory(), "powered-by-bgg-dark.svg"));

        Convert.ToHexStringLower(SHA256.HashData(bytes)).Should().Be(DarkLogoSha256);
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

    [Fact]
    public void The_notice_lists_the_dark_logo_with_its_hash_as_the_standard_variant_and_keeps_both_files_unchanged()
    {
        var notice = File.ReadAllText(Path.Combine(ImageDirectory(), "NOTICE.md"));

        notice.Should().Contain("powered-by-bgg-dark.svg");
        notice.Should().Contain(DarkLogoSha256);
        notice.Should().Contain("standard");
        notice.Should().Contain("Both files are copied unchanged");
        notice.Should().NotContain("Downloads");
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
