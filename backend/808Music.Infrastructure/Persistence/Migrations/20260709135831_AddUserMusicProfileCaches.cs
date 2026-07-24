using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMusicProfileCaches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserMusicProfileCaches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ProfileDate = table.Column<DateOnly>(type: "date", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SourceInteractionCount = table.Column<int>(type: "int", nullable: false),
                    SourceWindowDays = table.Column<int>(type: "int", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TagAffinitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClusterAffinitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecentTrackIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FavoriteArtistIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FavoriteAlbumIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMusicProfileCaches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserMusicProfileCaches_GeneratedAt",
                table: "UserMusicProfileCaches",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserMusicProfileCaches_UserId_ProfileDate",
                table: "UserMusicProfileCaches",
                columns: new[] { "UserId", "ProfileDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserMusicProfileCaches");
        }
    }
}
