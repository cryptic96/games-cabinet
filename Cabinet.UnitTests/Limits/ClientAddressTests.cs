using System.Net;
using Cabinet.Service.Limits;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Cabinet.UnitTests.Limits;

/// <summary>Verifies the key a request is counted under: IPv4 as itself, mapped IPv4 as IPv4, IPv6 by its /64 prefix.</summary>
public class ClientAddressTests
{
    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    public void An_address_is_counted_under_its_key(string address, string expected)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);

        ClientAddress.PartitionKey(context).Should().Be(expected);
    }

    [Fact]
    public void A_request_without_an_address_is_counted_as_unknown()
    {
        ClientAddress.PartitionKey(new DefaultHttpContext()).Should().Be("unknown");
    }
}
