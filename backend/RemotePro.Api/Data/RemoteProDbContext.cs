using Microsoft.EntityFrameworkCore;
using RemotePro.Api.Models;

namespace RemotePro.Api.Data;

public sealed class RemoteProDbContext : DbContext
{
    public RemoteProDbContext(
        DbContextOptions<RemoteProDbContext> options)
        : base(options)
    {
    }

    public DbSet<Device> Devices => Set<Device>();

    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ==========================================
        // DEVICE CONFIGURATION
        // ==========================================

        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices");

            entity.HasKey(device => device.Id);

            entity.Property(device => device.DeviceName)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(device => device.Hostname)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(device => device.OperatingSystem)
                .HasMaxLength(150)
                .IsRequired();

            entity.Property(device => device.IpAddress)
                .HasMaxLength(45)
                .IsRequired();

            entity.Property(device => device.Status)
                .HasMaxLength(30)
                .IsRequired();

            entity.HasIndex(device => device.Hostname)
                .HasDatabaseName("IX_devices_Hostname_CaseInsensitive")
                .IsUnique();
        });

        // ==========================================
        // DEVICE CREDENTIAL CONFIGURATION
        // ==========================================

        modelBuilder.Entity<DeviceCredential>(entity =>
        {
            entity.ToTable("device_credentials");

            entity.HasKey(credential => credential.Id);

            entity.Property(credential => credential.TokenHash)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(credential => credential.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(credential => credential.TokenHash)
                .IsUnique();

            entity.HasIndex(credential => credential.DeviceId);

            entity.HasOne(credential => credential.Device)
                .WithMany()
                .HasForeignKey(credential => credential.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
