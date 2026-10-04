using System.Net;
using System.Reflection;
using Cabinet.Domain;
using Cabinet.Service.Hosting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsProduction())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddSystemdConsole();
}

builder.Services.Configure<KestrelServerOptions>(options => options.AddServerHeader = false);

builder.Services.AddHealthChecks();

builder.Services.AddRazorPages();

var buildInfo = BuildInfo.Parse(
    typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
builder.Services.AddSingleton(buildInfo);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;

    var knownProxies = builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
    foreach (var proxy in knownProxies)
    {
        if (!IPAddress.TryParse(proxy, out var address))
        {
            throw new InvalidOperationException(
                "ReverseProxy:KnownProxies contains an entry that is not an IP address.");
        }

        options.KnownProxies.Add(address);
    }
});

var app = builder.Build();

var opsPort = OpsEndpoint.FromConfiguration(app.Configuration);

app.UseForwardedHeaders();

app.UseHealthChecks("/health", opsPort, new HealthCheckOptions
{
    ResponseWriter = (context, result) =>
    {
        context.Response.ContentType = "application/json";
        var build = context.RequestServices.GetRequiredService<BuildInfo>();

        return context.Response.WriteAsJsonAsync(new
        {
            status = result.Status.ToString(),
            version = build.Version,
            commit = build.Commit,
        });
    }
});

app.UseRouting();

app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

await app.RunAsync();

/// <summary>Entry point for the cabinet host, exposed as a partial class so integration tests can boot it in-process.</summary>
public partial class Program;
