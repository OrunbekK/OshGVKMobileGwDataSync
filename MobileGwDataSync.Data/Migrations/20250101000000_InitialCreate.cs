using Microsoft.EntityFrameworkCore.Migrations;

namespace MobileGwDataSync.Data.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Таблица sync_jobs
            migrationBuilder.CreateTable(
                name: "sync_jobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    JobType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    DependsOnJobId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsExclusive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    OneCEndpoint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TargetTable = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    TargetProcedure = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Configuration = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_jobs", x => x.Id);
                });

            // Таблица sync_runs
            migrationBuilder.CreateTable(
                name: "sync_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobId = table.Column<string>(type: "TEXT", nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    RecordsProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    RecordsFetched = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sync_runs_sync_jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "sync_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Таблица sync_run_steps
            migrationBuilder.CreateTable(
                name: "sync_run_steps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    Metrics = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_run_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sync_run_steps_sync_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "sync_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Таблица alert_rules
            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Condition = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Severity = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Channels = table.Column<string>(type: "TEXT", nullable: false),
                    ThrottleMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_rules", x => x.Id);
                });

            // Таблица alert_history
            migrationBuilder.CreateTable(
                name: "alert_history",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RuleId = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggeredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    NotificationsSent = table.Column<string>(type: "TEXT", nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "INTEGER", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_alert_history_alert_rules_RuleId",
                        column: x => x.RuleId,
                        principalTable: "alert_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Таблица performance_metrics
            migrationBuilder.CreateTable(
                name: "performance_metrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetricName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    MetricValue = table.Column<decimal>(type: "TEXT", nullable: false),
                    Unit = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_performance_metrics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_performance_metrics_sync_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "sync_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Таблица job_locks
            migrationBuilder.CreateTable(
                name: "job_locks",
                columns: table => new
                {
                    JobId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LockedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_locks", x => x.JobId);
                });

            // Индексы
            migrationBuilder.CreateIndex("IX_sync_jobs_Name", "sync_jobs", "Name", unique: true);
            migrationBuilder.CreateIndex("IX_sync_runs_JobId", "sync_runs", "JobId");
            migrationBuilder.CreateIndex("IX_sync_runs_StartTime", "sync_runs", "StartTime");
            migrationBuilder.CreateIndex("IX_sync_runs_Status", "sync_runs", "Status");
            migrationBuilder.CreateIndex("IX_sync_run_steps_RunId", "sync_run_steps", "RunId");
            migrationBuilder.CreateIndex("IX_sync_run_steps_StepName", "sync_run_steps", "StepName");
            migrationBuilder.CreateIndex("IX_alert_rules_Name", "alert_rules", "Name", unique: true);
            migrationBuilder.CreateIndex("IX_alert_history_RuleId", "alert_history", "RuleId");
            migrationBuilder.CreateIndex("IX_alert_history_TriggeredAt", "alert_history", "TriggeredAt");
            migrationBuilder.CreateIndex("IX_performance_metrics_RunId", "performance_metrics", "RunId");
            migrationBuilder.CreateIndex("IX_performance_metrics_MetricName", "performance_metrics", "MetricName");
            migrationBuilder.CreateIndex("IX_performance_metrics_RecordedAt", "performance_metrics", "RecordedAt");
            migrationBuilder.CreateIndex("IX_job_locks_ExpiresAt", "job_locks", "ExpiresAt");

            // Вставка начальных данных
            migrationBuilder.InsertData(
                table: "sync_jobs",
                columns: new[] { "Id", "Name", "JobType", "CronExpression", "IsEnabled", "IsExclusive", "Priority", "OneCEndpoint", "TargetTable", "TargetProcedure", "CreatedAt", "Configuration" },
                values: new object[] {
                    "subscribers-sync",
                    "Синхронизация абонентов",
                    "Subscribers",
                    "0 0 * * * ?",
                    true,
                    true,
                    10,
                    "/api/subscribers",
                    "staging_subscribers",
                    "USP_MA_MergeSubscribers",
                    new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    null!
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("job_locks");
            migrationBuilder.DropTable("alert_history");
            migrationBuilder.DropTable("performance_metrics");
            migrationBuilder.DropTable("sync_run_steps");
            migrationBuilder.DropTable("alert_rules");
            migrationBuilder.DropTable("sync_runs");
            migrationBuilder.DropTable("sync_jobs");
        }
    }
}