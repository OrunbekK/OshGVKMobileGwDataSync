using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MobileGwDataSync.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "sync_jobs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
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

            migrationBuilder.InsertData(
                table: "alert_rules",
                columns: new[] { "Id", "Channels", "Condition", "CreatedAt", "IsEnabled", "Name", "Severity", "ThrottleMinutes", "Type", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, "[\"email\"]", "Status == Failed", new DateTime(2025, 9, 20, 4, 52, 56, 876, DateTimeKind.Utc).AddTicks(3863), true, "Sync Failure Alert", "Critical", 5, "SyncStatus", null },
                    { 2, "[\"email\"]", "DurationMinutes > 10", new DateTime(2025, 9, 20, 4, 52, 56, 876, DateTimeKind.Utc).AddTicks(3867), true, "Slow Sync Alert", "Warning", 15, "Duration", null }
                });

            migrationBuilder.InsertData(
                table: "sync_jobs",
                columns: new[] { "Id", "Configuration", "CreatedAt", "CronExpression", "IsEnabled", "LastRunAt", "Name", "NextRunAt", "UpdatedAt" },
                values: new object[] { "default-sync-job", null, new DateTime(2025, 9, 20, 4, 52, 56, 876, DateTimeKind.Utc).AddTicks(3706), "0 0 * * * ?", true, null, "Default 1C Sync Job", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_alert_history_RuleId",
                table: "alert_history",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "IX_alert_history_TriggeredAt",
                table: "alert_history",
                column: "TriggeredAt");

            migrationBuilder.CreateIndex(
                name: "IX_alert_rules_Name",
                table: "alert_rules",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_performance_metrics_MetricName",
                table: "performance_metrics",
                column: "MetricName");

            migrationBuilder.CreateIndex(
                name: "IX_performance_metrics_RecordedAt",
                table: "performance_metrics",
                column: "RecordedAt");

            migrationBuilder.CreateIndex(
                name: "IX_performance_metrics_RunId",
                table: "performance_metrics",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_sync_jobs_Name",
                table: "sync_jobs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_run_steps_RunId",
                table: "sync_run_steps",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_sync_run_steps_StepName",
                table: "sync_run_steps",
                column: "StepName");

            migrationBuilder.CreateIndex(
                name: "IX_sync_runs_JobId",
                table: "sync_runs",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_sync_runs_StartTime",
                table: "sync_runs",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_sync_runs_Status",
                table: "sync_runs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_history");

            migrationBuilder.DropTable(
                name: "performance_metrics");

            migrationBuilder.DropTable(
                name: "sync_run_steps");

            migrationBuilder.DropTable(
                name: "alert_rules");

            migrationBuilder.DropTable(
                name: "sync_runs");

            migrationBuilder.DropTable(
                name: "sync_jobs");
        }
    }
}
