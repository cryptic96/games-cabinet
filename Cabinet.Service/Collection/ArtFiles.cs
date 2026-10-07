using Cabinet.Domain.Layout;
using Cabinet.Repository.Images;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Cabinet.Service.Collection;

/// <summary>
/// Serves the stored pictures from the site's own origin. Only the art directory is reachable, only <c>.webp</c> files are
/// served and always as <c>image/webp</c>, and every answer is immutable because a file's name carries a hash of its
/// content, so a changed picture is a new address and the browser never shows a stale one.
/// </summary>
public static class ArtFiles
{
    private const string WebpExtension = ".webp";
    private const string WebpContentType = "image/webp";
    private const string ImmutableForAYear = "public, max-age=31536000, immutable";

    /// <summary>Serves the art directory under <see cref="ArtFitting.RequestPath"/>.</summary>
    /// <param name="app">The application to add the file serving to.</param>
    public static IApplicationBuilder UseCabinetArt(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var cache = app.ApplicationServices.GetRequiredService<ArtCache>();
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings.Clear();
        contentTypes.Mappings[WebpExtension] = WebpContentType;

        return app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(cache.Path),
            RequestPath = ArtFitting.RequestPath,
            ContentTypeProvider = contentTypes,
            ServeUnknownFileTypes = false,
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.CacheControl = ImmutableForAYear;
                context.Context.Response.Headers.XContentTypeOptions = "nosniff";
            },
        });
    }
}
