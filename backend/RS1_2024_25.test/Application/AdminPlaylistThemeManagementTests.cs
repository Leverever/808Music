using _808Music.Application.PlaylistThemes;
using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using _808Music.Infrastructure.AutomaticPlaylists;
using _808Music.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RS1_2024_25.Tests.Application;

public sealed class AdminPlaylistThemeManagementTests
{
    [Fact]
    public async Task CreatePersistsNormalizedThemeWithLabels()
    {
        await using var dbContext = CreateDbContext();
        var handler = new AdminPlaylistThemeManagementHandler(dbContext);

        var created = await handler.Create(new CreateAdminPlaylistThemeCommand(
            "  Sunrise-Boost  ",
            "Sunrise Boost",
            "Bright tracks for the start of the day.",
            true,
            30,
            50,
            [
                new AdminPlaylistThemeLabelInput(
                    "uplifting",
                    PersonalizedPlaylistThemeLabelPolarity.Positive,
                    PersonalizedPlaylistThemeLabelSource.EssentiaTag,
                    "moodtheme",
                    1.5m),
                new AdminPlaylistThemeLabelInput(
                    "sad",
                    PersonalizedPlaylistThemeLabelPolarity.Negative,
                    PersonalizedPlaylistThemeLabelSource.EssentiaTag,
                    "moodtheme",
                    0.5m)
            ]));

        Assert.Equal("sunrise-boost", created.ThemeKey);
        Assert.True(created.IsActive);
        Assert.Equal(30, created.TrackCount);
        Assert.Equal(2, created.Labels.Count);
        Assert.All(created.Labels, label =>
            Assert.Equal("moodtheme", label.TagNamespace));
        Assert.Equal(1, await dbContext.PersonalizedPlaylistThemes.CountAsync());
        Assert.Equal(2, await dbContext.PersonalizedPlaylistThemeLabels.CountAsync());
    }

    [Fact]
    public async Task UpdateReplacesLabelsAndPreservesImmutableThemeKey()
    {
        await using var dbContext = CreateDbContext();
        var handler = new AdminPlaylistThemeManagementHandler(dbContext);
        var created = await CreateTheme(handler);

        var updated = await handler.Update(
            created.Id,
            new UpdateAdminPlaylistThemeCommand(
                "Changed Name",
                "Changed description",
                false,
                20,
                80,
                [
                    new AdminPlaylistThemeLabelInput(
                        "calm",
                        PersonalizedPlaylistThemeLabelPolarity.Positive,
                        PersonalizedPlaylistThemeLabelSource.EssentiaTag,
                        "moodtheme",
                        2m)
                ]));

        Assert.NotNull(updated);
        Assert.Equal(created.ThemeKey, updated.ThemeKey);
        Assert.Equal("Changed Name", updated.Name);
        Assert.False(updated.IsActive);
        var label = Assert.Single(updated.Labels);
        Assert.Equal("calm", label.Label);
        Assert.Equal(1, await dbContext.PersonalizedPlaylistThemeLabels.CountAsync());
    }

    [Fact]
    public async Task CreateRejectsDuplicateKeys()
    {
        await using var dbContext = CreateDbContext();
        var handler = new AdminPlaylistThemeManagementHandler(dbContext);
        await CreateTheme(handler);

        var exception = await Assert.ThrowsAsync<PlaylistThemeConflictException>(() =>
            CreateTheme(handler));

        Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRejectsThemesWithoutPositiveEssentiaTag()
    {
        await using var dbContext = CreateDbContext();
        var handler = new AdminPlaylistThemeManagementHandler(dbContext);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Create(new CreateAdminPlaylistThemeCommand(
                "text-only",
                "Text only",
                string.Empty,
                true,
                25,
                10,
                [
                    new AdminPlaylistThemeLabelInput(
                        "a reflective late night",
                        PersonalizedPlaylistThemeLabelPolarity.Positive,
                        PersonalizedPlaylistThemeLabelSource.ClapText,
                        null,
                        1m)
                ])));

        Assert.Contains("positive Essentia", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRejectsAnalyzedTagsWithoutNamespace()
    {
        await using var dbContext = CreateDbContext();
        var handler = new AdminPlaylistThemeManagementHandler(dbContext);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Create(new CreateAdminPlaylistThemeCommand(
                "missing-namespace",
                "Missing namespace",
                string.Empty,
                true,
                25,
                10,
                [
                    new AdminPlaylistThemeLabelInput(
                        "focus",
                        PersonalizedPlaylistThemeLabelPolarity.Positive,
                        PersonalizedPlaylistThemeLabelSource.EssentiaTag,
                        null,
                        1m)
                ])));

        Assert.Contains("namespace", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeactivatedThemeIsExcludedFromActiveProvider()
    {
        await using var dbContext = CreateDbContext();
        var handler = new AdminPlaylistThemeManagementHandler(dbContext);
        var created = await CreateTheme(handler);
        await handler.SetActive(created.Id, false);
        var provider = new PersonalizedPlaylistThemeProvider(dbContext);

        var activeThemes = await provider.GetActiveThemesAsync();

        Assert.Empty(activeThemes);
    }

    [Fact]
    public async Task TagCatalogCombinesAnalyzedAndThemeTagsByNamespace()
    {
        await using var dbContext = CreateDbContext();
        var analysisId = Guid.NewGuid();
        dbContext.TrackAudioTags.AddRange(
            new TrackAudioTag(
                analysisId,
                "discogs.electronic",
                "Techno",
                0.9m,
                "discogs"),
            new TrackAudioTag(
                analysisId,
                "discogs.electronic",
                "techno",
                0.8m,
                "discogs"),
            new TrackAudioTag(
                analysisId,
                "genre",
                "Electronic",
                0.7m,
                "essentia"));
        await dbContext.SaveChangesAsync();
        var handler = new AdminPlaylistThemeManagementHandler(dbContext);
        await CreateTheme(handler);

        var catalog = await handler.GetTagCatalog();

        var discogs = Assert.Single(
            catalog,
            item => item.Namespace == "discogs.electronic");
        Assert.Single(discogs.Labels);
        Assert.Equal("Techno", discogs.Labels[0], ignoreCase: true);
        Assert.Contains(catalog, item =>
            item.Namespace == "genre" &&
            item.Labels.Contains("Electronic"));
        Assert.Contains(catalog, item =>
            item.Namespace == "top50tags" &&
            item.Labels.Contains("focus"));
    }

    private static MusicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MusicDbContext(options);
    }

    private static Task<AdminPlaylistThemeResponse> CreateTheme(
        AdminPlaylistThemeManagementHandler handler)
    {
        return handler.Create(new CreateAdminPlaylistThemeCommand(
            "focus-flow",
            "Focus Flow",
            "A steady personalized focus mix.",
            true,
            25,
            10,
            [
                new AdminPlaylistThemeLabelInput(
                    "focus",
                    PersonalizedPlaylistThemeLabelPolarity.Positive,
                    PersonalizedPlaylistThemeLabelSource.EssentiaTag,
                    "top50tags",
                    1m)
            ]));
    }
}
