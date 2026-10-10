using System.Diagnostics;
using System.Net;
using Cabinet.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace Cabinet.IntegrationTests;

/// <summary>
/// Runs its tests one at a time after every parallel test has finished, because they count the open handles of the whole
/// test process and a host another test starts at the same moment would change that count.
/// </summary>
[CollectionDefinition(nameof(WholeProcessCountCollection), DisableParallelization = true)]
public sealed class WholeProcessCountCollection;

/// <summary>
/// Verifies a disposed test factory lets go of the file watchers its pages started, on the socket host and on the
/// in-memory host alike. On Linux each watcher holds an inotify instance, the system allows only a few hundred per user,
/// and a full test run starts new hosts for every test.
/// </summary>
[Collection(nameof(WholeProcessCountCollection))]
public class TestHostCleanupTests
{
    private const string ProcessHandleDirectory = "/proc/self/fd";

    private static readonly TimeSpan ReleaseTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task A_disposed_host_lets_go_of_every_inotify_instance_its_page_started()
    {
        Assert.SkipUnless(Directory.Exists(ProcessHandleDirectory), "only Linux lists the open inotify instances of a process");
        var before = CountInotifyInstances();

        await using (var factory = new CabinetWebApplicationFactory())
        {
            using var socketClient = factory.CreatePublicClient();
            using var inMemoryClient = factory.CreateClient();
            using var socketResponse = await socketClient.GetAsync("/", TestContext.Current.CancellationToken);
            using var inMemoryResponse = await inMemoryClient.GetAsync("/", TestContext.Current.CancellationToken);

            socketResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            inMemoryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            CountInotifyInstances().Should().BeGreaterThan(before);
        }

        (await WaitForInotifyCountAsync(before)).Should().Be(before);
    }

    /// <summary>
    /// Waits a few seconds at most for the count to drop back, because a file watcher closes its inotify instance on its
    /// own thread shortly after it is disposed.
    /// </summary>
    private static async Task<int> WaitForInotifyCountAsync(int expected)
    {
        var stopwatch = Stopwatch.StartNew();
        var count = CountInotifyInstances();
        while (count != expected && stopwatch.Elapsed < ReleaseTimeout)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
            count = CountInotifyInstances();
        }

        return count;
    }

    private static int CountInotifyInstances() =>
        new DirectoryInfo(ProcessHandleDirectory).EnumerateFileSystemInfos().Count(IsInotifyInstance);

    private static bool IsInotifyInstance(FileSystemInfo handle)
    {
        try
        {
            return handle.LinkTarget?.Contains("inotify", StringComparison.Ordinal) == true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
