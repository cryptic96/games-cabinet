using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Cabinet.Domain.Layout;
using Cabinet.Domain.Samples;
using FluentAssertions;

namespace Cabinet.UnitTests.Layout;

/// <summary>
/// Locks the arrangement down. Layouts for the small samples are recorded in full, the largest sample by its digest, and
/// the version record ties them to the layout version, so any change to the arrangement is a visible diff in review and
/// an intended one comes with a raised version. Re-recording is a local action behind an environment switch that no
/// automated run sets.
/// </summary>
public class LayoutGoldenTests
{
    private const string UpdateSwitch = "CABINET_UPDATE_GOLDENS";
    private const string VersionFileName = "layout-version.txt";
    private const string LargeSample = "400";
    private const string VersionKey = "layoutVersion: ";
    private const string DigestKey = "digest: ";
    private const string RerecordCommand =
        "CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait \"Category=Layout\"";

    private static readonly string[] FullSamples = ["0", "1", "5", "12", "65"];
    private static readonly string[] Profiles = [SectionDesigns.DesktopName, SectionDesigns.PhoneName];
    private static readonly Lazy<string> Prepared = new(PrepareGoldens);

    public static TheoryData<string, string> RecordedLayouts
    {
        get
        {
            var data = new TheoryData<string, string>();

            foreach (var profile in Profiles)
            {
                foreach (var sample in FullSamples)
                {
                    data.Add(profile, sample);
                }
            }

            return data;
        }
    }

    public static TheoryData<string> ProfileNames => new(Profiles);

    [Theory]
    [MemberData(nameof(RecordedLayouts))]
    [Trait("Category", "Layout")]
    public void A_small_sample_gives_exactly_the_recorded_layout(string profile, string sample)
    {
        _ = Prepared.Value;
        var fileName = $"{profile}-{sample}.json";
        var recorded = File.ReadAllText(Path.Combine(GoldenDirectory(), fileName));

        AssertSameText(
            Normalise(recorded),
            Normalise(IndentedLayout(profile, sample) + "\n"),
            $"The layout for sample {sample} on the {profile} design differs from {fileName}");
    }

