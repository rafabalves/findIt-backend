namespace FindIt.Api.Services;

public sealed class RequestExpirationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<RequestExpirationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<RequestExpirationService>();
            var count = await service.ExpireDueAsync(DateTime.UtcNow, cancellationToken);
            if (count > 0)
            {
                logger.LogInformation("{Count} solicitações aprovadas expiraram.", count);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Falha ao expirar solicitações aprovadas.");
        }
    }
}
