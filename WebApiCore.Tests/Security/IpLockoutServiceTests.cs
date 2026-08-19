using WebApiCore.Infrastructure.Security;

namespace WebApiCore.Tests.Security;

public class IpLockoutServiceTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static IpLockoutOptions DefaultOptions() => new()
    {
        MaxFailures = 5,
        FailureWindow = TimeSpan.FromMinutes(10),
        BlockDuration = TimeSpan.FromHours(1)
    };

    [Fact]
    public void IsBlocked_WithNoFailures_ReturnsFalse()
    {
        var clock = new FakeTimeProvider(BaseTime);
        var service = new IpLockoutService(DefaultOptions(), clock);

        var isBlocked = service.IsBlocked("10.0.0.1");

        Assert.False(isBlocked);
    }

    [Fact]
    public void IsBlocked_WithFewerThanMaxFailures_ReturnsFalse()
    {
        var clock = new FakeTimeProvider(BaseTime);
        var service = new IpLockoutService(DefaultOptions(), clock);

        for (var i = 0; i < 4; i++)
            service.RegisterFailure("10.0.0.1");

        Assert.False(service.IsBlocked("10.0.0.1"));
        Assert.Null(service.GetRemainingBlockTime("10.0.0.1"));
    }

    [Fact]
    public void RegisterFailure_ReachingMaxFailures_BlocksForOneHour()
    {
        var clock = new FakeTimeProvider(BaseTime);
        var service = new IpLockoutService(DefaultOptions(), clock);

        for (var i = 0; i < 5; i++)
            service.RegisterFailure("10.0.0.1");

        Assert.True(service.IsBlocked("10.0.0.1"));

        var remaining = service.GetRemainingBlockTime("10.0.0.1");
        Assert.NotNull(remaining);
        Assert.Equal(TimeSpan.FromHours(1), remaining.Value);
    }

    [Fact]
    public void GetRemainingBlockTime_WhenNotBlocked_ReturnsNull()
    {
        var clock = new FakeTimeProvider(BaseTime);
        var service = new IpLockoutService(DefaultOptions(), clock);

        var remaining = service.GetRemainingBlockTime("10.0.0.1");

        Assert.Null(remaining);
    }

    [Fact]
    public void IsBlocked_AfterBlockDuration_ReturnsFalse()
    {
        var clock = new FakeTimeProvider(BaseTime);
        var service = new IpLockoutService(DefaultOptions(), clock);

        for (var i = 0; i < 5; i++)
            service.RegisterFailure("10.0.0.1");
        Assert.True(service.IsBlocked("10.0.0.1"));

        clock.Advance(TimeSpan.FromHours(1));

        Assert.False(service.IsBlocked("10.0.0.1"));
        Assert.Null(service.GetRemainingBlockTime("10.0.0.1"));
    }

    [Fact]
    public void RegisterFailure_OutsideFailureWindow_ResetsCounter()
    {
        var clock = new FakeTimeProvider(BaseTime);
        var service = new IpLockoutService(DefaultOptions(), clock);

        for (var i = 0; i < 4; i++)
            service.RegisterFailure("10.0.0.1");

        clock.Advance(TimeSpan.FromMinutes(10).Add(TimeSpan.FromSeconds(1)));
        service.RegisterFailure("10.0.0.1");

        Assert.False(service.IsBlocked("10.0.0.1"));
    }

    [Fact]
    public void RegisterFailure_AcrossFailuresInsideWindow_Accumulate()
    {
        var clock = new FakeTimeProvider(BaseTime);
        var service = new IpLockoutService(DefaultOptions(), clock);

        for (var i = 0; i < 3; i++)
            service.RegisterFailure("10.0.0.1");

        clock.Advance(TimeSpan.FromMinutes(5));
        service.RegisterFailure("10.0.0.1");
        service.RegisterFailure("10.0.0.1");

        Assert.True(service.IsBlocked("10.0.0.1"));
    }

    [Fact]
    public void Reset_ClearsFailuresAndBlock()
    {
        var clock = new FakeTimeProvider(BaseTime);
        var service = new IpLockoutService(DefaultOptions(), clock);

        for (var i = 0; i < 5; i++)
            service.RegisterFailure("10.0.0.1");
        Assert.True(service.IsBlocked("10.0.0.1"));

        service.Reset("10.0.0.1");

        Assert.False(service.IsBlocked("10.0.0.1"));
        Assert.Null(service.GetRemainingBlockTime("10.0.0.1"));
    }

    [Fact]
    public void Failures_TrackedPerIpAddress()
    {
        var clock = new FakeTimeProvider(BaseTime);
        var service = new IpLockoutService(DefaultOptions(), clock);

        for (var i = 0; i < 5; i++)
            service.RegisterFailure("10.0.0.1");

        Assert.True(service.IsBlocked("10.0.0.1"));
        Assert.False(service.IsBlocked("10.0.0.2"));
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset start) => _utcNow = start;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }
}