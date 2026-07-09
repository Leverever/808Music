using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedPersonalizedPlaylists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeneratedPersonalizedPlaylists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ThemeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PlaylistDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedPersonalizedPlaylists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GeneratedPersonalizedPlaylistTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackId = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedPersonalizedPlaylistTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedPersonalizedPlaylistTracks_GeneratedPersonalizedPlaylists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "GeneratedPersonalizedPlaylists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GeneratedPersonalizedPlaylistTracks_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedPersonalizedPlaylists_UserId_PlaylistDate",
                table: "GeneratedPersonalizedPlaylists",
                columns: new[] { "UserId", "PlaylistDate" });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedPersonalizedPlaylists_UserId_ThemeKey_PlaylistDate",
                table: "GeneratedPersonalizedPlaylists",
                columns: new[] { "UserId", "ThemeKey", "PlaylistDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedPersonalizedPlaylistTracks_PlaylistId_Position",
                table: "GeneratedPersonalizedPlaylistTracks",
                columns: new[] { "PlaylistId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedPersonalizedPlaylistTracks_PlaylistId_TrackId",
                table: "GeneratedPersonalizedPlaylistTracks",
                columns: new[] { "PlaylistId", "TrackId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedPersonalizedPlaylistTracks_TrackId",
                table: "GeneratedPersonalizedPlaylistTracks",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneratedPersonalizedPlaylistTracks");

            migrationBuilder.DropTable(
                name: "GeneratedPersonalizedPlaylists");
        }
    }
}
