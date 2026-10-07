using System.Net.Http.Headers;
using FluentAssertions;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>Assertions on response headers that fail when the header is missing instead of passing silently.</summary>
public static class HttpAssertions
{
    /// <summary>Asserts the response names a content type and that its media type equals <paramref name="expected"/>.</summary>
    /// <param name="response">The response under test.</param>
    /// <param name="expected">The expected media type, without parameters.</param>
    /// <param name="because">Context shown when the assertion fails.</param>
    public static void ShouldHaveMediaType(this HttpResponseMessage response, string expected, string because = "")
    {
        MediaTypeHeaderValue? contentType = response.Content.Headers.ContentType;
        contentType.Should().NotBeNull("the response must name its content type. {0}", because);
        contentType!.MediaType.Should().Be(expected, because);
    }

    /// <summary>Asserts the response names a content type and that its media type starts with <paramref name="prefix"/>.</summary>
    /// <param name="response">The response under test.</param>
    /// <param name="prefix">The expected start of the media type, for example a media family.</param>
    public static void ShouldHaveMediaTypeStartingWith(this HttpResponseMessage response, string prefix)
    {
        MediaTypeHeaderValue? contentType = response.Content.Headers.ContentType;
        contentType.Should().NotBeNull("the response must name its content type");
        contentType!.MediaType.Should().StartWith(prefix);
    }

    /// <summary>Asserts the response carries a Cache-Control header with the no-store directive.</summary>
    /// <param name="response">The response under test.</param>
    public static void ShouldBeNoStore(this HttpResponseMessage response)
    {
        CacheControlHeaderValue? cacheControl = response.Headers.CacheControl;
        cacheControl.Should().NotBeNull("the response must carry a Cache-Control header");
        cacheControl!.NoStore.Should().BeTrue();
    }
}
