using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Plantmonitor.DataModel.DataModel;

public partial class DataContext : DbContext
{
    public DataContext(DbContextOptions<DataContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ConfigurationDatum> ConfigurationData { get; set; }

    public virtual DbSet<DeviceMovement> DeviceMovements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConfigurationDatum>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("configuration_data_pkey");

            entity.ToTable("configuration_data", "plantmonitor");

            entity.HasIndex(e => e.Key, "configuration_data_key_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Key).HasColumnName("key");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<DeviceMovement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("device_movement_pkey");

            entity.ToTable("device_movement", "plantmonitor");

            entity.HasIndex(e => new { e.DeviceId, e.Name }, "device_movement_device_id_name_key").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.MovementPlan)
                .HasColumnType("jsonb")
                .HasColumnName("movement_plan");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    private partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
