using Microsoft.EntityFrameworkCore;

namespace ServerDistorApi.Model;

public class ServerContext : DbContext
{
    public ServerContext(DbContextOptions<ServerContext> options) : base(options)
    {
    }

    public DbSet<Server> Servers { get; set; }
    public DbSet<ServerLogs> ServerLogs { get; set; }
    public DbSet<Rental> Rentals { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Server>(entity =>
        {
            entity.Property(x => x.OS).IsRequired();
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<Rental>(entity =>
        {
            entity.Property(x => x.ClientId).IsRequired();
            entity.HasIndex(x => x.ServerId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ReadyAfterUtc);
            entity.HasIndex(x => x.AutoReleaseAtUtc);
        });

        modelBuilder.Entity<ServerLogs>(entity =>
        {
            entity.HasIndex(x => x.ServerId);
            entity.HasIndex(x => x.RentEnd);
        });
    }
}