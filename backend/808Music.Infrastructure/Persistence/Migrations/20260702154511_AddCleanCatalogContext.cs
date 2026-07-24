using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCleanCatalogContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlbumTracks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AlbumId = table.Column<int>(type: "int", nullable: false),
                    TrackId = table.Column<int>(type: "int", nullable: false),
                    DiscNumber = table.Column<int>(type: "int", nullable: false),
                    TrackNumber = table.Column<int>(type: "int", nullable: false),
                    TitleOverride = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsPrimaryRelease = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlbumTracks_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlbumTracks_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TrackStemSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ModelVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackStemSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackStemSets_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackStems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StemSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StemType = table.Column<int>(type: "int", nullable: false),
                    ObjectKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    DurationMs = table.Column<int>(type: "int", nullable: true),
                    SampleRate = table.Column<int>(type: "int", nullable: true),
                    BitrateKbps = table.Column<int>(type: "int", nullable: true),
                    Codec = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Channels = table.Column<int>(type: "int", nullable: true),
                    ChecksumSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackStems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackStems_TrackStemSets_StemSetId",
                        column: x => x.StemSetId,
                        principalTable: "TrackStemSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumTracks_AlbumId_DiscNumber_TrackNumber",
                table: "AlbumTracks",
                columns: new[] { "AlbumId", "DiscNumber", "TrackNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlbumTracks_TrackId",
                table: "AlbumTracks",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackStems_StemSetId",
                table: "TrackStems",
                column: "StemSetId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackStemSets_TrackId",
                table: "TrackStemSets",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlbumTracks");

            migrationBuilder.DropTable(
                name: "TrackStems");

            migrationBuilder.DropTable(
                name: "TrackStemSets");
        }
    }
}
