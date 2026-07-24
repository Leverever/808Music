using _808Music.Application.Tracks;
using _808Music.Domain.Artists;
using _808Music.Domain.Catalog;
using _808Music.Domain.Static;
using _808Music.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RS1_2024_25.Tests.Application;

public sealed class TrackCatalogHandlerTests
{
    [Fact]
    public async Task Catalog_returns_only_lead_tracks_with_server_filtering()
    {
        await using var db = CreateDbContext();
        var lead = new Artist { Name = "Lead" };
        var featured = new Artist { Name = "Featured" };
        var first = new Track { Title = "First song", TrackPath = "tracks/first.mp3" };
        var second = new Track { Title = "Second song", TrackPath = "tracks/second.mp3" };
        var appearance = new Track { Title = "Feature appearance", TrackPath = "tracks/appearance.mp3" };
        db.AddRange(lead, featured, first, second, appearance);
        db.ArtistTracks.AddRange(
            new ArtistTrack { Artist = lead, Track = first, IsLead = true, ShowOnProfile = true },
            new ArtistTrack { Artist = lead, Track = second, IsLead = true, ShowOnProfile = true },
            new ArtistTrack { Artist = lead, Track = appearance, IsLead = false, ShowOnProfile = true },
            new ArtistTrack { Artist = featured, Track = appearance, IsLead = true, ShowOnProfile = true });
        await db.SaveChangesAsync();

        var handler = new TrackCatalogHandler(db);
        var result = await handler.List(new TrackCatalogQuery(
            lead.Id,
            PageNumber: 1,
            PageSize: 20,
            Title: "song"));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal([second.Id, first.Id], result.Items.Select(x => x.Id));
        Assert.DoesNotContain(result.Items, x => x.Id == appearance.Id);
    }

