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

    /// <summary>
    /// Reads and validates the Layout settings now and registers them as a singleton, so a bad value stops the app at
    /// startup instead of failing the first request.
    /// </summary>
    public static IServiceCollection AddCabinetLayout(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddSingleton(LayoutSettings.FromConfiguration(configuration));
    }

    /// <summary>Maps the layout route; unknown sample or profile names answer 404.</summary>
    public static IEndpointRouteBuilder MapCabinetLayout(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(Route, (string? sample, string? profile, HttpContext context) =>
        {
            if (!SyntheticCollections.TryGetSample(sample, out var items) || !SectionDesigns.TryGet(profile, out var design))
            {
                return Results.NotFound();
            }

            var options = context.RequestServices.GetRequiredService<LayoutOptions>();
            var layout = CabinetLayoutEngine.Build(items, design, options);

            return Results.Content(LayoutJson.Serialize(layout), "application/json");
        });

        return endpoints;
    }
}
