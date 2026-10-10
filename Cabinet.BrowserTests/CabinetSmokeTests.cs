using Cabinet.BrowserTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.BrowserTests;

/// <summary>Proves a real browser can load the cabinet page from the host, draw boxes, and stay on the host's own origin.</summary>
[Trait("Category", "Browser")]
public sealed class CabinetSmokeTests : CabinetPageTest
{
    private static readonly Dictionary<string, string?> PrototypeOn = new() { ["Prototype:Enabled"] = "true" };

    [Fact]
    public async Task The_sample_cabinet_draws_boxes_without_console_errors_or_requests_to_other_hosts()
    {
        await Page.SetViewportSizeAsync(1440, 900);
        await StartAsync(PrototypeOn);

        await GotoCabinetAsync("/?sample=65");
        var placementCount = await Page.Locator("#cabinet .placement").CountAsync();
        await SaveScreenshotAsync("smoke-desktop");

        placementCount.Should().BeGreaterThan(0);
        ConsoleErrors.Should().BeEmpty();
        RequestHosts.Should().OnlyContain(host => host == $"127.0.0.1:{PublicPort}");
    }
}
