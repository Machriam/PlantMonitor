﻿using System;
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

    public virtual DbSet<DeviceSwitchAssociation> DeviceSwitchAssociations { get; set; }

    public virtual DbSet<SwitchableOutletCode> SwitchableOutletCodes { get; set; }

    public virtual DbSet<TemperatureMeasurement> TemperatureMeasurements { get; set; }

    public virtual DbSet<TemperatureMeasurementValue> TemperatureMeasurementValues { get; set; }

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
            entity.Property(e => e.MovementPlanJson)
                .HasColumnType("jsonb")
                .HasColumnName("movement_plan_json");
            entity.Property(e => e.Name).HasColumnName("name");
        });

        modelBuilder.Entity<DeviceSwitchAssociation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("device_switch_association_pkey");

            entity.ToTable("device_switch_association", "plantmonitor");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.OutletOffFk).HasColumnName("outlet_off_fk");
            entity.Property(e => e.OutletOnFk).HasColumnName("outlet_on_fk");

            entity.HasOne(d => d.OutletOffFkNavigation).WithMany(p => p.DeviceSwitchAssociationOutletOffFkNavigations)
                .HasForeignKey(d => d.OutletOffFk)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("device_switch_association_outlet_off_fk_fkey");

            entity.HasOne(d => d.OutletOnFkNavigation).WithMany(p => p.DeviceSwitchAssociationOutletOnFkNavigations)
                .HasForeignKey(d => d.OutletOnFk)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("device_switch_association_outlet_on_fk_fkey");
        });

        modelBuilder.Entity<SwitchableOutletCode>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("switchable_outlet_code_pkey");

            entity.ToTable("switchable_outlet_code", "plantmonitor");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ChannelBaseNumber).HasColumnName("channel_base_number");
            entity.Property(e => e.ChannelNumber).HasColumnName("channel_number");
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.OutletName).HasColumnName("outlet_name");
            entity.Property(e => e.TurnsOn).HasColumnName("turns_on");
        });

        modelBuilder.Entity<TemperatureMeasurement>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("temperature_measurement_pkey");

            entity.ToTable("temperature_measurement", "plantmonitor");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Comment).HasColumnName("comment");
            entity.Property(e => e.DeviceId).HasColumnName("device_id");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
        });

        modelBuilder.Entity<TemperatureMeasurementValue>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("temperature_measurement_value_pkey");

            entity.ToTable("temperature_measurement_value", "plantmonitor");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.MeasurementFk).HasColumnName("measurement_fk");
            entity.Property(e => e.Temperature).HasColumnName("temperature");
            entity.Property(e => e.Timestamp).HasColumnName("timestamp");

            entity.HasOne(d => d.MeasurementFkNavigation).WithMany(p => p.TemperatureMeasurementValues)
                .HasForeignKey(d => d.MeasurementFk)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("temperature_measurement_value_measurement_fk_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    private partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
