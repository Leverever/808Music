using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackAudioAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TrackAudioAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmbeddingModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmbeddingJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackAudioAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackAudioAnalyses_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackAudioTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrackAudioAnalysisId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Namespace = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(9,6)", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackAudioTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackAudioTags_TrackAudioAnalyses_TrackAudioAnalysisId",
                        column: x => x.TrackAudioAnalysisId,
                        principalTable: "TrackAudioAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrackAudioAnalyses_TrackId",
                table: "TrackAudioAnalyses",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackAudioAnalyses_TrackId_IsActive",
                table: "TrackAudioAnalyses",
                columns: new[] { "TrackId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackAudioTags_Namespace_Label",
                table: "TrackAudioTags",
                columns: new[] { "Namespace", "Label" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackAudioTags_TrackAudioAnalysisId",
                table: "TrackAudioTags",
                column: "TrackAudioAnalysisId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackAudioTags");

            migrationBuilder.DropTable(
                name: "TrackAudioAnalyses");
        }
    }
}
