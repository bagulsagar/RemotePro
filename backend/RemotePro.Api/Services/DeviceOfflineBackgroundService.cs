using Microsoft.EntityFrameworkCore;
using RemotePro.Api.Data;

namespace RemotePro.Api.Services;

public sealed class DeviceOfflineBackgroundService : BackgroundService
{
    private static readonly TimeSpan CheckInterval =
        TimeSpan.FromSeconds(15);

    private static readonly TimeSpan OfflineThreshold =
        TimeSpan.FromSeconds(90);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviceOfflineBackgroundService> _logger;

    public DeviceOfflineBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<DeviceOfflineBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Device offline detection service started.");

        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await MarkStaleDevicesOfflineAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }

    private async Task MarkStaleDevicesOfflineAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<RemoteProDbContext>();

            var cutoffUtc = DateTime.UtcNow - OfflineThreshold;

            var staleDevices = await db.Devices
                .Where(device =>
                    device.Status == "Online" &&
                    (device.LastSeenAtUtc == null ||
                     device.LastSeenAtUtc < cutoffUtc))
                .ToListAsync(cancellationToken);

            if (staleDevices.Count == 0)
            {
                return;
            }

            foreach (var device in staleDevices)
            {
                device.Status = "Offline";
            }

            await db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Marked {DeviceCount} stale devices Offline.",
                staleDevices.Count);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to update stale device statuses.");
        }
    }
}
