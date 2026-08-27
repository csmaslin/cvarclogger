using CvarcLogger.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace CvarcLogger.Data;

public class CvarcLoggerDbContext : DbContext
{
    public DbSet<Qso> Qsos => Set<Qso>();
    public DbSet<StationProfile> StationProfiles => Set<StationProfile>();
    public DbSet<DxccEntity> DxccEntities => Set<DxccEntity>();
    public DbSet<PrefixMapping> PrefixMappings => Set<PrefixMapping>();
    public DbSet<SotaActivation> SotaActivations => Set<SotaActivation>();
    public DbSet<PotaActivation> PotaActivations => Set<PotaActivation>();
    public DbSet<ContestSubmission> ContestSubmissions => Set<ContestSubmission>();
    public DbSet<SkccMember> SkccMembers => Set<SkccMember>();

    public CvarcLoggerDbContext(DbContextOptions<CvarcLoggerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Qso>(entity =>
        {
            entity.HasKey(q => q.Id);
            entity.Property(q => q.Callsign).IsRequired().HasMaxLength(20);
            entity.Property(q => q.StationCallsign).IsRequired().HasMaxLength(20);
            entity.Property(q => q.Band).IsRequired().HasMaxLength(10);
            entity.Property(q => q.Mode).IsRequired().HasMaxLength(20);

            entity.Property(q => q.QslSent).HasConversion<string>().HasMaxLength(20);
            entity.Property(q => q.QslRcvd).HasConversion<string>().HasMaxLength(20);
            entity.Property(q => q.LotwQslSent).HasConversion<string>().HasMaxLength(20);
            entity.Property(q => q.LotwQslRcvd).HasConversion<string>().HasMaxLength(20);

            entity.Property(q => q.Precedence).HasMaxLength(1);
            entity.Property(q => q.Check).HasMaxLength(2);
            entity.Property(q => q.Class).HasMaxLength(4);
            entity.Property(q => q.SkccNr).HasMaxLength(10);
            entity.Property(q => q.MySkccNr).HasMaxLength(10);

            entity.HasOne(q => q.DxccEntity)
                .WithMany()
                .HasForeignKey(q => q.DxccEntityCode)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(q => q.StationProfile)
                .WithMany()
                .HasForeignKey(q => q.StationProfileId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(q => q.Callsign);
            entity.HasIndex(q => q.QsoDateTimeOnUtc);
        });

        modelBuilder.Entity<StationProfile>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Callsign).IsRequired().HasMaxLength(20);
            entity.Property(s => s.SkccNr).HasMaxLength(10);
        });

        modelBuilder.Entity<DxccEntity>(entity =>
        {
            entity.HasKey(d => d.EntityCode);
            entity.Property(d => d.EntityCode).ValueGeneratedNever();
            entity.Property(d => d.EntityName).IsRequired().HasMaxLength(100);

            entity.HasMany(d => d.Prefixes)
                .WithOne(p => p.DxccEntity)
                .HasForeignKey(p => p.DxccEntityCode)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PrefixMapping>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Prefix).IsRequired().HasMaxLength(10);
            entity.HasIndex(p => p.Prefix).IsUnique();
        });

        modelBuilder.Entity<SotaActivation>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.SummitCode).IsRequired().HasMaxLength(20);
            entity.Property(s => s.SummitName).HasMaxLength(100);
            entity.Ignore(s => s.ContactCount);
        });

        modelBuilder.Entity<PotaActivation>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.ParkReference).IsRequired().HasMaxLength(20);
            entity.Property(p => p.ParkName).HasMaxLength(150);
        });

        modelBuilder.Entity<ContestSubmission>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.ContestId).IsRequired().HasMaxLength(40);
            entity.Property(c => c.Callsign).IsRequired().HasMaxLength(20);
            entity.Property(c => c.CategoryOperator).HasMaxLength(40);
            entity.Property(c => c.CategoryAssisted).HasMaxLength(40);
            entity.Property(c => c.CategoryBand).HasMaxLength(40);
            entity.Property(c => c.CategoryMode).HasMaxLength(40);
            entity.Property(c => c.CategoryPower).HasMaxLength(40);
            entity.Property(c => c.CategoryStation).HasMaxLength(40);
            entity.Property(c => c.CategoryTransmitter).HasMaxLength(40);
            entity.Property(c => c.CategoryOverlay).HasMaxLength(40);
            entity.Property(c => c.ClaimedScore).HasMaxLength(20);
            entity.Property(c => c.Club).HasMaxLength(100);
            entity.Property(c => c.Location).HasMaxLength(20);
            entity.Property(c => c.Name).HasMaxLength(100);
            entity.Property(c => c.Address).HasMaxLength(200);
            entity.Property(c => c.AddressCity).HasMaxLength(100);
            entity.Property(c => c.AddressStateProvince).HasMaxLength(40);
            entity.Property(c => c.AddressPostalCode).HasMaxLength(20);
            entity.Property(c => c.AddressCountry).HasMaxLength(60);
            entity.Property(c => c.Operators).HasMaxLength(500);
            entity.Property(c => c.Email).HasMaxLength(100);
            entity.HasIndex(c => c.ContestId);
            entity.HasIndex(c => c.ModifiedAtUtc);
        });
    }
}
