using Cabinet.Domain.Owner;

namespace Cabinet.Service.Pages.Owner;

/// <summary>What the partial that draws the preview of a linked picture needs: the preview itself, the staged picture it was made from and the page's state.</summary>
/// <param name="GameId">The game the link is for.</param>
/// <param name="CollectionId">The copy of that game the link is for.</param>
/// <param name="Token">The token of the staged picture, which the preview image is served under.</param>
/// <param name="Preview">The preview to draw.</param>
/// <param name="Revision">The revision the page was built from, which the form that uses the picture carries.</param>
public sealed record LinkPreviewModel(int GameId, long CollectionId, string Token, LinkPreview Preview, long Revision)
{
    /// <summary>How many pixels one millimetre of the preview takes when no script scales it, which is the scale the preview shelf uses by default.</summary>
    public const double PixelsPerMillimetre = 0.5;

    /// <summary>The size in pixels of a length in millimetres at the preview's default scale.</summary>
    /// <param name="millimetres">The length in millimetres.</param>
    public static int Pixels(int millimetres) => (int)Math.Round(millimetres * PixelsPerMillimetre, MidpointRounding.AwayFromZero);
}
