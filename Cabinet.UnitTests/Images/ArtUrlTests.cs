using Cabinet.Repository.Images;
using FluentAssertions;

namespace Cabinet.UnitTests.Images;

/// <summary>Proves picture addresses are read into one canonical form and that only the named host, over https, is allowed.</summary>
[Trait("Category", "Images")]
public sealed class ArtUrlTests
{
    private static readonly ArtSourcePolicy Policy = new(new HashSet<string>(StringComparer.Ordinal) { "example.org" });

    [Fact]
    public void An_address_that_starts_with_two_slashes_is_read_as_https()
    {
        ArtUrl.Canonical("/" + "/example.org/a.png").Should().Be("https://example.org/a.png");
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
    {
        ArtUrl.Canonical("  https://example.org/a.png\n").Should().Be("https://example.org/a.png");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("relative/path.png")]
    [InlineData("/rooted/path.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("ftp://example.org/a.png")]
    [InlineData("file:///etc/passwd")]
    public void Text_that_is_not_an_http_or_https_address_gives_nothing(string? text)
    {
        ArtUrl.Canonical(text).Should().BeNull();
    }

    [Fact]
    public void An_address_over_the_length_cap_gives_nothing_and_one_at_the_cap_is_kept()
    {
        var prefix = "https://example.org/";
        var atCap = prefix + new string('a', ArtUrl.MaxLength - prefix.Length);

        ArtUrl.Canonical(atCap).Should().Be(atCap);
        ArtUrl.Canonical(atCap + "a").Should().BeNull();
    }

    [Fact]
    public void An_http_address_is_kept_so_the_policy_can_refuse_it()
    {
        ArtUrl.Canonical("http://example.org/a.png").Should().Be("http://example.org/a.png");
    }

    [Fact]
    public void Https_on_the_named_host_is_allowed_ignoring_case()
    {
        Policy.Allows(new Uri("https://example.org/a.png")).Should().BeTrue();
        Policy.Allows(new Uri("https://EXAMPLE.Org/a.png")).Should().BeTrue();
    }

    [Theory]
    [InlineData("http://example.org/a.png")]
    [InlineData("https://example.org.evil.example/a.png")]
    [InlineData("https://evil.example/example.org/a.png")]
    [InlineData("https://evil.example/a.png?host=example.org")]
    [InlineData("https://203.0.113.9/a.png")]
    [InlineData("https://[2001:db8::1]/a.png")]
    [InlineData("https://user:secret@example.org/a.png")]
    [InlineData("https://user@example.org/a.png")]
    [InlineData("https://example.org:8443/a.png")]
    [InlineData("https://other.example.org/a.png")]
    public void Everything_else_is_refused(string address)
    {
        Policy.Allows(new Uri(address)).Should().BeFalse();
    }

    [Fact]
    public void The_development_origin_is_allowed_only_on_an_exact_scheme_host_and_port()
    {
        var policy = new ArtSourcePolicy(new HashSet<string>(StringComparer.Ordinal), new Uri("http://127.0.0.1:5090"));

        policy.Allows(new Uri("http://127.0.0.1:5090/images/a.png")).Should().BeTrue();
        policy.Allows(new Uri("https://127.0.0.1:5090/images/a.png")).Should().BeFalse();
        policy.Allows(new Uri("http://127.0.0.1:5091/images/a.png")).Should().BeFalse();
        policy.Allows(new Uri("http://127.0.0.2:5090/images/a.png")).Should().BeFalse();
        policy.Allows(new Uri("http://localhost:5090/images/a.png")).Should().BeFalse();
    }
}
