using SkiaSharp;

namespace Cabinet.FakeBgg;

/// <summary>The invented pictures that stand in for box art in tests and in the fake image host.</summary>
public enum SyntheticArtKind
{
    /// <summary>A full-bleed illustration in the 3:4 shape of a typical box front.</summary>
    FlatCover,

    /// <summary>A full-bleed illustration that is relatively wider than a typical box front.</summary>
    FlatWide,

    /// <summary>A full-bleed illustration that is relatively narrower than a typical box front.</summary>
    FlatNarrow,

    /// <summary>A very wide full-bleed illustration.</summary>
    Banner,

    /// <summary>A cover inside a thick white frame.</summary>
    WhiteFramed,

    /// <summary>A cover inside a thin black frame.</summary>
    BlackFramed,

    /// <summary>A white picture with one small light-grey band.</summary>
    AllWhite,

    /// <summary>A full-bleed near-black field with one centred band.</summary>
    NearBlack,

    /// <summary>A full-bleed mid-green field with one centred band, close to the hardest case for legible text.</summary>
    MidGreen,

    /// <summary>A full-bleed vertical gradient with no plain backdrop in it.</summary>
    GradientFullBleed,

    /// <summary>A photographed-style 3D box on a white backdrop.</summary>
    BoxOnWhite,

    /// <summary>A photographed-style 3D box on a light grey gradient backdrop.</summary>
    BoxOnGreyGradient,

    /// <summary>A photographed-style 3D box on a near-black backdrop.</summary>
    BoxOnBlack,

    /// <summary>A photographed-style 3D box on a fully transparent backdrop.</summary>
    BoxTransparent,

    /// <summary>Bytes that are not a picture at all.</summary>
    Undecodable,

    /// <summary>A cut-out 3D box cropped close to its sides on a fully transparent backdrop, with a see-through shadow below and to the right.</summary>
    BoxTransparentShadow,

    /// <summary>A 3D box cropped close to its sides on a white-to-near-white backdrop with compression-like noise and a soft contact shadow.</summary>
    BoxOnNoisyWhite,

    /// <summary>A flat cover inside a thick near-black border, with near-black art touching the border in two corners.</summary>
    DarkBorderCover,
}

/// <summary>
/// Draws invented box art in code, the same bytes every time. No captured picture ever enters the repository: every
/// fixture the art detector and the colour extraction are tested against comes from here.
/// </summary>
public static class SyntheticArt
{
    private const int CoverWidth = 600;
    private const int CoverHeight = 800;
    private const int BoxCanvasSize = 800;
    private const int PngQuality = 100;
    private const float BoxTiltDegrees = -14f;
    private const float WhiteFrameShare = 0.15f;
    private const float BlackFrameShare = 0.025f;
    private const int CroppedShotWidth = 760;
    private const int CroppedShotHeight = 640;
    private const float CroppedShotInset = 2f;
    private const float CroppedFrontWidth = 360f;
    private const float CroppedFrontHeight = 230f;
    private const float CroppedDepthX = 110f;
    private const float CroppedDepthY = -60f;
    private const int NoiseBlock = 8;
    private const int WhitePadding = 24;
    private const int NoisyBackdropTop = 234;
    private const int NoisyBackdropBottom = 230;
    private const int NoiseRange = 6;
    private const int DarkBorderWidth = 42;
    private const float DarkTriangleShare = 0.40f;

    private static readonly SKColor FramedFieldColour = new(20, 130, 140);

    private static readonly byte[] UndecodableBytes = "these bytes are not a picture"u8.ToArray();

    /// <summary>Every kind, in declaration order.</summary>
    public static IReadOnlyList<SyntheticArtKind> All { get; } = Enum.GetValues<SyntheticArtKind>();

    /// <summary>The width and height in pixels that <see cref="Encode"/> produces for the kind.</summary>
    public static (int Width, int Height) SizeOf(SyntheticArtKind kind) => kind switch
    {
        SyntheticArtKind.FlatWide => (900, 800),
        SyntheticArtKind.FlatNarrow => (500, 800),
        SyntheticArtKind.Banner => (1200, 300),
        SyntheticArtKind.BoxOnWhite or SyntheticArtKind.BoxOnGreyGradient or SyntheticArtKind.BoxOnBlack or SyntheticArtKind.BoxTransparent
            => (BoxCanvasSize, BoxCanvasSize),
        SyntheticArtKind.Undecodable => (0, 0),
        SyntheticArtKind.BoxTransparentShadow or SyntheticArtKind.BoxOnNoisyWhite => (CroppedShotWidth, CroppedShotHeight),
        _ => (CoverWidth, CoverHeight),
    };

