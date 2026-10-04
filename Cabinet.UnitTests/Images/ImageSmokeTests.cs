using System.Text;
using Cabinet.Repository.Images;
using FluentAssertions;
using SkiaSharp;

namespace Cabinet.UnitTests.Images;

/// <summary>Verifies that the imaging stack decodes, resizes and encodes WebP.</summary>
public class ImageSmokeTests
{
    /// <summary>The smoke run reports the resized dimensions and a non-empty encoded payload.</summary>
    [Fact]
    public void Run_ReportsResizedDimensionsAndPositiveByteCount()
    {
        var result = ImageSmoke.Run();

        result.Width.Should().Be(480);
        result.Height.Should().Be(360);
        result.Bytes.Should().BeGreaterThan(0);
    }

    /// <summary>The encoded sample is a RIFF container carrying a WEBP marker.</summary>
    [Fact]
    public void EncodeSample_ProducesRiffWebpContainer()
    {
        var bytes = ImageSmoke.EncodeSample();

        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("RIFF");
        Encoding.ASCII.GetString(bytes, 8, 4).Should().Be("WEBP");
    }

    /// <summary>The encoded sample decodes back to the expected resized dimensions.</summary>
    [Fact]
    public void EncodeSample_DecodesBackToResizedDimensions()
    {
        var bytes = ImageSmoke.EncodeSample();

        using var decoded = SKBitmap.Decode(bytes);

        decoded.Should().NotBeNull();
        decoded.Width.Should().Be(480);
        decoded.Height.Should().Be(360);
    }

    /// <summary>A payload without the expected markers is rejected with a plain error.</summary>
    [Fact]
    public void Verify_RejectsBytesWithoutWebpMarkers()
    {
        var act = () => ImageSmoke.Verify(new byte[64], 480, 360);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>A payload that is not an image at all is rejected with a plain error.</summary>
    [Fact]
    public void Verify_RejectsUndecodablePayload()
    {
        var bytes = new byte[64];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(bytes, 8);

        var act = () => ImageSmoke.Verify(bytes, 480, 360);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>A decodable payload with the wrong dimensions is rejected with a plain error.</summary>
    [Fact]
    public void Verify_RejectsUnexpectedDimensions()
    {
        var bytes = ImageSmoke.EncodeSample();

        var act = () => ImageSmoke.Verify(bytes, 240, 180);

        act.Should().Throw<InvalidOperationException>();
    }
}
