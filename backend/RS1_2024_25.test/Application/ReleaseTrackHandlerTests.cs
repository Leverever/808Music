using _808Music.Application.Releases;
using _808Music.Domain.Artists;
using _808Music.Domain.Catalog;
using _808Music.Domain.Static;
using _808Music.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace RS1_2024_25.Tests.Application;

public sealed class ReleaseTrackHandlerTests
{
    [Fact]
    public async Task CreateReadUpdateAndDeleteMaintainCatalogCompatibilityFields()
    {
        await using var dbContext = CreateDbContext();
        var (release, track) = await SeedCatalog(dbContext);
        var handler = new ReleaseTrackHandler(dbContext);

        var created = await handler.Create(
            new CreateReleaseTrackCommand(
                release.Id,
                track.Id,
                DiscNumber: 1,
                TrackNumber: 1,
                TitleOverride: "Release title",
                IsPrimaryRelease: true));

        Assert.Equal(release.Id, created.ReleaseId);
        Assert.Equal("Release title", created.Title);
        Assert.Equal(release.Id, track.AlbumId);
        Assert.Equal(1, release.NumOfTracks);

        var listed = await handler.List(new ReleaseTrackListQuery(release.Id));
        var read = await handler.Get(release.Id, track.Id);

        Assert.NotNull(listed);
        Assert.Single(listed.Items);
        Assert.NotNull(read);
        Assert.True(read.IsPrimaryRelease);

        var updated = await handler.Update(
            new UpdateReleaseTrackCommand(
                release.Id,
                track.Id,
                DiscNumber: 2,
                TrackNumber: 4,
                TitleOverride: null,
                IsPrimaryRelease: false));

        Assert.NotNull(updated);
        Assert.Equal(2, updated.DiscNumber);
        Assert.Equal(4, updated.TrackNumber);
        Assert.Equal(track.Title, updated.Title);
        Assert.Null(track.AlbumId);

        var deleted = await handler.Delete(release.Id, track.Id);

        Assert.True(deleted);
        Assert.Equal(0, release.NumOfTracks);
        Assert.Empty(dbContext.AlbumTracks);
    }

