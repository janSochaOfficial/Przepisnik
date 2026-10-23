using RecipesList.Infrastructure.Storage;

namespace RecipesList.Api.Features.Images;

/// <summary>Runs <see cref="OrphanImageCleaner"/> hourly (TECHNICAL §3.7).</summary>
internal sealed partial class OrphanImageCleanupService(IServiceScopeFactory scopes, TimeProvider time, ILogger<OrphanImageCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Period = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Period, time);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    /// <summary>One cleanup pass; failures are logged and retried on the next tick.</summary>
    internal async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OrphanImageCleaner>().RunAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogFailed(logger, e);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Orphan image cleanup failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
