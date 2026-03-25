using Microsoft.EntityFrameworkCore;
using ServerDistorApi.Contracts;
using ServerDistorApi.Model;

namespace ServerDistorApi.Services;

public class ServerServices : IServerServices
{
    private static readonly TimeSpan BootDelay = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(20);

    private readonly ServerContext _db;
    private readonly ILogger<ServerServices> _logger;
    private readonly ISystemClock _clock;

    public ServerServices(ServerContext db, ILogger<ServerServices> logger, ISystemClock clock)
    {
        _db = db;
        _logger = logger;
        _clock = clock;
    }

    public async Task<IReadOnlyList<Server>> GetServersAsync(CancellationToken ct)
    {
        return await _db.Servers.AsNoTracking().OrderBy(s => s.Id).ToListAsync(ct);
    }

    public async Task<Server> AddServerAsync(CreateServerRequest request, CancellationToken ct)
    {
        var server = new Server(request.OS, request.HardwareGB, request.CoreCPU, 0)
        {
            Status = request.IsPoweredOn ? ServerStatus.Free : ServerStatus.Off
        };

        await _db.Servers.AddAsync(server, ct);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Server {ServerId} added with status {Status}", server.Id, server.Status);
        return server;
    }

    public Task<Server?> GetServerByIdAsync(int id, CancellationToken ct)
    {
        return _db.Servers.FindAsync([id], ct).AsTask();
    }

    public async Task<IReadOnlyList<Server>> SearchFreeServersAsync(string? os, int? minHardwareGb, int? minCoreCpu, CancellationToken ct)
    {
        var query = _db.Servers.AsNoTracking().Where(s => s.Status == ServerStatus.Free || s.Status == ServerStatus.Off);

        if (!string.IsNullOrWhiteSpace(os))
        {
            query = query.Where(s => s.OS == os);
        }

        if (minHardwareGb.HasValue)
        {
            query = query.Where(s => s.HardwareGB >= minHardwareGb.Value);
        }

        if (minCoreCpu.HasValue)
        {
            query = query.Where(s => s.CoreCPU >= minCoreCpu.Value);
        }

        return await query.OrderBy(s => s.Id).ToListAsync(ct);
    }

    public async Task<RentServerResult> RentServerAsync(CreateRentalRequest request, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var server = await _db.Servers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServerId, ct);
        if (server is null)
        {
            return new RentServerResult(RentServerError.ServerNotFound, null);
        }

        if (server.Status is not (ServerStatus.Free or ServerStatus.Off))
        {
            return new RentServerResult(RentServerError.Busy, null);
        }

        var nextStatus = server.Status == ServerStatus.Off ? ServerStatus.Starting : ServerStatus.Rented;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var rows = await _db.Servers
            .Where(s => s.Id == request.ServerId && s.Status == server.Status)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, nextStatus)
                .SetProperty(s => s.StartRequestTime, nextStatus == ServerStatus.Starting ? now : null)
                .SetProperty(s => s.RentStartTime, nextStatus == ServerStatus.Rented ? now : null)
                .SetProperty(s => s.RentEndTime, nextStatus == ServerStatus.Rented ? now + LeaseDuration : null)
                .SetProperty(s => s.RentByUserId, 0), ct);

        if (rows == 0)
        {
            await tx.RollbackAsync(ct);
            return new RentServerResult(RentServerError.StateChanged, null);
        }

        var rental = new Rental
        {
            ServerId = request.ServerId,
            ClientId = request.ClientId,
            RequestedAtUtc = now,
            ReadyAfterUtc = nextStatus == ServerStatus.Starting ? now + BootDelay : now,
            Status = nextStatus == ServerStatus.Starting ? RentalStatus.PendingStart : RentalStatus.Active,
            RentedAtUtc = nextStatus == ServerStatus.Rented ? now : null,
            AutoReleaseAtUtc = nextStatus == ServerStatus.Rented ? now + LeaseDuration : null
        };

        await _db.Rentals.AddAsync(rental, ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Rental {RentalId} created for server {ServerId} in status {Status}", rental.Id, rental.ServerId, rental.Status);
        return RentServerResult.Success(rental);
    }

    public async Task<RentalReadinessResponse?> GetReadinessAsync(int rentalId, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var activated = await _db.Rentals
            .Where(r => r.Id == rentalId && r.Status == RentalStatus.PendingStart && r.ReadyAfterUtc <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, RentalStatus.Active)
                .SetProperty(r => r.RentedAtUtc, now)
                .SetProperty(r => r.AutoReleaseAtUtc, now + LeaseDuration), ct);

        if (activated > 0)
        {
            var serverId = await _db.Rentals
                .Where(r => r.Id == rentalId)
                .Select(r => r.ServerId)
                .FirstAsync(ct);

            await _db.Servers
                .Where(s => s.Id == serverId && s.Status == ServerStatus.Starting)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Status, ServerStatus.Rented)
                    .SetProperty(s => s.RentStartTime, now)
                    .SetProperty(s => s.RentEndTime, now + LeaseDuration)
                    .SetProperty(s => s.StartRequestTime, (DateTime?)null), ct);
        }

        var rental = await _db.Rentals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rentalId, ct);
        if (rental is null)
        {
            return null;
        }

        return new RentalReadinessResponse(
            rental.Id,
            rental.ServerId,
            rental.Status == RentalStatus.Active,
            rental.Status,
            rental.ReadyAfterUtc);
    }

    public async Task<ReleaseRentalResult> ReleaseRentalAsync(int rentalId, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var rental = await _db.Rentals.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rentalId, ct);
        if (rental is null)
        {
            return new ReleaseRentalResult(ReleaseRentalError.RentalNotFound, false);
        }

        if (rental.Status == RentalStatus.Released)
        {
            return new ReleaseRentalResult(ReleaseRentalError.None, true);
        }

        var changed = await _db.Rentals
            .Where(r => r.Id == rentalId && r.Status != RentalStatus.Released)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(r => r.Status, RentalStatus.Released)
                .SetProperty(r => r.ReleasedAtUtc, now), ct);

        if (changed == 0)
        {
            return new ReleaseRentalResult(ReleaseRentalError.None, true);
        }

        await _db.Servers
            .Where(s => s.Id == rental.ServerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, ServerStatus.Free)
                .SetProperty(s => s.RentStartTime, (DateTime?)null)
                .SetProperty(s => s.RentEndTime, (DateTime?)null)
                .SetProperty(s => s.StartRequestTime, (DateTime?)null)
                .SetProperty(s => s.RentByUserId, 0), ct);

        await _db.ServerLogs.AddAsync(new ServerLogs
        {
            ServerId = rental.ServerId,
            UserId = 0,
            RentStart = rental.RentedAtUtc ?? rental.RequestedAtUtc,
            RentEnd = now,
            WasAutoReleased = DateTime.MinValue
        }, ct);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Rental {RentalId} released manually", rentalId);

        return new ReleaseRentalResult(ReleaseRentalError.None, false);
    }
}