    [Theory]
    [MemberData(nameof(ProfileNames))]
    [Trait("Category", "Layout")]
    public void The_largest_sample_gives_exactly_the_recorded_digest(string profile)
    {
        _ = Prepared.Value;
        var fileName = $"{profile}-{LargeSample}.sha256";
        var recorded = File.ReadAllText(Path.Combine(GoldenDirectory(), fileName)).Trim();

        recorded.Should().Be(
            Digest(CompactLayout(profile, LargeSample)),
            $"the layout for sample {LargeSample} on the {profile} design differs from {fileName}. {ChangeAdvice}");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_recorded_layouts_belong_to_the_current_layout_version()
    {
        _ = Prepared.Value;
        var record = ReadVersionRecord();
        var digest = DigestOfEverything();

        if (digest != record.Digest && record.Version == CabinetLayoutEngine.LayoutVersion)
        {
            Assert.Fail($"The arrangement changed but CabinetLayoutEngine.LayoutVersion is still {record.Version}. {ChangeAdvice}");
        }

        record.Version.Should().Be(
            CabinetLayoutEngine.LayoutVersion,
            $"the recorded layouts were made at another layout version. {ChangeAdvice}");
        digest.Should().Be(record.Digest, $"the recorded layouts are out of date. {ChangeAdvice}");
    }

    [Fact]
    [Trait("Category", "Layout")]
    public void The_golden_directory_holds_exactly_the_recorded_files()
    {
        _ = Prepared.Value;
        var expected = Profiles
            .SelectMany(profile => FullSamples.Select(sample => $"{profile}-{sample}.json")
                .Append($"{profile}-{LargeSample}.sha256"))
            .Append(VersionFileName)
            .Order(StringComparer.Ordinal)
            .ToList();

        Directory.GetFiles(GoldenDirectory()).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            .Should().Equal(expected);
    }

    private static void AssertSameText(string recorded, string current, string what)
    {
        if (recorded == current)
        {
            return;
        }

        var recordedLines = recorded.Split('\n');
        var currentLines = current.Split('\n');
        var line = Enumerable.Range(0, Math.Min(recordedLines.Length, currentLines.Length))
            .Cast<int?>()
            .FirstOrDefault(index => recordedLines[index!.Value] != currentLines[index.Value]) ?? Math.Min(recordedLines.Length, currentLines.Length);

        Assert.Fail($"{what}, first at line {line + 1}. {ChangeAdvice}");
    }

    private static string ChangeAdvice =>
        $"If the change is intended, raise CabinetLayoutEngine.LayoutVersion and re-record with: {RerecordCommand}";

    private static string PrepareGoldens()
    {
        if (Environment.GetEnvironmentVariable(UpdateSwitch) != "1")
        {
            return string.Empty;
        }

        var directory = GoldenDirectory();
        Directory.CreateDirectory(directory);
        var digest = DigestOfEverything();
        var versionFile = Path.Combine(directory, VersionFileName);

        if (File.Exists(versionFile))
        {
            var record = ReadVersionRecord();

            if (record.Digest != digest && record.Version == CabinetLayoutEngine.LayoutVersion)
            {
                throw new InvalidOperationException(
                    $"Refusing to re-record: the arrangement changed but CabinetLayoutEngine.LayoutVersion is still {record.Version}. Raise it first, then run: {RerecordCommand}");
            }
        }

        foreach (var profile in Profiles)
        {
            foreach (var sample in FullSamples)
            {
                File.WriteAllText(Path.Combine(directory, $"{profile}-{sample}.json"), IndentedLayout(profile, sample) + "\n");
            }

            File.WriteAllText(
                Path.Combine(directory, $"{profile}-{LargeSample}.sha256"),
                Digest(CompactLayout(profile, LargeSample)) + "\n");
        }

        File.WriteAllText(
            versionFile,
            string.Create(CultureInfo.InvariantCulture, $"{VersionKey}{CabinetLayoutEngine.LayoutVersion}\n{DigestKey}{digest}\n"));

        return digest;
    }

    private static (int Version, string Digest) ReadVersionRecord()
    {
        var lines = File.ReadAllLines(Path.Combine(GoldenDirectory(), VersionFileName));
        var version = lines.Single(line => line.StartsWith(VersionKey, StringComparison.Ordinal))[VersionKey.Length..];
        var digest = lines.Single(line => line.StartsWith(DigestKey, StringComparison.Ordinal))[DigestKey.Length..];

        return (int.Parse(version, CultureInfo.InvariantCulture), digest.Trim());
    }

    private static string DigestOfEverything()
    {
        var builder = new StringBuilder();

        foreach (var profile in Profiles)
        {
            foreach (var sample in FullSamples.Append(LargeSample))
            {
                builder.Append(CompactLayout(profile, sample)).Append('\n');
            }
        }

        return Digest(builder.ToString());
    }

    private static string IndentedLayout(string profile, string sample) =>
        LayoutJson.Serialize(BuildLayout(profile, sample), indented: true);

    private static string CompactLayout(string profile, string sample) =>
        LayoutJson.Serialize(BuildLayout(profile, sample));

    private static CabinetLayout BuildLayout(string profile, string sample)
    {
        SectionDesigns.TryGet(profile, out var design).Should().BeTrue();
        SyntheticCollections.TryGetSample(sample, out var items).Should().BeTrue();

        return CabinetLayoutEngine.Build(items, design!);
    }

    private static string Digest(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Normalise(text))));

    private static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string GoldenDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var project = Path.Combine(directory.FullName, "Cabinet.UnitTests", "Cabinet.UnitTests.csproj");

            if (File.Exists(project))
            {
                return Path.Combine(directory.FullName, "Cabinet.UnitTests", "Layout", "Golden");
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Cabinet.UnitTests above the test output directory.");
    }
}
