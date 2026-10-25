namespace RecipesList.IntegrationTests.Infrastructure;

/// <summary>Forwards to whichever clock is current, so a test can swap in a fake one on a running app.</summary>
internal sealed class SwitchableTimeProvider(Func<TimeProvider> current) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => current().GetUtcNow();

    public override long GetTimestamp() => current().GetTimestamp();

    public override long TimestampFrequency => current().TimestampFrequency;

    public override TimeZoneInfo LocalTimeZone => current().LocalTimeZone;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        System.CreateTimer(callback, state, dueTime, period);
}
