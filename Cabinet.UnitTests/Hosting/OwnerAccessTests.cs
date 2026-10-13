using System.Net;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Cabinet.UnitTests.Hosting;

/// <summary>Verifies the owner access settings are checked at start-up and a mistake fails closed instead of opening the owner tools.</summary>
public class OwnerAccessTests
{
    private const string OwnerUrl = "http://127.0.0.1:5082";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromConfiguration_switches_the_owner_tools_off_without_an_owner_url(string? url)
    {
        var options = OwnerAccess.FromConfiguration(Build(url, ranges: [], proxies: []));

        options.Port.Should().BeNull();
        options.Enabled.Should().BeFalse();
        options.Ranges.Should().BeEmpty();
    }

    [Fact]
    public void FromConfiguration_rejects_an_owner_url_without_ranges()
    {
        var act = () => OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: [], proxies: ["127.0.0.1"]));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Owner:AllowedRanges*");
    }

    [Fact]
    public void FromConfiguration_rejects_ranges_without_a_known_proxy()
    {
        var act = () => OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: ["192.0.2.0/24"], proxies: []));

        act.Should().Throw<InvalidOperationException>().WithMessage("*ReverseProxy:KnownProxies*");
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("::ffff:192.0.2.0/120")]
    [InlineData("::ffff:192.0.2.7")]
    [InlineData("192.0.2.0/33")]
    [InlineData("2001:db8::/129")]
    [InlineData("192.0.2.0/-1")]
    [InlineData("192.0.2.0/x")]
    [InlineData("/24")]
    [InlineData("word")]
    [InlineData("192.0.2/24")]
    [InlineData("3221225985")]
    public void FromConfiguration_rejects_a_range_that_could_let_everyone_in_or_cannot_be_read(string range)
    {
        var act = () => OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: ["192.0.2.0/24", range], proxies: ["127.0.0.1"]));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Owner:AllowedRanges:1*");
    }

    [Fact]
    public void FromConfiguration_trims_each_range_entry()
    {
        var options = OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: [" 192.0.2.0/24 "], proxies: ["127.0.0.1"]));

        options.Ranges.Should().ContainSingle().Which.ToString().Should().Be("192.0.2.0/24");
    }

    [Fact]
    public void FromConfiguration_reads_a_bare_ipv4_address_as_a_single_host()
    {
        var options = OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: ["192.0.2.10"], proxies: ["127.0.0.1"]));

        options.Ranges.Should().ContainSingle().Which.ToString().Should().Be("192.0.2.10/32");
        options.Allows(IPAddress.Parse("192.0.2.10")).Should().BeTrue();
        options.Allows(IPAddress.Parse("192.0.2.11")).Should().BeFalse();
    }

    [Fact]
    public void FromConfiguration_reads_a_bare_ipv6_address_as_a_single_host()
    {
        var options = OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: ["2001:db8::1"], proxies: ["127.0.0.1"]));

        options.Ranges.Should().ContainSingle().Which.ToString().Should().Be("2001:db8::1/128");
        options.Allows(IPAddress.Parse("2001:db8::1")).Should().BeTrue();
        options.Allows(IPAddress.Parse("2001:db8::2")).Should().BeFalse();
    }

    [Theory]
    [InlineData("192.0.2.5/24", "192.0.2.0/24")]
    [InlineData("192.0.2.255/25", "192.0.2.128/25")]
    [InlineData("2001:db8::7/64", "2001:db8::/64")]
    public void FromConfiguration_clears_the_host_bits_of_a_range(string configured, string expected)
    {
        var options = OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: [configured], proxies: ["127.0.0.1"]));

        options.Ranges.Should().ContainSingle().Which.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("Kestrel:Endpoints:Web:Url", "http://127.0.0.1:5082")]
    [InlineData("Kestrel:Endpoints:Ops:Url", "http://127.0.0.1:5082")]
    [InlineData("Kestrel:Endpoints:Web:Url", "http://*:5082")]
    [InlineData("Kestrel:Endpoints:Ops:Url", "http://[::1]:5082")]
    public void FromConfiguration_rejects_an_owner_port_shared_with_another_listener(string otherKey, string otherUrl)
    {
        var configuration = Build(OwnerUrl, ranges: ["192.0.2.0/24"], proxies: ["127.0.0.1"], (otherKey, otherUrl));

        var act = () => OwnerAccess.FromConfiguration(configuration);

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{otherKey}*");
    }

    [Fact]
    public void FromConfiguration_accepts_an_owner_port_that_differs_from_the_other_listeners()
    {
        var configuration = Build(
            OwnerUrl,
            ranges: ["192.0.2.0/24"],
            proxies: ["127.0.0.1"],
            ("Kestrel:Endpoints:Web:Url", "http://127.0.0.1:5080"),
            ("Kestrel:Endpoints:Ops:Url", "http://127.0.0.1:5081"));

        var options = OwnerAccess.FromConfiguration(configuration);

        options.Port.Should().Be(5082);
    }

    [Theory]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://127.0.0.1:80")]
    [InlineData("https://127.0.0.1:5443")]
    [InlineData("127.0.0.1:5082")]
    [InlineData("not a url")]
    public void FromConfiguration_rejects_an_owner_url_without_an_explicit_port_or_not_http(string url)
    {
        var act = () => OwnerAccess.FromConfiguration(Build(url, ranges: ["192.0.2.0/24"], proxies: ["127.0.0.1"]));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Kestrel:Endpoints:Owner:Url*");
    }

    [Fact]
    public void FromConfiguration_names_the_key_and_index_but_never_the_configured_value()
    {
        var act = () => OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: ["198.51.100.123/33"], proxies: ["127.0.0.1"]));

        var message = act.Should().Throw<InvalidOperationException>().Which.Message;

        message.Should().Contain("Owner:AllowedRanges:0");
        message.Should().NotContain("198.51.100.123");
        message.Should().NotContain("/33");
    }

    [Theory]
    [InlineData("203.0.113.77/0")]
    [InlineData("::ffff:203.0.113.77/120")]
    [InlineData("word-203.0.113.77")]
    public void FromConfiguration_never_repeats_a_rejected_range_in_its_message(string range)
    {
        var act = () => OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: [range], proxies: ["127.0.0.1"]));

        act.Should().Throw<InvalidOperationException>().Which.Message.Should().NotContain("203.0.113.77");
    }

    [Fact]
    public void Allows_treats_an_ipv4_address_in_mapped_form_as_the_plain_address()
    {
        var options = OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: ["192.0.2.0/24"], proxies: ["127.0.0.1"]));

        options.Allows(IPAddress.Parse("::ffff:192.0.2.7")).Should().BeTrue();
        options.Allows(IPAddress.Parse("::ffff:203.0.113.7")).Should().BeFalse();
    }

    [Fact]
    public void Allows_refuses_a_missing_address()
    {
        var options = OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: ["192.0.2.0/24"], proxies: ["127.0.0.1"]));

        options.Allows(null).Should().BeFalse();
    }

    [Fact]
    public void Allows_refuses_an_ipv6_address_when_only_ipv4_ranges_are_configured()
    {
        var options = OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: ["192.0.2.0/24", "127.0.0.0/8"], proxies: ["127.0.0.1"]));

        options.Allows(IPAddress.IPv6Loopback).Should().BeFalse();
    }

    [Fact]
    public void Allows_refuses_the_address_of_a_known_proxy_even_when_a_range_holds_it()
    {
        var options = OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: ["192.0.2.0/24"], proxies: ["192.0.2.10"]));

        options.Allows(IPAddress.Parse("192.0.2.10")).Should().BeFalse();
        options.Allows(IPAddress.Parse("::ffff:192.0.2.10")).Should().BeFalse();
        options.Allows(IPAddress.Parse("192.0.2.11")).Should().BeTrue();
    }

    [Fact]
    public void Allows_lets_a_loopback_proxy_through_so_a_single_machine_can_play_both_parts()
    {
        var options = OwnerAccess.FromConfiguration(Build(OwnerUrl, ranges: ["127.0.0.0/8"], proxies: ["127.0.0.1"]));

        options.Allows(IPAddress.Loopback).Should().BeTrue();
    }

    private static IConfiguration Build(
        string? ownerUrl,
        string[] ranges,
        string[] proxies,
        params (string Key, string Value)[] extra)
    {
        var values = new Dictionary<string, string?>();
        if (ownerUrl is not null)
        {
            values["Kestrel:Endpoints:Owner:Url"] = ownerUrl;
        }

        for (var index = 0; index < ranges.Length; index++)
        {
            values[$"Owner:AllowedRanges:{index}"] = ranges[index];
        }

        for (var index = 0; index < proxies.Length; index++)
        {
            values[$"ReverseProxy:KnownProxies:{index}"] = proxies[index];
        }

        foreach (var (key, value) in extra)
        {
            values[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
