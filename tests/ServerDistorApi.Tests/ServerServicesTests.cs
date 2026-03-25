using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ServerDistorApi.Contracts;
using ServerDistorApi.Model;
using ServerDistorApi.Services;
using Xunit;

namespace ServerDistorApi.Tests;

public class ServerServicesTests
{
    [Fact]
    public async Task Rent_Free_Server_Is_Active_Immediately()
    {
        var dbPath = NewDbPath();
        try
        {
            var clock = new FakeClock(new DateTime(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc));

            await using var seedContext = CreateContext(dbPath);
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.Servers.Add(new Server("linux", 32, 8, 0) { Status = ServerStatus.Free });
            await seedContext.SaveChangesAsync();

            await using var context = CreateContext(dbPath);
            var service = new ServerServices(context, NullLogger<ServerServices>.Instance, clock);

            var result = await service.RentServerAsync(new CreateRentalRequest(1, "client-a"), CancellationToken.None);

            Assert.Equal(RentServerError.None, result.Error);
            Assert.NotNull(result.Rental);
            Assert.Equal(RentalStatus.Active, result.Rental!.Status);
            Assert.NotNull(result.Rental.AutoReleaseAtUtc);
        }
        finally
        {
            TryDeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task Rent_Off_Server_Becomes_Ready_After_5_Minutes()
    {
        var dbPath = NewDbPath();
        try
        {
            var clock = new FakeClock(new DateTime(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc));

            await using var seedContext = CreateContext(dbPath);
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.Servers.Add(new Server("linux", 64, 16, 0) { Status = ServerStatus.Off });
            await seedContext.SaveChangesAsync();

            await using var context = CreateContext(dbPath);
            var service = new ServerServices(context, NullLogger<ServerServices>.Instance, clock);

            var rentResult = await service.RentServerAsync(new CreateRentalRequest(1, "client-a"), CancellationToken.None);
            Assert.Equal(RentalStatus.PendingStart, rentResult.Rental!.Status);

            var notReady = await service.GetReadinessAsync(rentResult.Rental.Id, CancellationToken.None);
            Assert.False(notReady!.IsReady);

            clock.Advance(TimeSpan.FromMinutes(5));
            var ready = await service.GetReadinessAsync(rentResult.Rental.Id, CancellationToken.None);

            Assert.True(ready!.IsReady);
            Assert.Equal(RentalStatus.Active, ready.Status);
        }
        finally
        {
            TryDeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task Concurrent_Rent_On_Same_Server_Allows_Only_One_Success()
    {
        var dbPath = NewDbPath();
        try
        {
            var clock = new FakeClock(new DateTime(2026, 3, 25, 12, 0, 0, DateTimeKind.Utc));

            await using var seedContext = CreateContext(dbPath);
            await seedContext.Database.EnsureCreatedAsync();
            seedContext.Servers.Add(new Server("linux", 32, 8, 0) { Status = ServerStatus.Free });
            await seedContext.SaveChangesAsync();

            await using var context1 = CreateContext(dbPath);
            await using var context2 = CreateContext(dbPath);

            var service1 = new ServerServices(context1, NullLogger<ServerServices>.Instance, clock);
            var service2 = new ServerServices(context2, NullLogger<ServerServices>.Instance, clock);

            var t1 = service1.RentServerAsync(new CreateRentalRequest(1, "client-1"), CancellationToken.None);
            var t2 = service2.RentServerAsync(new CreateRentalRequest(1, "client-2"), CancellationToken.None);

            var results = await Task.WhenAll(t1, t2);
            var successCount = results.Count(r => r.Error == RentServerError.None);

            Assert.Equal(1, successCount);
            Assert.Equal(1, results.Count(r => r.Error != RentServerError.None));
        }
        finally
        {
            TryDeleteDb(dbPath);
        }
    }

    [Fact]
    public async Task Lifecycle_Service_Auto_Releases_Expired_Rental()
    {
        var dbPath = NewDbPath();
        try
        {
            var now = new DateTime(2026, 3, 25, 12, 40, 0, DateTimeKind.Utc);
            var clock = new FakeClock(now);

            await using var seedContext = CreateContext(dbPath);
            await seedContext.Database.EnsureCreatedAsync();

            seedContext.Servers.Add(new Server("linux", 32, 8, 0)
            {
                Id = 1,
                Status = ServerStatus.Rented,
                RentStartTime = now.AddMinutes(-20),
                RentEndTime = now.AddMinutes(-1)
            });

            seedContext.Rentals.Add(new Rental
            {
                Id = 1,
                ServerId = 1,
                ClientId = "client-a",
                Status = RentalStatus.Active,
                RequestedAtUtc = now.AddMinutes(-30),
                ReadyAfterUtc = now.AddMinutes(-25),
                RentedAtUtc = now.AddMinutes(-20),
                AutoReleaseAtUtc = now.AddSeconds(-1)
            });

            await seedContext.SaveChangesAsync();

            var services = new ServiceCollection();
            services.AddSingleton<ISystemClock>(clock);
            services.AddLogging();
            services.AddDbContext<ServerContext>(options => options.UseSqlite($"Data Source={dbPath}"));
            using var provider = services.BuildServiceProvider();

            var lifecycle = new RentalLifecycleService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<RentalLifecycleService>.Instance,
                clock);

            await lifecycle.RunIterationForTestsAsync(CancellationToken.None);

            await using var verifyContext = CreateContext(dbPath);
            var server = await verifyContext.Servers.FirstAsync();
            var rental = await verifyContext.Rentals.FirstAsync();
            var log = await verifyContext.ServerLogs.FirstOrDefaultAsync();

            Assert.Equal(ServerStatus.Free, server.Status);
            Assert.Equal(RentalStatus.Released, rental.Status);
            Assert.NotNull(log);
            Assert.NotEqual(DateTime.MinValue, log!.WasAutoReleased);
        }
        finally
        {
            TryDeleteDb(dbPath);
        }
    }

    private static ServerContext CreateContext(string dbPath)
    {
        var options = new DbContextOptionsBuilder<ServerContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new ServerContext(options);
    }

    private static string NewDbPath() => Path.Combine(Path.GetTempPath(), $"serverdistor-tests-{Guid.NewGuid():N}.db");

    private static void TryDeleteDb(string dbPath)
    {
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
        }
    }

    private sealed class FakeClock : ISystemClock
    {
        public FakeClock(DateTime nowUtc)
        {
            UtcNow = nowUtc;
        }

        public DateTime UtcNow { get; private set; }

        public void Advance(TimeSpan delta)
        {
            UtcNow = UtcNow.Add(delta);
        }
    }
}

