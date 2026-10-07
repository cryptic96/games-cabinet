using System.Security.Cryptography;
using FluentAssertions;

namespace Cabinet.UnitTests.Configuration;

/// <summary>Pins the vendored third-party browser script to the exact bytes that were verified when it was added.</summary>
[Trait("Category", "Configuration")]
public class VendoredAssetTests
{
    private const string SignalRClientSha256 = "97e9b97e642a72e5a470917147a2bf79f86cad829a5c6786adb10614d248bb95";
    private const string SignalRClientVersion = "10.0.11";

    [Fact]
    public void The_vendored_live_client_is_the_verified_file_byte_for_byte()
    {
        var bytes = File.ReadAllBytes(Path.Combine(VendoredDirectory(), "signalr.min.js"));

        Convert.ToHexStringLower(SHA256.HashData(bytes)).Should().Be(SignalRClientSha256);
    }

    [Fact]
    public void The_notice_names_the_same_hash_version_and_licence_as_the_pinned_file()
    {
        var notice = File.ReadAllText(Path.Combine(VendoredDirectory(), "NOTICE.md"));

        notice.Should().Contain(SignalRClientSha256);
        notice.Should().Contain(SignalRClientVersion);
        notice.Should().Contain("The MIT License (MIT)");
        notice.Should().Contain("sha512-");
    }

    private static string VendoredDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Cabinet.Service", "wwwroot", "lib", "signalr");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The vendored script directory was not found above the test output directory.");
    }
}