    [Fact]
    public async Task CreateRejectsTrackWhoseLeadArtistDoesNotMatchRelease()
    {
        await using var dbContext = CreateDbContext();
        var (release, track) = await SeedCatalog(dbContext);
        dbContext.ArtistTracks.Single().ArtistId = release.ArtistId + 1;
        await dbContext.SaveChangesAsync();
        var handler = new ReleaseTrackHandler(dbContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Create(
            new CreateReleaseTrackCommand(
                release.Id,
                track.Id,
                DiscNumber: 1,
                TrackNumber: 1,
                TitleOverride: null,
                IsPrimaryRelease: true)));

        Assert.Contains("lead artist", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(dbContext.AlbumTracks);
    }

    [Fact]
    public async Task CreateAllowsArtistMismatchWhenExplicitlyAuthorizedForAdmin()
    {
        await using var dbContext = CreateDbContext();
        var (release, track) = await SeedCatalog(dbContext);
        dbContext.ArtistTracks.Single().ArtistId = release.ArtistId + 1;
        await dbContext.SaveChangesAsync();
        var handler = new ReleaseTrackHandler(dbContext);

        var result = await handler.Create(
            new CreateReleaseTrackCommand(
                release.Id,
                track.Id,
                DiscNumber: 1,
                TrackNumber: 1,
                TitleOverride: null,
                IsPrimaryRelease: true,
                AllowArtistMismatch: true));

        Assert.Equal(release.Id, result.ReleaseId);
        Assert.Equal(track.Id, result.TrackId);
        Assert.Equal(release.Id, track.AlbumId);
    }

    [Fact]
    public async Task CreateRejectsAnOccupiedDiscAndTrackNumber()
    {
        await using var dbContext = CreateDbContext();
        var (release, firstTrack) = await SeedCatalog(dbContext);
        var secondTrack = new Track { Title = "Second", TrackPath = "tracks/second.mp3" };
        dbContext.Tracks.Add(secondTrack);
        dbContext.ArtistTracks.Add(new ArtistTrack
        {
            ArtistId = release.ArtistId,
            Track = secondTrack,
            IsLead = true,
            ShowOnProfile = true
        });
        await dbContext.SaveChangesAsync();

        var handler = new ReleaseTrackHandler(dbContext);
        await handler.Create(new CreateReleaseTrackCommand(
            release.Id, firstTrack.Id, 1, 1, null, true));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Create(
            new CreateReleaseTrackCommand(release.Id, secondTrack.Id, 1, 1, null, false)));

        Assert.Contains("disc and track number", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListIncludesLegacyAlbumIdTracksWithoutAssociations()
    {
        await using var dbContext = CreateDbContext();
        var (release, track) = await SeedCatalog(dbContext);
        track.AlbumId = release.Id;
        await dbContext.SaveChangesAsync();
        var handler = new ReleaseTrackHandler(dbContext);

        var result = await handler.List(new ReleaseTrackListQuery(release.Id));

        Assert.NotNull(result);
        var listedTrack = Assert.Single(result.Items);
        Assert.Null(listedTrack.AssociationId);
        Assert.True(listedTrack.IsLegacyAssociation);
        Assert.True(listedTrack.IsPrimaryRelease);
        Assert.Equal(track.Id, listedTrack.TrackId);
        Assert.Equal(release.Id, listedTrack.ReleaseId);
        Assert.Single(listedTrack.Artists);
    }

    [Fact]
    public async Task ListPaginatesAssociatedAndLegacyTracksOnTheBackend()
    {
        await using var dbContext = CreateDbContext();
        var (release, firstTrack) = await SeedCatalog(dbContext);
        firstTrack.AlbumId = release.Id;

        for (var index = 2; index <= 5; index++)
        {
            var track = new Track
            {
                Title = $"Track {index}",
                AlbumId = release.Id,
                TrackPath = $"tracks/{index}.mp3"
            };
            dbContext.Tracks.Add(track);
            dbContext.ArtistTracks.Add(new ArtistTrack
            {
                ArtistId = release.ArtistId,
                Track = track,
                IsLead = true,
                ShowOnProfile = true
            });
        }

        await dbContext.SaveChangesAsync();
        var handler = new ReleaseTrackHandler(dbContext);

        var result = await handler.List(new ReleaseTrackListQuery(
            release.Id,
            PageNumber: 2,
            PageSize: 2));

        Assert.NotNull(result);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.Items.Count);
        Assert.True(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public async Task ReorderChangesTrackAndDiscNumbersInOneRequest()
    {
        await using var dbContext = CreateDbContext();
        var (release, firstTrack) = await SeedCatalog(dbContext);
        var secondTrack = new Track { Title = "Second", TrackPath = "tracks/second.mp3" };
        var thirdTrack = new Track { Title = "Third", TrackPath = "tracks/third.mp3" };
        dbContext.Tracks.AddRange(secondTrack, thirdTrack);
        dbContext.ArtistTracks.AddRange(
            new ArtistTrack
            {
                ArtistId = release.ArtistId,
                Track = secondTrack,
                IsLead = true,
                ShowOnProfile = true
            },
            new ArtistTrack
            {
                ArtistId = release.ArtistId,
                Track = thirdTrack,
                IsLead = true,
                ShowOnProfile = true
            });
        await dbContext.SaveChangesAsync();

        var handler = new ReleaseTrackHandler(dbContext);
        await handler.Create(new CreateReleaseTrackCommand(
            release.Id, firstTrack.Id, 1, 1, null, true));
        await handler.Create(new CreateReleaseTrackCommand(
            release.Id, secondTrack.Id, 1, 2, null, true));
        await handler.Create(new CreateReleaseTrackCommand(
            release.Id, thirdTrack.Id, 1, 3, null, true));

        var reordered = await handler.Reorder(new ReorderReleaseTracksCommand(
            release.Id,
            [
                new ReleaseTrackPosition(thirdTrack.Id, 1, 1),
                new ReleaseTrackPosition(firstTrack.Id, 1, 2),
                new ReleaseTrackPosition(secondTrack.Id, 2, 1)
            ]));

        Assert.True(reordered);
        var positions = await dbContext.AlbumTracks
            .Where(x => x.AlbumId == release.Id)
            .ToDictionaryAsync(x => x.TrackId);
        Assert.Equal((1, 1), (positions[thirdTrack.Id].DiscNumber, positions[thirdTrack.Id].TrackNumber));
        Assert.Equal((1, 2), (positions[firstTrack.Id].DiscNumber, positions[firstTrack.Id].TrackNumber));
        Assert.Equal((2, 1), (positions[secondTrack.Id].DiscNumber, positions[secondTrack.Id].TrackNumber));
    }

    [Fact]
    public async Task ReorderMaterializesLegacyAssociationsAndRejectsPartialRequests()
    {
        await using var dbContext = CreateDbContext();
        var (release, firstTrack) = await SeedCatalog(dbContext);
        firstTrack.AlbumId = release.Id;
        var secondTrack = new Track
        {
            Title = "Second",
            TrackPath = "tracks/second.mp3",
            AlbumId = release.Id
        };
        dbContext.Tracks.Add(secondTrack);
        dbContext.ArtistTracks.Add(new ArtistTrack
        {
            ArtistId = release.ArtistId,
            Track = secondTrack,
            IsLead = true,
            ShowOnProfile = true
        });
        await dbContext.SaveChangesAsync();
        var handler = new ReleaseTrackHandler(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Reorder(
            new ReorderReleaseTracksCommand(
                release.Id,
                [new ReleaseTrackPosition(firstTrack.Id, 1, 1)])));

        var reordered = await handler.Reorder(new ReorderReleaseTracksCommand(
            release.Id,
            [
                new ReleaseTrackPosition(secondTrack.Id, 1, 1),
                new ReleaseTrackPosition(firstTrack.Id, 2, 1)
            ]));

        Assert.True(reordered);
        Assert.Equal(2, await dbContext.AlbumTracks.CountAsync());
        Assert.All(dbContext.AlbumTracks, association => Assert.True(association.IsPrimaryRelease));
    }

    private static MusicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new MusicDbContext(options);
    }

    private static async Task<(Album Release, Track Track)> SeedCatalog(MusicDbContext dbContext)
    {
        var artist = new Artist { Name = "Catalog artist" };
        var albumType = new AlbumType { Type = "Album" };
        var release = new Album
        {
            Title = "Catalog release",
            Artist = artist,
            AlbumType = albumType,
            ReleaseDate = DateTime.UtcNow,
            IsActive = true
        };
        var track = new Track
        {
            Title = "Original title",
            Length = 30,
            TrackPath = "tracks/original.mp3"
        };

        dbContext.AddRange(artist, albumType, release, track);
        dbContext.ArtistTracks.Add(new ArtistTrack
        {
            Artist = artist,
            Track = track,
            IsLead = true,
            ShowOnProfile = true
        });
        await dbContext.SaveChangesAsync();

        return (release, track);
    }
}
