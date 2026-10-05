using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;

namespace Cabinet.Service.Layout;

/// <summary>
/// The undocumented endpoint the cabinet page reads. It only builds layouts for sample and profile names on fixed
/// allowlists, so a visitor cannot ask for an arbitrary collection size, and it sends no cross-origin headers.
/// </summary>
public static class LayoutEndpoint
{
    /// <summary>The route the cabinet page fetches.</summary>
    public const string Route = "/cabinet/layout";

    /// <summary>Maps the layout route; unknown sample or profile names answer 404.</summary>
    public static IEndpointRouteBuilder MapCabinetLayout(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Route, (string? sample, string? profile) =>
        {
            if (!SyntheticCollections.TryGetSample(sample, out var items) || !SectionDesigns.TryGet(profile, out var design))
            {
                return Results.NotFound();
            }

            var layout = CabinetLayoutEngine.Build(items, design);

            return Results.Content(LayoutJson.Serialize(layout), "application/json");
        });

        return endpoints;
    }
}
