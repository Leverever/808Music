using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioClustering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AudioClusterRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AlgorithmName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EmbeddingSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioClusterRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AudioClusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClusterRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClusterKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Size = table.Column<int>(type: "int", nullable: false),
                    TopTagsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioClusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioClusters_AudioClusterRuns_ClusterRunId",
                        column: x => x.ClusterRunId,
                        principalTable: "AudioClusterRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TrackClusterAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClusterRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrackId = table.Column<int>(type: "int", nullable: false),
                    ClusterKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsNoise = table.Column<bool>(type: "bit", nullable: false),
                    DistanceToCenter = table.Column<decimal>(type: "decimal(18,9)", nullable: true),
                    MembershipScore = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrackClusterAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TrackClusterAssignments_AudioClusterRuns_ClusterRunId",
                        column: x => x.ClusterRunId,
                        principalTable: "AudioClusterRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrackClusterAssignments_AudioClusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "AudioClusters",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TrackClusterAssignments_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioClusterRuns_AlgorithmName_EmbeddingSource_IsActive",
                table: "AudioClusterRuns",
                columns: new[] { "AlgorithmName", "EmbeddingSource", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AudioClusterRuns_IsActive",
                table: "AudioClusterRuns",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AudioClusters_ClusterRunId",
                table: "AudioClusters",
                column: "ClusterRunId");

            migrationBuilder.CreateIndex(
                name: "IX_AudioClusters_ClusterRunId_ClusterKey",
                table: "AudioClusters",
                columns: new[] { "ClusterRunId", "ClusterKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackClusterAssignments_ClusterId",
                table: "TrackClusterAssignments",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackClusterAssignments_ClusterRunId",
                table: "TrackClusterAssignments",
                column: "ClusterRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TrackClusterAssignments_ClusterRunId_TrackId",
                table: "TrackClusterAssignments",
                columns: new[] { "ClusterRunId", "TrackId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrackClusterAssignments_TrackId",
                table: "TrackClusterAssignments",
                column: "TrackId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrackClusterAssignments");

            migrationBuilder.DropTable(
                name: "AudioClusters");

            migrationBuilder.DropTable(
                name: "AudioClusterRuns");
        }
    }
}
