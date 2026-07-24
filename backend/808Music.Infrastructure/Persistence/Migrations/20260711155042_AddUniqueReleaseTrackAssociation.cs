using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueReleaseTrackAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlbumTracks_TrackId",
                table: "AlbumTracks");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumTracks_AlbumId_TrackId",
                table: "AlbumTracks",
                columns: new[] { "AlbumId", "TrackId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlbumTracks_TrackId",
                table: "AlbumTracks",
                column: "TrackId",
                unique: true,
                filter: "[IsPrimaryRelease] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AlbumTracks_AlbumId_TrackId",
                table: "AlbumTracks");

            migrationBuilder.DropIndex(
                name: "IX_AlbumTracks_TrackId",
                table: "AlbumTracks");

            migrationBuilder.CreateIndex(
                name: "IX_AlbumTracks_TrackId",
                table: "AlbumTracks",
                column: "TrackId");
        }
    }
}
