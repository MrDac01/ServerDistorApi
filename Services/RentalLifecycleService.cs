using Microsoft.EntityFrameworkCore;
using ServerDistorApi.Model;

namespace ServerDistorApi.Services;

public class RentalLifecycleService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(20);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RentalLifecycleService> _logger;
    private readonly ISystemClock _clock;

    public RentalLifecycleService(IServiceScopeFactory scopeFactory, ILogger<RentalLifecycleService> logger, ISystemClock clock)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessLifecycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rental lifecycle iteration failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public Task RunIterationForTestsAsync(CancellationToken ct) => ProcessLifecycleAsync(ct);

    private async Task ProcessLifecycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerContext>();
        var now = _clock.UtcNow;

        var rentalsToActivate = await db.Rentals
            .AsNoTracking()
            .Where(r => r.Status == RentalStatus.PendingStart && r.ReadyAfterUtc <= now)
            .ToListAsync(ct);

        foreach (var rental in rentalsToActivate)
        {
            var activated = await db.Rentals
                .Where(r => r.Id == rental.Id && r.Status == RentalStatus.PendingStart)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, RentalStatus.Active)
                    .SetProperty(r => r.RentedAtUtc, now)
                    .SetProperty(r => r.AutoReleaseAtUtc, now + LeaseDuration), ct);

            if (activated == 0)
            {
                continue;
            }

            await db.Servers
                .Where(s => s.Id == rental.ServerId && s.Status == ServerStatus.Starting)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Status, ServerStatus.Rented)
                    .SetProperty(s => s.RentStartTime, now)
                    .SetProperty(s => s.RentEndTime, now + LeaseDuration)
                    .SetProperty(s => s.StartRequestTime, (DateTime?)null), ct);

            _logger.LogInformation("Rental {RentalId} became active", rental.Id);
        }

        var rentalsToRelease = await db.Rentals
            .AsNoTracking()
            .Where(r => r.Status == RentalStatus.Active && r.AutoReleaseAtUtc != null && r.AutoReleaseAtUtc <= now)
            .ToListAsync(ct);

        foreach (var rental in rentalsToRelease)
        {
            var released = await db.Rentals
                .Where(r => r.Id == rental.Id && r.Status == RentalStatus.Active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(r => r.Status, RentalStatus.Released)
                    .SetProperty(r => r.ReleasedAtUtc, now), ct);

            if (released == 0)
            {
                continue;
            }

            await db.Servers
                .Where(s => s.Id == rental.ServerId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Status, ServerStatus.Free)
                    .SetProperty(s => s.RentStartTime, (DateTime?)null)
                    .SetProperty(s => s.RentEndTime, (DateTime?)null)
                    .SetProperty(s => s.StartRequestTime, (DateTime?)null)
                    .SetProperty(s => s.RentByUserId, 0), ct);

            await db.ServerLogs.AddAsync(new ServerLogs
            {
                ServerId = rental.ServerId,
                UserId = 0,
                RentStart = rental.RentedAtUtc ?? rental.RequestedAtUtc,
                RentEnd = now,
                WasAutoReleased = now
            }, ct);

            _logger.LogInformation("Rental {RentalId} auto released", rental.Id);
        }

        if (rentalsToRelease.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
