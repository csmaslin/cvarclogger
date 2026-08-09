using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Data.Models;

public partial class CvarcloggerContext : DbContext
{
    public CvarcloggerContext()
    {
    }

    public CvarcloggerContext(DbContextOptions<CvarcloggerContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DxccEntity> DxccEntities { get; set; }

    public virtual DbSet<EfmigrationsLock> EfmigrationsLocks { get; set; }

    public virtual DbSet<PrefixMapping> PrefixMappings { get; set; }

    public virtual DbSet<Qso> Qsos { get; set; }

    public virtual DbSet<StationProfile> StationProfiles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlite("Data Source=C:\\Users\\user\\AppData\\Local\\CVARC Logger\\cvarclogger.db");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DxccEntity>(entity =>
        {
            entity.HasKey(e => e.EntityCode);
        });

        modelBuilder.Entity<EfmigrationsLock>(entity =>
        {
            entity.ToTable("__EFMigrationsLock");

            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<PrefixMapping>(entity =>
        {
            entity.HasIndex(e => e.DxccEntityCode, "IX_PrefixMappings_DxccEntityCode");

            entity.HasIndex(e => e.Prefix, "IX_PrefixMappings_Prefix").IsUnique();

            entity.HasOne(d => d.DxccEntityCodeNavigation).WithMany(p => p.PrefixMappings).HasForeignKey(d => d.DxccEntityCode);
        });

        modelBuilder.Entity<Qso>(entity =>
        {
            entity.HasIndex(e => e.Callsign, "IX_Qsos_Callsign");

            entity.HasIndex(e => e.DxccEntityCode, "IX_Qsos_DxccEntityCode");

            entity.HasIndex(e => e.QsoDateTimeOnUtc, "IX_Qsos_QsoDateTimeOnUtc");

            entity.HasIndex(e => e.StationProfileId, "IX_Qsos_StationProfileId");

            entity.HasOne(d => d.DxccEntityCodeNavigation).WithMany(p => p.Qsos)
                .HasForeignKey(d => d.DxccEntityCode)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.StationProfile).WithMany(p => p.Qsos)
                .HasForeignKey(d => d.StationProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
