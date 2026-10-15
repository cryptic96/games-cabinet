using System.Diagnostics.Metrics;

namespace Cabinet.IntegrationTests.Infrastructure;

/// <summary>
/// Counts the upgraded connections, which are the open WebSockets, that Kestrel holds on one loopback port, from the server's own
/// metric. The live channel's hub frees its place a moment before Kestrel frees the upgrade it counts against its own cap, so a
/// test that reconnects the moment the hub's place is free can be refused an upgrade by a slot that is still held. Waiting for this
/// count makes that moment certain instead of timing-dependent.
/// </summary>
public sealed class KestrelUpgradeWatch : IDisposable
{
    private const string MeterName = "Microsoft.AspNetCore.Server.Kestrel";
    private const string InstrumentName = "kestrel.upgraded_connections";
    private const string PortTag = "server.port";

    private readonly MeterListener _listener = new();
    private long _open;

    /// <summary>Starts counting upgrades on the given port. Create it after the host has started and before any page connects.</summary>
    /// <param name="port">The loopback port the host listens on.</param>
    public KestrelUpgradeWatch(int port)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == MeterName && instrument.Name == InstrumentName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == PortTag && tag.Value is int measured && measured == port)
                {
                    Interlocked.Add(ref _open, measurement);
                }
            }
        });
        _listener.Start();
    }

    /// <summary>How many upgraded connections Kestrel holds on the port now.</summary>
    public long Open => Volatile.Read(ref _open);

    /// <summary>Waits until Kestrel holds exactly the given number of upgraded connections.</summary>
    /// <param name="expected">The number of upgraded connections to wait for.</param>
    public Task WaitForOpenAsync(long expected) => SyncHarness.WaitUntil(() => Task.FromResult(Open == expected));

    /// <inheritdoc />
    public void Dispose() => _listener.Dispose();
}
