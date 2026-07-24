using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserTrackInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserTrackInteractions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    TrackId = table.Column<int>(type: "int", nullable: false),
                    InteractionType = table.Column<int>(type: "int", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlayedMs = table.Column<long>(type: "bigint", nullable: true),
                    TrackDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    CompletionRatio = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    ContextType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClientEventId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTrackInteractions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserTrackInteractions_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTrackInteractions_TrackId_InteractionType",
                table: "UserTrackInteractions",
                columns: new[] { "TrackId", "InteractionType" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTrackInteractions_UserId_ClientEventId",
                table: "UserTrackInteractions",
                columns: new[] { "UserId", "ClientEventId" },
                unique: true,
                filter: "[ClientEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_UserTrackInteractions_UserId_OccurredAt",
                table: "UserTrackInteractions",
                columns: new[] { "UserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTrackInteractions_UserId_TrackId",
                table: "UserTrackInteractions",
                columns: new[] { "UserId", "TrackId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTrackInteractions");
        }
    }
}
