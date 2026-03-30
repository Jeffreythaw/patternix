using Microsoft.EntityFrameworkCore;
using Patternix.Api.Domain;

namespace Patternix.Api.Data;

public sealed class PatternixDbContext : DbContext
{
    public PatternixDbContext(DbContextOptions<PatternixDbContext> options) : base(options)
    {
    }

    public DbSet<Dataset> Datasets => Set<Dataset>();
    public DbSet<DatasetRow> DatasetRows => Set<DatasetRow>();
    public DbSet<TheoryDefinition> Theories => Set<TheoryDefinition>();
    public DbSet<TheoryRun> TheoryRuns => Set<TheoryRun>();
    public DbSet<TheoryResult> TheoryResults => Set<TheoryResult>();
    public DbSet<CandidateSolution> CandidateSolutions => Set<CandidateSolution>();
    public DbSet<SolverLogEntry> SolverLogs => Set<SolverLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        const string prefix = "4D_table_";

        modelBuilder.Entity<Dataset>(entity =>
        {
            entity.ToTable($"{prefix}Datasets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.SourceText).HasColumnType("nvarchar(max)");
            entity.HasMany(x => x.Rows)
                .WithOne(x => x.Dataset!)
                .HasForeignKey(x => x.DatasetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DatasetRow>(entity =>
        {
            entity.ToTable($"{prefix}DatasetRows");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RawLine).HasMaxLength(1000);
        });

        modelBuilder.Entity<TheoryDefinition>(entity =>
        {
            entity.ToTable($"{prefix}Theories");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.GroupName).HasMaxLength(120);
            entity.Property(x => x.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<TheoryRun>(entity =>
        {
            entity.ToTable($"{prefix}TheoryRuns");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Dataset)
                .WithMany(x => x.TheoryRuns)
                .HasForeignKey(x => x.DatasetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TheoryResult>(entity =>
        {
            entity.ToTable($"{prefix}TheoryResults");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TheoryCode).HasMaxLength(80);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.GroupName).HasMaxLength(120);
            entity.Property(x => x.Status).HasMaxLength(24);
            entity.Property(x => x.CoverageScore).HasPrecision(18, 4);
            entity.Property(x => x.Confidence).HasPrecision(18, 4);
            entity.Property(x => x.ForwardRate).HasPrecision(18, 4);
            entity.Property(x => x.ReverseRate).HasPrecision(18, 4);
            entity.HasOne(x => x.TheoryRun)
                .WithMany(x => x.Results)
                .HasForeignKey(x => x.TheoryRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CandidateSolution>(entity =>
        {
            entity.ToTable($"{prefix}CandidateSolutions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Rationale).HasMaxLength(1000);
            entity.Property(x => x.TheoriesJson).HasMaxLength(2000);
            entity.Property(x => x.EvidenceJson).HasMaxLength(4000);
            entity.Property(x => x.Confidence).HasPrecision(18, 4);
            entity.HasOne(x => x.TheoryRun)
                .WithMany(x => x.Candidates)
                .HasForeignKey(x => x.TheoryRunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SolverLogEntry>(entity =>
        {
            entity.ToTable($"{prefix}SolverLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Level).HasMaxLength(32);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Detail).HasMaxLength(2000);
            entity.HasOne(x => x.Dataset)
                .WithMany(x => x.Logs)
                .HasForeignKey(x => x.DatasetId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
