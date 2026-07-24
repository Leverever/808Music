using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StorePersonalizedPlaylistThemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ThemeId",
                table: "GeneratedPersonalizedPlaylists",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PersonalizedPlaylistThemes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThemeKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TrackCount = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalizedPlaylistThemes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PersonalizedPlaylistThemeLabels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ThemeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Polarity = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(9,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalizedPlaylistThemeLabels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonalizedPlaylistThemeLabels_PersonalizedPlaylistThemes_ThemeId",
                        column: x => x.ThemeId,
                        principalTable: "PersonalizedPlaylistThemes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            var seededAt = new DateTime(2026, 7, 12, 0, 0, 0, DateTimeKind.Utc);
            var energeticMixId = new Guid("11111111-1111-4111-8111-111111111111");
            var threeAmAndAloneId = new Guid("22222222-2222-4222-8222-222222222222");
            var lateNightDriveId = new Guid("33333333-3333-4333-8333-333333333333");
            var gymMotivationId = new Guid("44444444-4444-4444-8444-444444444444");

            migrationBuilder.InsertData(
                table: "PersonalizedPlaylistThemes",
                columns: new[]
                {
                    "Id", "ThemeKey", "Name", "Description", "IsActive",
                    "TrackCount", "SortOrder", "CreatedAt", "UpdatedAt"
                },
                values: new object[,]
                {
                    {
                        energeticMixId,
                        "energetic-mix",
                        "Energetic Mix",
                        "A daily upbeat mix shaped around your recent listening.",
                        true,
                        25,
                        10,
                        seededAt,
                        seededAt
                    },
                    {
                        threeAmAndAloneId,
                        "three-am-and-alone",
                        "3am and Alone Mix",
                        "A late-night, introspective mix picked for your taste.",
                        true,
                        25,
                        20,
                        seededAt,
                        seededAt
                    },
                    {
                        lateNightDriveId,
                        "late-night-drive",
                        "Late Night Drive Mix",
                        "Smooth tracks for night drives, tuned to your profile.",
                        true,
                        25,
                        30,
                        seededAt,
                        seededAt
                    },
                    {
                        gymMotivationId,
                        "gym-motivation",
                        "Gym Motivation Mix",
                        "High-energy tracks for training, personalized daily.",
                        true,
                        25,
                        40,
                        seededAt,
                        seededAt
                    }
                });

            migrationBuilder.Sql(
                """
                INSERT INTO [PersonalizedPlaylistThemeLabels]
                    ([Id], [ThemeId], [Label], [Polarity], [Source], [Weight])
                VALUES
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'energetic', 1, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'energy', 1, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'upbeat', 1, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'dance', 1, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'electronic', 1, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'pop', 1, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'party', 1, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'sad', 2, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'ambient', 2, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'acoustic', 2, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'calm', 2, 1, 1.0),
                    (NEWID(), '11111111-1111-4111-8111-111111111111', N'sleep', 2, 1, 1.0),

                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'sad', 1, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'melancholic', 1, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'lonely', 1, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'night', 1, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'ambient', 1, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'downtempo', 1, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'chill', 1, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'party', 2, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'dance', 2, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'gym', 2, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'workout', 2, 1, 1.0),
                    (NEWID(), '22222222-2222-4222-8222-222222222222', N'club', 2, 1, 1.0),

                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'night', 1, 1, 1.0),
                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'drive', 1, 1, 1.0),
                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'chill', 1, 1, 1.0),
                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'electronic', 1, 1, 1.0),
                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'synth', 1, 1, 1.0),
                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'pop', 1, 1, 1.0),
                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'rnb', 1, 1, 1.0),
                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'hiphop', 1, 1, 1.0),
                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'workout', 2, 1, 1.0),
                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'gym', 2, 1, 1.0),
                    (NEWID(), '33333333-3333-4333-8333-333333333333', N'aggressive', 2, 1, 1.0),

                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'gym', 1, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'workout', 1, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'energetic', 1, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'energy', 1, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'aggressive', 1, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'hiphop', 1, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'rock', 1, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'dance', 1, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'sad', 2, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'ambient', 2, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'calm', 2, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'acoustic', 2, 1, 1.0),
                    (NEWID(), '44444444-4444-4444-8444-444444444444', N'sleep', 2, 1, 1.0);
                """);

            migrationBuilder.Sql(
                """
                UPDATE playlists
                SET playlists.[ThemeId] = themes.[Id]
                FROM [GeneratedPersonalizedPlaylists] AS playlists
                INNER JOIN [PersonalizedPlaylistThemes] AS themes
                    ON playlists.[ThemeKey] = themes.[ThemeKey];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedPersonalizedPlaylists_ThemeId",
                table: "GeneratedPersonalizedPlaylists",
                column: "ThemeId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalizedPlaylistThemeLabels_ThemeId_Polarity_Source_Label",
                table: "PersonalizedPlaylistThemeLabels",
                columns: new[] { "ThemeId", "Polarity", "Source", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalizedPlaylistThemes_IsActive_SortOrder",
                table: "PersonalizedPlaylistThemes",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalizedPlaylistThemes_ThemeKey",
                table: "PersonalizedPlaylistThemes",
                column: "ThemeKey",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_GeneratedPersonalizedPlaylists_PersonalizedPlaylistThemes_ThemeId",
                table: "GeneratedPersonalizedPlaylists",
                column: "ThemeId",
                principalTable: "PersonalizedPlaylistThemes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GeneratedPersonalizedPlaylists_PersonalizedPlaylistThemes_ThemeId",
                table: "GeneratedPersonalizedPlaylists");

            migrationBuilder.DropTable(
                name: "PersonalizedPlaylistThemeLabels");

            migrationBuilder.DropTable(
                name: "PersonalizedPlaylistThemes");

            migrationBuilder.DropIndex(
                name: "IX_GeneratedPersonalizedPlaylists_ThemeId",
                table: "GeneratedPersonalizedPlaylists");

            migrationBuilder.DropColumn(
                name: "ThemeId",
                table: "GeneratedPersonalizedPlaylists");
        }
    }
}
