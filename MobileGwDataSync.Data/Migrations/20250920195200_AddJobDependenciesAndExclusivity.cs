using Microsoft.EntityFrameworkCore.Migrations;

namespace MobileGwDataSync.Data.Migrations
{
    public partial class AddJobDependenciesAndExclusivity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Добавляем новые колонки
            migrationBuilder.AddColumn<string>(
                name: "JobType",
                table: "sync_jobs",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "Subscribers");

            migrationBuilder.AddColumn<string>(
                name: "DependsOnJobId",
                table: "sync_jobs",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsExclusive",
                table: "sync_jobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "sync_jobs",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OneCEndpoint",
                table: "sync_jobs",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TargetTable",
                table: "sync_jobs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetProcedure",
                table: "sync_jobs",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            // Создаем таблицу job_locks
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

            migrationBuilder.CreateIndex(
                name: "IX_job_locks_ExpiresAt",
                table: "job_locks",
                column: "ExpiresAt");

            // Обновляем существующую запись
            migrationBuilder.Sql(@"
                UPDATE sync_jobs 
                SET JobType = 'Subscribers',
                    IsExclusive = 1,
                    Priority = 10,
                    OneCEndpoint = '/api/subscribers',
                    TargetTable = 'staging_subscribers',
                    TargetProcedure = 'USP_MA_MergeSubscribers'
                WHERE Id = 'default-sync-job'
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "job_locks");
            migrationBuilder.DropColumn(name: "JobType", table: "sync_jobs");
            migrationBuilder.DropColumn(name: "DependsOnJobId", table: "sync_jobs");
            migrationBuilder.DropColumn(name: "IsExclusive", table: "sync_jobs");
            migrationBuilder.DropColumn(name: "Priority", table: "sync_jobs");
            migrationBuilder.DropColumn(name: "OneCEndpoint", table: "sync_jobs");
            migrationBuilder.DropColumn(name: "TargetTable", table: "sync_jobs");
            migrationBuilder.DropColumn(name: "TargetProcedure", table: "sync_jobs");
        }
    }
}