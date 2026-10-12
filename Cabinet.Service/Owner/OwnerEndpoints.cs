using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Cabinet.Service.Owner;

/// <summary>
/// Registers the owner tools: the start-up check of their access settings, the rules every page under the owner folder
/// shares, and the antiforgery settings their forms rely on. Nothing here adds a login, an account or a session; the network
/// location, enforced by <see cref="OwnerAccess"/>, is the only lock.
/// </summary>
public static class OwnerEndpoints
{
    /// <summary>The path of the pictures page.</summary>
    public const string PicturesPath = "/owner/pictures";

    /// <summary>The path of the locations page.</summary>
    public const string LocationsPath = "/owner/locations";

    /// <summary>The path of the settings page.</summary>
    public const string SettingsPath = "/owner/settings";

    private const int DefaultBodyLimitBytes = 64 * 1024;

    /// <summary>
    /// Validates the owner access settings now, so a half-configured owner endpoint stops the start-up instead of serving, and
    /// marks every page under the owner folder as owner-only with a small request body limit.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The hosting environment; the antiforgery cookie is https-only in production.</param>
    public static IServiceCollection AddCabinetOwner(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddSingleton(OwnerAccess.FromConfiguration(configuration));

        services.Configure<RazorPagesOptions>(options =>
            options.Conventions.AddFolderApplicationModelConvention("/Owner", model =>
            {
                model.EndpointMetadata.Add(new OwnerOnlyAttribute());
                model.Filters.Add(new RequestSizeLimitAttribute(DefaultBodyLimitBytes));
            }));

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "cabinet-owner-af";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = environment.IsProduction()
                ? CookieSecurePolicy.Always
                : CookieSecurePolicy.SameAsRequest;
            options.HeaderName = "RequestVerificationToken";
        });

        return services;
    }
}
