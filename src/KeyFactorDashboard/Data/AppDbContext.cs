using Microsoft.EntityFrameworkCore;

namespace KeyFactorDashboard.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ProcessDataset> Datasets => Set<ProcessDataset>();
    public DbSet<TrainJob> TrainJobs => Set<TrainJob>();
    public DbSet<WhatIfNote> WhatIfNotes => Set<WhatIfNote>();
    public DbSet<RoleRecord> Roles => Set<RoleRecord>();
    public DbSet<PlantUser> Users => Set<PlantUser>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ProcessDataset>(e =>
        {
            e.ToTable("process_dataset");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.PayloadKind).HasMaxLength(16);
            e.Property(x => x.ColumnsJson).HasColumnType("TEXT");
            e.Property(x => x.PreviewJson).HasColumnType("TEXT");
            e.Property(x => x.OverviewJson).HasColumnType("TEXT");
            e.Property(x => x.Payload).IsRequired();
        });

        model.Entity<TrainJob>(e =>
        {
            e.ToTable("train_job");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.DatasetId).HasMaxLength(64);
            e.Property(x => x.TargetColumn).HasMaxLength(128);
            e.Property(x => x.Algorithm).HasMaxLength(32);
            e.Property(x => x.Task).HasMaxLength(32);
            e.Property(x => x.MetricsJson).HasColumnType("TEXT");
            e.Property(x => x.FeatureNamesJson).HasColumnType("TEXT");
            e.Property(x => x.ImportancesJson).HasColumnType("TEXT");
            e.HasOne(x => x.Dataset)
                .WithMany()
                .HasForeignKey(x => x.DatasetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<WhatIfNote>(e =>
        {
            e.ToTable("whatif_note");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.ModelId).HasMaxLength(64);
            e.Property(x => x.RequestJson).HasColumnType("TEXT");
            e.Property(x => x.ResponseJson).HasColumnType("TEXT");
        });

        model.Entity<RoleRecord>(e =>
        {
            e.ToTable("plant_role");
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(32);
        });

        model.Entity<PlantUser>(e =>
        {
            e.ToTable("plant_user");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.Username).HasMaxLength(64);
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(512);
            e.Property(x => x.DisplayName).HasMaxLength(128);
            e.Property(x => x.Role).HasMaxLength(32);
        });

        model.Entity<ModelVersion>(e =>
        {
            e.ToTable("model_version");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.DatasetId).HasMaxLength(64);
            e.Property(x => x.Algorithm).HasMaxLength(32);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.MetricsJson).HasColumnType("TEXT");
            e.Property(x => x.ImportancesJson).HasColumnType("TEXT");
            e.Property(x => x.Note).HasMaxLength(512);
            e.HasIndex(x => new { x.DatasetId, x.VersionNumber }).IsUnique();
        });

        model.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_event");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasMaxLength(64);
            e.Property(x => x.UserId).HasMaxLength(64);
            e.Property(x => x.Action).HasMaxLength(32);
            e.Property(x => x.EntityType).HasMaxLength(32);
            e.Property(x => x.EntityId).HasMaxLength(64);
            e.Property(x => x.DetailJson).HasColumnType("TEXT");
        });
    }
}
