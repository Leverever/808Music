using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackMasterMigrations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackMasterMigrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackId = table.Column<int>(type: "int", nullable: false),
                    LegacyRelativePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TargetObjectKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SourceSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    SourceChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StemSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LegacyDeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackMasterMigrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackMasterMigrations_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackMasterMigrations_Status_UpdatedAt",
                table: "TrackMasterMigrations",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackMasterMigrations_TargetObjectKey",
                table: "TrackMasterMigrations",
                column: "TargetObjectKey",
                unique: true,
                filter: "[TargetObjectKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TrackMasterMigrations_TrackId",
                table: "TrackMasterMigrations",
                column: "TrackId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackMasterMigrations");
        }
    }
}
