using Cabinet.Domain.Collection;
using Cabinet.Domain.Owner;
using FluentAssertions;

namespace Cabinet.UnitTests.Owner;

/// <summary>
/// Proves the one rule that says when a linked picture counts as dead: two failed checks that are at least a day apart, and
/// one success wipes the record of failures.
/// </summary>
[Trait("Category", "Owner")]
public sealed class LinkHealthTests
{
    private const string Address = "https://cf.example.org/owner/linked.png";

    private static readonly DateTimeOffset Now = new(2030, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_link_with_no_failures_is_not_dead()
    {
        LinkHealth.IsDead(Link(0, null), Now).Should().BeFalse();
    }

    [Fact]
    public void A_link_with_one_failure_is_not_dead_however_old_the_failure_is()
    {
        LinkHealth.IsDead(Link(1, Now.AddDays(-30)), Now).Should().BeFalse();
    }

    [Fact]
    public void A_link_with_two_failures_less_than_a_day_apart_is_not_dead()
    {
        LinkHealth.IsDead(Link(2, Now - LinkHealth.DeadAfterSpan + TimeSpan.FromSeconds(1)), Now).Should().BeFalse();
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(2, 5)]
    [InlineData(9, 1)]
    public void A_link_with_two_or_more_failures_at_least_a_day_apart_is_dead(int failures, int days)
    {
        LinkHealth.IsDead(Link(failures, Now.AddDays(-days)), Now).Should().BeTrue();
    }

    [Fact]
    public void The_rule_is_two_failed_checks_and_one_day()
    {
        LinkHealth.DeadAfterFailures.Should().Be(2);
        LinkHealth.DeadAfterSpan.Should().Be(TimeSpan.FromDays(1));
    }

    [Fact]
    public void A_failure_counts_one_and_remembers_when_the_failures_began()
    {
        var failed = LinkHealth.AfterFailure(Link(0, null), Now);

        failed.FailedChecks.Should().Be(1);
        failed.FailedSinceUtc.Should().Be(Now);
        failed.Url.Should().Be(Address);
    }

    [Fact]
    public void A_later_failure_counts_again_but_keeps_the_moment_the_failures_began()
    {
        var first = LinkHealth.AfterFailure(Link(0, null), Now);

        var second = LinkHealth.AfterFailure(first, Now.AddDays(2));

        second.FailedChecks.Should().Be(2);
        second.FailedSinceUtc.Should().Be(Now);
        LinkHealth.IsDead(second, Now.AddDays(2)).Should().BeTrue();
    }

    [Fact]
    public void A_success_stores_the_record_and_wipes_the_failures()
    {
        var record = new ImageRecord(Address, ImageStatus.Ok, Now);
        var failing = Link(5, Now.AddDays(-3));

        var healthy = LinkHealth.AfterSuccess(failing, record);

        healthy.Record.Should().Be(record);
        healthy.FailedChecks.Should().Be(0);
        healthy.FailedSinceUtc.Should().BeNull();
        healthy.Url.Should().Be(Address);
        LinkHealth.IsDead(healthy, Now).Should().BeFalse();
    }

    private static PictureLink Link(int failedChecks, DateTimeOffset? failedSince) => new(Address, null, failedChecks, failedSince);
}