    [Fact]
    public async Task Catalog_filters_by_primary_release_streams_and_duration_before_paging()
    {
        await using var db = CreateDbContext();
        var lead = new Artist { Name = "Lead" };
        var type = new AlbumType { Type = "Album" };
        var matchingRelease = new Album
        {
            Title = "Night Drive Deluxe",
            Artist = lead,
            AlbumType = type,
            ReleaseDate = DateTime.UtcNow
        };
        var otherRelease = new Album
        {
            Title = "Morning Edition",
            Artist = lead,
            AlbumType = type,
            ReleaseDate = DateTime.UtcNow
        };
        var matching = new Track
        {
            Title = "Match",
            TrackPath = "tracks/match.mp3",
            Streams = 750,
            Length = 240,
            Album = matchingRelease
        };
        var tooShort = new Track
        {
            Title = "Short",
            TrackPath = "tracks/short.mp3",
            Streams = 750,
            Length = 60,
            Album = matchingRelease
        };
        var wrongRelease = new Track
        {
            Title = "Other",
            TrackPath = "tracks/other.mp3",
            Streams = 750,
            Length = 240,
            Album = otherRelease
        };
        db.AddRange(lead, type, matchingRelease, otherRelease, matching, tooShort, wrongRelease);
        db.ArtistTracks.AddRange(
            new ArtistTrack { Artist = lead, Track = matching, IsLead = true, ShowOnProfile = true },
            new ArtistTrack { Artist = lead, Track = tooShort, IsLead = true, ShowOnProfile = true },
            new ArtistTrack { Artist = lead, Track = wrongRelease, IsLead = true, ShowOnProfile = true });
        await db.SaveChangesAsync();

        var result = await new TrackCatalogHandler(db).List(new TrackCatalogQuery(
            lead.Id,
            PageNumber: 1,
            PageSize: 20,
            PrimaryReleaseTitle: "Deluxe",
            MinStreams: 500,
            MaxStreams: 1000,
            MinDurationSeconds: 180,
            MaxDurationSeconds: 300));

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(matching.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Catalog_sorts_before_applying_server_paging()
    {
        await using var db = CreateDbContext();
        var lead = new Artist { Name = "Lead" };
        var low = new Track { Title = "Low", TrackPath = "tracks/low.mp3", Streams = 5 };
        var high = new Track { Title = "High", TrackPath = "tracks/high.mp3", Streams = 500 };
        var middle = new Track { Title = "Middle", TrackPath = "tracks/middle.mp3", Streams = 50 };
        db.AddRange(lead, low, high, middle);
        db.ArtistTracks.AddRange(
            new ArtistTrack { Artist = lead, Track = low, IsLead = true, ShowOnProfile = true },
            new ArtistTrack { Artist = lead, Track = high, IsLead = true, ShowOnProfile = true },
            new ArtistTrack { Artist = lead, Track = middle, IsLead = true, ShowOnProfile = true });
        await db.SaveChangesAsync();

        var result = await new TrackCatalogHandler(db).List(new TrackCatalogQuery(
            lead.Id,
            PageNumber: 1,
            PageSize: 1,
            SortBy: "streams",
            SortDirection: "desc"));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(high.Id, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task Featured_artist_save_is_atomic_and_preserves_relationship_settings()
    {
        await using var db = CreateDbContext();
        var lead = new Artist { Name = "Lead" };
        var firstFeatured = new Artist { Name = "First" };
        var secondFeatured = new Artist { Name = "Second" };
        var track = new Track { Title = "Track", TrackPath = "tracks/track.mp3" };
        db.AddRange(lead, firstFeatured, secondFeatured, track);
        db.ArtistTracks.AddRange(
            new ArtistTrack { Artist = lead, Track = track, IsLead = true, ShowOnProfile = true },
            new ArtistTrack { Artist = firstFeatured, Track = track, IsLead = false, ShowOnProfile = true });
        await db.SaveChangesAsync();

        var handler = new TrackCatalogHandler(db);
        var result = await handler.ReplaceFeaturedArtists(
            track.Id,
            [
                new FeaturedArtistInput(firstFeatured.Id, false),
                new FeaturedArtistInput(secondFeatured.Id, true)
            ]);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.False(result.Single(x => x.ArtistId == firstFeatured.Id).ShowOnProfile);
        Assert.True(result.Single(x => x.ArtistId == secondFeatured.Id).ShowOnProfile);
        Assert.Single(db.ArtistTracks.Where(x => x.TrackId == track.Id && x.IsLead));
    }

    [Fact]
    public async Task Release_save_materializes_legacy_association_and_synchronizes_primary_release()
    {
        await using var db = CreateDbContext();
        var (artist, track, firstRelease, secondRelease) = await SeedTrackAndReleases(db);
        track.AlbumId = firstRelease.Id;
        await db.SaveChangesAsync();

        var handler = new TrackCatalogHandler(db);
        var result = await handler.ReplaceReleases(
            track.Id,
            [
                new TrackReleaseInput(firstRelease.Id, 1, 1, null, false),
                new TrackReleaseInput(secondRelease.Id, 1, 2, "Alternate", true)
            ]);

        Assert.NotNull(result);
        Assert.Equal(2, db.AlbumTracks.Count(x => x.TrackId == track.Id));
        Assert.Equal(secondRelease.Id, track.AlbumId);
        Assert.Equal("Alternate", result.Single(x => x.ReleaseId == secondRelease.Id).TitleOverride);
        Assert.Equal(1, firstRelease.NumOfTracks);
        Assert.Equal(1, secondRelease.NumOfTracks);
        Assert.All(result, x => Assert.False(x.IsLegacyAssociation));
        Assert.Equal(artist.Id, db.Albums.Single(x => x.Id == firstRelease.Id).ArtistId);
    }

    [Fact]
    public async Task Release_save_rejects_an_occupied_position()
    {
        await using var db = CreateDbContext();
        var (_, track, release, _) = await SeedTrackAndReleases(db);
        var other = new Track { Title = "Other", TrackPath = "tracks/other.mp3" };
        db.Tracks.Add(other);
        db.AlbumTracks.Add(new AlbumTrack
        {
            Album = release,
            Track = other,
            DiscNumber = 1,
            TrackNumber = 3
        });
        await db.SaveChangesAsync();

        var handler = new TrackCatalogHandler(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.ReplaceReleases(
            track.Id,
            [new TrackReleaseInput(release.Id, 1, 3, null, true)]));
    }

    [Fact]
    public async Task Statistics_zero_fill_the_selected_range_and_count_identified_listeners()
    {
        await using var db = CreateDbContext();
        var track = new Track
        {
            Title = "Track",
            TrackPath = "tracks/track.mp3",
            Length = 180,
            Streams = 5
        };
        db.Tracks.Add(track);
        await db.SaveChangesAsync();
        db.TrackStreams.AddRange(
            new TrackStream { TrackId = track.Id, UserId = 10, StreamedAt = DateTime.UtcNow.Date },
            new TrackStream { TrackId = track.Id, UserId = 10, StreamedAt = DateTime.UtcNow.Date },
            new TrackStream { TrackId = track.Id, UserId = 11, StreamedAt = DateTime.UtcNow.Date.AddDays(-2) });
        await db.SaveChangesAsync();

        var handler = new TrackStatisticsHandler(db);
        var result = await handler.Get(track.Id, 7);

        Assert.NotNull(result);
        Assert.Equal(7, result.DailyStreams.Count);
        Assert.Equal(3, result.PeriodStreams);
        Assert.Equal(2, result.PeriodUniqueListeners);
        Assert.Equal(2, result.AllTimeUniqueListeners);
        Assert.Equal(900, result.EstimatedAllTimeStreamedSeconds);
        Assert.Equal(2, result.DailyStreams.Single(x => x.Date == DateTime.UtcNow.Date).Streams);
    }

    private static MusicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MusicDbContext(options);
    }

    private static async Task<(Artist Artist, Track Track, Album First, Album Second)> SeedTrackAndReleases(
        MusicDbContext db)
    {
        var artist = new Artist { Name = "Lead" };
        var type = new AlbumType { Type = "Album" };
        var first = new Album
        {
            Title = "First",
            Artist = artist,
            AlbumType = type,
            ReleaseDate = DateTime.UtcNow
        };
        var second = new Album
        {
            Title = "Second",
            Artist = artist,
            AlbumType = type,
            ReleaseDate = DateTime.UtcNow
        };
        var track = new Track { Title = "Track", TrackPath = "tracks/track.mp3" };
        db.AddRange(artist, type, first, second, track);
        db.ArtistTracks.Add(new ArtistTrack
        {
            Artist = artist,
            Track = track,
            IsLead = true,
            ShowOnProfile = true
        });
        await db.SaveChangesAsync();
        return (artist, track, first, second);
    }
}
