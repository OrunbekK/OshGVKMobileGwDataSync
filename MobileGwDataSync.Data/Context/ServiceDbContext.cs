using Microsoft.EntityFrameworkCore;
using MobileGwDataSync.Data.Entities;

namespace MobileGwDataSync.Data.Context
{
    public class ServiceDbContext : DbContext
    {
        public ServiceDbContext(DbContextOptions<ServiceDbContext> options)
            : base(options)
        {
        }

        public DbSet<SyncJobEntity> SyncJobs { get; set; } = null!;
        public DbSet<SyncRunEntity> SyncRuns { get; set; } = null!;
        public DbSet<SyncRunStepEntity> SyncRunSteps { get; set; } = null!;
        public DbSet<AlertRuleEntity> AlertRules { get; set; } = null!;
        public DbSet<AlertHistoryEntity> AlertHistory { get; set; } = null!;
        public DbSet<PerformanceMetricEntity> PerformanceMetrics { get; set; } = null!;

        public DbSet<JobLockEntity> JobLocks { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // SyncJob configuration
            modelBuilder.Entity<SyncJobEntity>(entity =>
            {
                entity.ToTable("sync_jobs");
                entity.HasIndex(e => e.Name).IsUnique();
                entity.HasMany(e => e.Runs)
                    .WithOne(r => r.Job)
                    .HasForeignKey(r => r.JobId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // SyncRun configuration
            modelBuilder.Entity<SyncRunEntity>(entity =>
            {
                entity.ToTable("sync_runs");
                entity.HasIndex(e => e.JobId);
                entity.HasIndex(e => e.StartTime);
                entity.HasIndex(e => e.Status);

                entity.HasMany(e => e.Steps)
                    .WithOne(s => s.Run)
                    .HasForeignKey(s => s.RunId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(e => e.Metrics)
                    .WithOne(m => m.Run)
                    .HasForeignKey(m => m.RunId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // SyncRunStep configuration
            modelBuilder.Entity<SyncRunStepEntity>(entity =>
            {
                entity.ToTable("sync_run_steps");
                entity.HasIndex(e => e.RunId);
                entity.HasIndex(e => e.StepName);
            });

            // AlertRule configuration
            modelBuilder.Entity<AlertRuleEntity>(entity =>
            {
                entity.ToTable("alert_rules");
                entity.HasIndex(e => e.Name).IsUnique();

                entity.HasMany(e => e.History)
                    .WithOne(h => h.Rule)
                    .HasForeignKey(h => h.RuleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // AlertHistory configuration
            modelBuilder.Entity<AlertHistoryEntity>(entity =>
            {
                entity.ToTable("alert_history");
                entity.HasIndex(e => e.RuleId);
                entity.HasIndex(e => e.TriggeredAt);
            });

            // PerformanceMetric configuration
            modelBuilder.Entity<PerformanceMetricEntity>(entity =>
            {
                entity.ToTable("performance_metrics");
                entity.HasIndex(e => e.RunId);
                entity.HasIndex(e => e.MetricName);
                entity.HasIndex(e => e.RecordedAt);
            });

            // Seed initial data
            modelBuilder.Entity<SyncJobEntity>().HasData(
                new SyncJobEntity
                {
                    Id = "subscribers-sync",
                    Name = "Синхронизация абонентов",
                    JobType = "Subscribers",
                    CronExpression = "0 0 * * * ?", // Каждый час
                    IsEnabled = true,
                    IsExclusive = true,
                    Priority = 10,
                    OneCEndpoint = "/api/subscribers",
                    TargetTable = "staging_subscribers",
                    TargetProcedure = "sp_MergeSubscribers",
                    CreatedAt = DateTime.UtcNow
                }
                // Позже добавите здесь другие задачи
);

            modelBuilder.Entity<AlertRuleEntity>().HasData(
                new AlertRuleEntity
                {
                    Id = 1,
                    Name = "Sync Failure Alert",
                    Type = "SyncStatus",
                    Condition = "Status == Failed",
                    Severity = "Critical",
                    Channels = "[\"email\"]",
                    ThrottleMinutes = 5,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                },
                new AlertRuleEntity
                {
                    Id = 2,
                    Name = "Slow Sync Alert",
                    Type = "Duration",
                    Condition = "DurationMinutes > 10",
                    Severity = "Warning",
                    Channels = "[\"email\"]",
                    ThrottleMinutes = 15,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                }
            );

            // JobLock configuration
            modelBuilder.Entity<JobLockEntity>(entity =>
            {
                entity.ToTable("job_locks");
                entity.HasIndex(e => e.ExpiresAt);
            })
        }
    }
}
