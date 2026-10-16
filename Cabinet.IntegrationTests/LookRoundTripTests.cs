using System.Net;
using System.Text;
using Cabinet.Domain.Owner;
using Cabinet.FakeBgg.Testing;
using Cabinet.IntegrationTests.Infrastructure;
using Cabinet.Service.Owner;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Proves that every look the owner can choose survives the whole trip: published from the settings page, written into the
/// downloaded owner data, restored onto a second fresh server, and written again as the same attribute on the root element of
/// the cabinet from the first response.
/// </summary>
[Trait("Category", "Owner")]
public sealed class LookRoundTripTests
{
    private const string SettingsPath = "/owner/settings";
    private const string LookPath = SettingsPath + "?handler=Look";
    private const string DownloadPath = SettingsPath + "?handler=Download";
    private const string CheckPath = SettingsPath + "?handler=CheckFile";
    private const string RestorePath = SettingsPath + "?handler=Restore";
    private const string FileField = "ownerDataFile";

    /// <summary>Every field with each value on its allow-list: 7 designs, 5 woods, 2 backs, 3 lights and 3 depths.</summary>
    public static TheoryData<string, string> AllowedValues
    {
        get
        {
            var cases = new TheoryData<string, string>();

            foreach (var value in CabinetLook.Designs)
            {
                cases.Add("design", value);
            }

            foreach (var value in CabinetLook.Woods)
            {
                cases.Add("wood", value);
            }

            foreach (var value in CabinetLook.BackChoices)
            {
                cases.Add("backs", value);
            }

            foreach (var value in CabinetLook.LightChoices)
            {
                cases.Add("lights", value);
            }

            foreach (var value in CabinetLook.DepthChoices)
            {
                cases.Add("depth", value);
            }

            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(AllowedValues))]
    public async Task An_allowed_value_survives_publishing_downloading_restoring_and_the_first_paint(string field, string value)
    {
        var fields = new Dictionary<string, string>
        {
            ["design"] = CabinetLook.Default.Design,
            ["wood"] = CabinetLook.Default.Wood,
            ["backs"] = CabinetLook.Default.Backs,
            ["lights"] = CabinetLook.Default.Lights,
            ["depth"] = CabinetLook.Default.Depth,
            [field] = value,
        };
        await using var first = NewFactory();
        using var firstSession = new OwnerSession(first);
        await firstSession.GetAsync(SettingsPath);
        await firstSession.PostJsonAsync(LookPath, [.. fields.Select(entry => new KeyValuePair<string, string>(entry.Key, entry.Value)), new("revision", "0")]);
        var download = await firstSession.PostFormAsync(DownloadPath, []);
        await using var second = NewFactory();
        using var secondSession = new OwnerSession(second);
        using var publicClient = second.CreatePublicClient();

        var check = await secondSession.PostFileAsync(CheckPath, FileField, Encoding.UTF8.GetBytes(download.Body));
        var preview = await secondSession.GetAsync(check.Location!);
        var restored = await secondSession.PostFormAsync(RestorePath, [new("token", OwnerSession.FieldValue(preview.Body, "token"))]);
        var page = await publicClient.GetStringAsync("/", TestContext.Current.CancellationToken);

        download.Body.Should().Contain($"\"{field}\": \"{value}\"").And.Contain("\"schemaVersion\": 3");
        restored.Status.Should().Be(HttpStatusCode.OK);
        page.Should().Contain($"data-{field}=\"{value}\"");
        ((OwnerDataState.Ready)second.ServingServices.GetRequiredService<OwnerDataService>().State).Data.Look
            .Should().Be(new CabinetLook(fields["design"], fields["wood"], fields["backs"], fields["lights"], fields["depth"]));
    }

    [Fact]
    public void The_cases_cover_every_allowed_value_of_every_field()
    {
        AllowedValues.Should().HaveCount(20);
    }

    private static CabinetWebApplicationFactory NewFactory() =>
        SyncHarness.CreateFactory(new ScriptedBggHandler(), SyncHarness.NewClock());
}
