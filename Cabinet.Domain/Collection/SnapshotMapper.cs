using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cabinet.Domain.Layout;

namespace Cabinet.Domain.Collection;

/// <summary>Turns the stored collection into the items the layout engine draws, in an order that does not depend on the source.</summary>
public static class SnapshotMapper
{
    private const int VersionLength = 16;

    /// <summary>
    /// Maps every stored item to an item to draw, ordered by collection entry and then by game, so the same collection in
    /// any source order gives the same cabinet.
    /// </summary>
    /// <param name="snapshot">The stored collection.</param>
    public static IReadOnlyList<CabinetItem> ToCabinetItems(CollectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return
        [
            .. snapshot.Items
                .OrderBy(item => item.CollectionId)
                .ThenBy(item => item.GameId)
                .Select(item => new CabinetItem(item.GameId, item.CollectionId, item.Title, item.Kind, DefaultBox(item.Kind), [])),
        ];
    }

    /// <summary>
    /// The identifier of a collection: the first sixteen lowercase hexadecimal characters of a SHA-256 hash over the mapped
    /// items in order. It changes whenever anything that is drawn changes and not otherwise.
    /// </summary>
    /// <param name="items">The mapped items, in mapped order.</param>
    public static string Version(IReadOnlyList<CabinetItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var lines = items.Select(item => string.Create(
            CultureInfo.InvariantCulture,
            $"{item.CollectionId}|{item.BggId}|{item.Kind}|{item.Title}|{item.Box.WidthMm}|{item.Box.HeightMm}|{item.Box.DepthMm}"));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', lines)));

        return Convert.ToHexStringLower(hash)[..VersionLength];
    }

    /// <summary>The box size used for an item whose real size is not known.</summary>
    /// <param name="kind">Whether the item is a standalone game or an expansion.</param>
    public static BoxDimensions DefaultBox(ItemKind kind) =>
        kind == ItemKind.Expansion ? new BoxDimensions(200, 260, 40) : new BoxDimensions(225, 300, 60);
}
