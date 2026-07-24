using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace _808Music.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaylistThemeLabelNamespaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersonalizedPlaylistThemeLabels_ThemeId_Polarity_Source_Label",
                table: "PersonalizedPlaylistThemeLabels");

            migrationBuilder.AddColumn<string>(
                name: "TagNamespace",
                table: "PersonalizedPlaylistThemeLabels",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE [PersonalizedPlaylistThemeLabels]
                SET [TagNamespace] = N'top50tags'
                WHERE [Source] = 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PersonalizedPlaylistThemeLabels_ThemeId_Polarity_Source_TagNamespace_Label",
                table: "PersonalizedPlaylistThemeLabels",
                columns: new[] { "ThemeId", "Polarity", "Source", "TagNamespace", "Label" },
                unique: true,
                filter: "[TagNamespace] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalizedPlaylistThemeLabels_ThemeId_Polarity_Source_Label",
                table: "PersonalizedPlaylistThemeLabels",
                columns: new[] { "ThemeId", "Polarity", "Source", "Label" },
                unique: true,
                filter: "[TagNamespace] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersonalizedPlaylistThemeLabels_ThemeId_Polarity_Source_TagNamespace_Label",
                table: "PersonalizedPlaylistThemeLabels");

            migrationBuilder.DropIndex(
                name: "IX_PersonalizedPlaylistThemeLabels_ThemeId_Polarity_Source_Label",
                table: "PersonalizedPlaylistThemeLabels");

            migrationBuilder.DropColumn(
                name: "TagNamespace",
                table: "PersonalizedPlaylistThemeLabels");

            migrationBuilder.CreateIndex(
                name: "IX_PersonalizedPlaylistThemeLabels_ThemeId_Polarity_Source_Label",
                table: "PersonalizedPlaylistThemeLabels",
                columns: new[] { "ThemeId", "Polarity", "Source", "Label" },
                unique: true);
        }
    }
}
