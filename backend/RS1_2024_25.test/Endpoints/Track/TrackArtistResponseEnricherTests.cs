using Microsoft.EntityFrameworkCore;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Data.Models;
using RS1_2024_25.API.Endpoints.TrackEndpoints;

namespace RS1_2024_25.Tests.Endpoints.Track;

public sealed class TrackArtistResponseEnricherTests
{
    [Fact]
    public async Task MissingTrackCreditsFallBackToTheAlbumArtist()
    {
        await using var db = CreateDbContext();
        var artist = new RS1_2024_25.API.Data.Models.Artist
        {
            Name = "Album artist",
            ProfilePhotoPath = "artist.jpg"
        };
        var album = new Album { Title = "Album", Artist = artist };
        var track = new RS1_2024_25.API.Data.Models.Track { Title = "Track", Album = album };
        db.AddRange(artist, album, track);
        await db.SaveChangesAsync();

        var response = new TrackGetResponse { Id = track.Id, Artists = [] };

        await TrackArtistResponseEnricher.AddAlbumArtistFallbacksAsync(db, [response]);

        var fallback = Assert.Single(response.Artists);
        Assert.Equal(artist.Id, fallback.Id);
        Assert.Equal("Album artist", fallback.Name);
        Assert.True(fallback.IsLead);
    }

    [Fact]
    public async Task ExplicitTrackCreditsAreNotReplaced()
    {
        await using var db = CreateDbContext();
        var response = new TrackGetResponse
        {
            Id = 99,
            Artists = [new ArtistTrackDto { Id = 7, Name = "Track artist", IsLead = true }]
        };

        await TrackArtistResponseEnricher.AddAlbumArtistFallbacksAsync(db, [response]);

        var artist = Assert.Single(response.Artists);
        Assert.Equal(7, artist.Id);
        Assert.Equal("Track artist", artist.Name);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