    /// <summary>
    /// Draws the picture and returns it as a PNG, except <see cref="SyntheticArtKind.Undecodable"/>, which returns fixed
    /// bytes that no decoder accepts.
    /// </summary>
    public static byte[] Encode(SyntheticArtKind kind)
    {
        if (kind == SyntheticArtKind.Undecodable)
        {
            return [.. UndecodableBytes];
        }

        var (width, height) = SizeOf(kind);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            Draw(canvas, kind, width, height);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, PngQuality);

        return png.ToArray();
    }

    private static void Draw(SKCanvas canvas, SyntheticArtKind kind, int width, int height)
    {
        switch (kind)
        {
            case SyntheticArtKind.FlatCover:
                DrawDiagonalIllustration(canvas, width, height, new SKColor(120, 12, 24), new SKColor(244, 150, 36));
                break;
            case SyntheticArtKind.FlatWide:
                DrawDiagonalIllustration(canvas, width, height, new SKColor(10, 60, 110), new SKColor(80, 200, 190));
                break;
            case SyntheticArtKind.FlatNarrow:
                DrawDiagonalIllustration(canvas, width, height, new SKColor(60, 20, 100), new SKColor(220, 80, 160));
                break;
            case SyntheticArtKind.Banner:
                DrawBanner(canvas, width, height);
                break;
            case SyntheticArtKind.WhiteFramed:
                DrawFramedCover(canvas, width, height, new SKColor(255, 255, 255), WhiteFrameShare);
                break;
            case SyntheticArtKind.BlackFramed:
                DrawFramedCover(canvas, width, height, new SKColor(0, 0, 0), BlackFrameShare);
                break;
            case SyntheticArtKind.AllWhite:
                DrawAllWhite(canvas, width, height);
                break;
            case SyntheticArtKind.NearBlack:
                DrawFieldWithBand(canvas, width, height, new SKColor(18, 18, 22), new SKColor(205, 160, 40));
                break;
            case SyntheticArtKind.MidGreen:
                DrawFieldWithBand(canvas, width, height, new SKColor(51, 153, 51), new SKColor(240, 232, 190));
                break;
            case SyntheticArtKind.GradientFullBleed:
                DrawVerticalGradient(canvas, width, height, [new SKColor(6, 22, 96), new SKColor(40, 90, 190), new SKColor(200, 236, 255)]);
                DrawTitleBand(canvas, width, height, new SKColor(255, 250, 235, 230));
                break;
            case SyntheticArtKind.BoxOnWhite:
                DrawBoxShot(canvas, new SKColor(255, 255, 255), new SKColor(189, 30, 40), true);
                break;
            case SyntheticArtKind.BoxOnGreyGradient:
                DrawVerticalGradient(canvas, width, height, [new SKColor(232, 232, 234), new SKColor(212, 212, 216)]);
                DrawVignette(canvas, width, height);
                DrawBoxShot(canvas, null, new SKColor(40, 90, 169), true);
                break;
            case SyntheticArtKind.BoxOnBlack:
                DrawVerticalGradient(canvas, width, height, [new SKColor(24, 24, 28), new SKColor(8, 8, 10)]);
                DrawVignette(canvas, width, height);
                DrawBoxShot(canvas, null, new SKColor(218, 168, 29), false);
                break;
            case SyntheticArtKind.BoxTransparent:
                DrawBoxShot(canvas, SKColors.Transparent, new SKColor(59, 148, 69), false);
                break;
            case SyntheticArtKind.BoxTransparentShadow:
                DrawTransparentShadowShot(canvas, width, height);
                break;
            case SyntheticArtKind.BoxOnNoisyWhite:
                DrawNoisyWhiteShot(canvas, width, height);
                break;
            case SyntheticArtKind.DarkBorderCover:
                DrawDarkBorderCover(canvas, width, height);
                break;
            default:
                throw new NotSupportedException($"No drawing exists for {kind}.");
        }
    }

    private static void DrawTransparentShadowShot(SKCanvas canvas, int width, int height)
    {
        var fitted = FitBox(width, 20f, CroppedFrontHeight);

        using (var shadowPaint = new SKPaint
        {
            Color = new SKColor(0, 0, 0, 110),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 14f),
        })
        {
            canvas.DrawOval(new SKRect(fitted.Bounds.Left + 60, fitted.Bounds.Bottom - 60, width - CroppedShotInset, height), shadowPaint);
        }

        DrawFittedBox(canvas, fitted, new SKColor(150, 60, 170));
    }

    private static void DrawNoisyWhiteShot(SKCanvas canvas, int width, int height)
    {
        canvas.Clear(new SKColor(255, 255, 255));
        canvas.Save();
        canvas.Translate(WhitePadding, WhitePadding);

        var photoWidth = width - (2 * WhitePadding);
        var photoHeight = height - (2 * WhitePadding);
        DrawNoisyBackdrop(canvas, photoWidth, photoHeight);
        var fitted = FitBox(photoWidth, 2f, CroppedFrontHeight);

        using (var shadowPaint = new SKPaint
        {
            Color = new SKColor(60, 60, 64, 80),
            IsAntialias = true,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 10f),
        })
        {
            canvas.DrawOval(new SKRect(fitted.Bounds.Left + 60, fitted.Bounds.Bottom - 30, fitted.Bounds.Right, photoHeight), shadowPaint);
        }

        DrawFittedBox(canvas, fitted, new SKColor(30, 110, 60));
        canvas.Restore();
    }

    private static void DrawNoisyBackdrop(SKCanvas canvas, int width, int height)
    {
        var pixels = new SKColor[width * height];

        for (var y = 0; y < height; y++)
        {
            var mix = (float)y / (height - 1);
            var level = (int)Math.Round(NoisyBackdropTop + ((NoisyBackdropBottom - NoisyBackdropTop) * mix));

            for (var x = 0; x < width; x++)
            {
                var red = level + NoiseShift(x / NoiseBlock, y / NoiseBlock, 0);
                var green = level + NoiseShift(x / NoiseBlock, y / NoiseBlock, 1);
                var blue = level + 2 + NoiseShift(x / NoiseBlock, y / NoiseBlock, 2);
                pixels[(y * width) + x] = new SKColor((byte)Math.Clamp(red, 0, 255), (byte)Math.Clamp(green, 0, 255), (byte)Math.Clamp(blue, 0, 255));
            }
        }

        using var backdrop = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        backdrop.Pixels = pixels;
        canvas.DrawBitmap(backdrop, 0f, 0f, new SKSamplingOptions(), null);
    }

    private static int NoiseShift(int blockX, int blockY, int channel)
    {
        unchecked
        {
            var hash = (uint)((blockX * 73856093) ^ (blockY * 19349663) ^ (channel * 83492791));
            hash ^= hash >> 13;
            hash *= 1274126177u;
            hash ^= hash >> 16;

            return (int)(hash % ((2 * NoiseRange) + 1)) - NoiseRange;
        }
    }

    private static void DrawDarkBorderCover(SKCanvas canvas, int width, int height)
    {
        canvas.Clear(new SKColor(14, 14, 16));
        var inner = new SKRect(DarkBorderWidth, DarkBorderWidth, width - DarkBorderWidth, height - DarkBorderWidth);

        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(inner.Left, inner.Top),
                new SKPoint(inner.Right, inner.Bottom),
                [new SKColor(232, 168, 40), new SKColor(196, 60, 40)],
                SKShaderTileMode.Clamp);
            canvas.DrawRect(inner, paint);
        }

        using (var band = new SKPaint { Color = new SKColor(255, 244, 214, 220), IsAntialias = true })
        {
            canvas.DrawRect(
                new SKRect(inner.Left + (inner.Width * 0.08f), inner.Top + (inner.Height * 0.40f), inner.Right - (inner.Width * 0.08f), inner.Top + (inner.Height * 0.52f)),
                band);
        }

        var reachX = inner.Width * DarkTriangleShare;
        var reachY = inner.Height * DarkTriangleShare;
        FillQuad(canvas, new SKColor(20, 20, 24), (inner.Left, inner.Top), (inner.Left + reachX, inner.Top), (inner.Left, inner.Top + reachY));
        FillQuad(canvas, new SKColor(10, 10, 12), (inner.Right, inner.Bottom), (inner.Right - reachX, inner.Bottom), (inner.Right, inner.Bottom - reachY));
    }

    private readonly record struct FittedBox(float Scale, float OffsetX, float OffsetY, float FrontHeight, SKRect Bounds);

    private static FittedBox FitBox(int canvasWidth, float topOffset, float frontHeight)
    {
        var tilt = BoxTiltDegrees * Math.PI / 180.0;
        var cos = (float)Math.Cos(tilt);
        var sin = (float)Math.Sin(tilt);
        var corners = new (float X, float Y)[]
        {
            (0, 0), (CroppedFrontWidth, 0), (CroppedFrontWidth, frontHeight), (0, frontHeight),
            (CroppedDepthX, CroppedDepthY), (CroppedFrontWidth + CroppedDepthX, CroppedDepthY),
            (CroppedFrontWidth + CroppedDepthX, frontHeight + CroppedDepthY),
        };
        var rotated = corners.Select(corner => ((corner.X * cos) - (corner.Y * sin), (corner.X * sin) + (corner.Y * cos))).ToList();
        var minX = rotated.Min(corner => corner.Item1);
        var maxX = rotated.Max(corner => corner.Item1);
        var minY = rotated.Min(corner => corner.Item2);
        var maxY = rotated.Max(corner => corner.Item2);
        var scale = (canvasWidth - (2 * CroppedShotInset)) / (maxX - minX);
        var offsetX = CroppedShotInset - (scale * minX);
        var offsetY = topOffset - (scale * minY);

        return new FittedBox(scale, offsetX, offsetY, frontHeight, new SKRect(CroppedShotInset, topOffset, canvasWidth - CroppedShotInset, topOffset + (scale * (maxY - minY))));
    }

    private static void DrawFittedBox(SKCanvas canvas, FittedBox fitted, SKColor front)
    {
        var frontHeight = fitted.FrontHeight;
        canvas.Save();
        canvas.Translate(fitted.OffsetX, fitted.OffsetY);
        canvas.Scale(fitted.Scale);
        canvas.RotateDegrees(BoxTiltDegrees);
        FillQuad(canvas, front, (0, 0), (CroppedFrontWidth, 0), (CroppedFrontWidth, frontHeight), (0, frontHeight));
        FillQuad(
            canvas,
            Shade(front, 0.62f),
            (CroppedFrontWidth, 0),
            (CroppedFrontWidth + CroppedDepthX, CroppedDepthY),
            (CroppedFrontWidth + CroppedDepthX, frontHeight + CroppedDepthY),
            (CroppedFrontWidth, frontHeight));
        FillQuad(
            canvas,
            Shade(front, 1.2f),
            (0, 0),
            (CroppedDepthX, CroppedDepthY),
            (CroppedFrontWidth + CroppedDepthX, CroppedDepthY),
            (CroppedFrontWidth, 0));
        canvas.Restore();
    }

    private static void DrawDiagonalIllustration(SKCanvas canvas, int width, int height, SKColor from, SKColor to)
    {
        using (var paint = new SKPaint())
        {
            paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width, height), [from, to], SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, width, height), paint);
        }

        DrawTitleBand(canvas, width, height, new SKColor(255, 244, 214, 220));
    }

    private static void DrawVerticalGradient(SKCanvas canvas, int width, int height, SKColor[] stops)
    {
        using var paint = new SKPaint();
        paint.Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, height), stops, SKShaderTileMode.Clamp);
        canvas.DrawRect(new SKRect(0, 0, width, height), paint);
    }

    private static void DrawBanner(SKCanvas canvas, int width, int height)
    {
        DrawVerticalGradient(canvas, width, height, [new SKColor(20, 40, 90), new SKColor(240, 200, 80)]);
        using (var sweep = new SKPaint())
        {
            sweep.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(width, 0),
                [new SKColor(220, 40, 60, 110), new SKColor(40, 200, 90, 110), new SKColor(60, 80, 230, 110), new SKColor(230, 60, 200, 110)],
                SKShaderTileMode.Clamp);
            canvas.DrawRect(new SKRect(0, 0, width, height), sweep);
        }

        DrawTitleBand(canvas, width, height, new SKColor(255, 250, 235, 230));
    }

    private static void DrawFramedCover(SKCanvas canvas, int width, int height, SKColor frame, float frameShare)
    {
        canvas.Clear(frame);
        var field = new SKRect(width * frameShare, height * frameShare, width * (1 - frameShare), height * (1 - frameShare));
        using var fieldPaint = new SKPaint { Color = FramedFieldColour, IsAntialias = true };
        canvas.DrawRect(field, fieldPaint);
        using var bandPaint = new SKPaint { Color = new SKColor(250, 236, 190), IsAntialias = true };
        canvas.DrawRect(
            new SKRect(field.Left + (field.Width * 0.1f), field.Top + (field.Height * 0.1f), field.Right - (field.Width * 0.1f), field.Top + (field.Height * 0.24f)),
            bandPaint);
    }

    private static void DrawAllWhite(SKCanvas canvas, int width, int height)
    {
        canvas.Clear(new SKColor(255, 255, 255));
        using var paint = new SKPaint { Color = new SKColor(225, 225, 225), IsAntialias = true };
        canvas.DrawRect(new SKRect((width - 200) / 2f, height * 0.16f, (width + 200) / 2f, (height * 0.16f) + 24), paint);
    }

    private static void DrawFieldWithBand(SKCanvas canvas, int width, int height, SKColor field, SKColor band)
    {
        canvas.Clear(field);
        using var paint = new SKPaint { Color = band, IsAntialias = true };
        canvas.DrawRect(new SKRect(width * 0.2f, height * 0.375f, width * 0.8f, height * 0.625f), paint);
    }

    private static void DrawTitleBand(SKCanvas canvas, int width, int height, SKColor colour)
    {
        using var paint = new SKPaint { Color = colour, IsAntialias = true };
        canvas.DrawRect(new SKRect(width * 0.08f, height * 0.10f, width * 0.92f, height * 0.26f), paint);
    }

    private static void DrawVignette(SKCanvas canvas, int width, int height)
    {
        using var paint = new SKPaint();
        paint.Shader = SKShader.CreateRadialGradient(
            new SKPoint(width / 2f, height / 2f),
            width * 0.75f,
            [new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 0), new SKColor(0, 0, 0, 12)],
            [0f, 0.6f, 1f],
            SKShaderTileMode.Clamp);
        canvas.DrawRect(new SKRect(0, 0, width, height), paint);
    }

    private static void DrawBoxShot(SKCanvas canvas, SKColor? backdrop, SKColor front, bool shadow)
    {
        if (backdrop is { } colour)
        {
            canvas.Clear(colour);
        }

        canvas.Save();
        canvas.RotateDegrees(BoxTiltDegrees, BoxCanvasSize / 2f, BoxCanvasSize / 2f);

        var left = 190f;
        var top = 250f;
        var frontWidth = 250f;
        var frontHeight = 340f;
        var depthX = 120f;
        var depthY = -70f;

        if (shadow)
        {
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 70),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 9f),
            };
            canvas.DrawOval(new SKRect(left + 20, top + frontHeight - 14, left + frontWidth + depthX + 20, top + frontHeight + 30), shadowPaint);
        }

        FillQuad(canvas, front, (left, top), (left + frontWidth, top), (left + frontWidth, top + frontHeight), (left, top + frontHeight));
        FillQuad(
            canvas,
            Shade(front, 0.62f),
            (left + frontWidth, top),
            (left + frontWidth + depthX, top + depthY),
            (left + frontWidth + depthX, top + frontHeight + depthY),
            (left + frontWidth, top + frontHeight));
        FillQuad(
            canvas,
            Shade(front, 1.2f),
            (left, top),
            (left + depthX, top + depthY),
            (left + frontWidth + depthX, top + depthY),
            (left + frontWidth, top));
        canvas.Restore();
    }

    private static void FillQuad(SKCanvas canvas, SKColor colour, params (float X, float Y)[] corners)
    {
        var builder = new SKPathBuilder();
        builder.MoveTo(corners[0].X, corners[0].Y);
        for (var index = 1; index < corners.Length; index++)
        {
            builder.LineTo(corners[index].X, corners[index].Y);
        }

        builder.Close();
        using var path = builder.Detach();
        using var paint = new SKPaint { Color = colour, IsAntialias = true };
        canvas.DrawPath(path, paint);
    }

    private static SKColor Shade(SKColor colour, float factor) => new(
        (byte)Math.Clamp(colour.Red * factor, 0, 255),
        (byte)Math.Clamp(colour.Green * factor, 0, 255),
        (byte)Math.Clamp(colour.Blue * factor, 0, 255),
        colour.Alpha);
}
