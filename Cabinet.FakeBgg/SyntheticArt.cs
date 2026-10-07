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
            default:
                throw new NotSupportedException($"No drawing exists for {kind}.");
        }
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
