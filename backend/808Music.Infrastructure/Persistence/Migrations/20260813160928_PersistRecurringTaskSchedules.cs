using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistRecurringTaskSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringTaskSchedules",
                columns: table => new
                {
                    TaskName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NextRunUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastScheduledRunUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringTaskSchedules", x => x.TaskName);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringTaskSchedules_NextRunUtc",
                table: "RecurringTaskSchedules",
                column: "NextRunUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringTaskSchedules");
        }
    }
}
