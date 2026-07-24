using _808Music.Application.Abstractions;
using _808Music.Application.Stems;
using _808Music.Domain.Catalog;
using _808Music.Domain.Enums;
using _808Music.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace RS1_2024_25.Tests.Application;

public sealed class TrackStemSetManagementTests
{
    [Fact]
    public async Task Activating_a_ready_set_deactivates_the_previous_set()
    {
        await using var db = CreateDbContext();
        var track = new Track { Title = "Track", TrackPath = "tracks/master.wav" };
        db.Tracks.Add(track);
        await db.SaveChangesAsync();
        var current = CreateReadySet(track.Id, StemSetSource.ArtistUploaded, "manual");
        var replacement = CreateReadySet(track.Id, StemSetSource.AiGenerated, "ai");
        current.Activate();
        db.TrackStemSets.AddRange(current, replacement);
        await db.SaveChangesAsync();
        var storage = CreateStorage();

        var result = await new ManageTrackStemSetsHandler(db, storage.Object).Activate(
            new ActivateTrackStemSetCommand(track.Id, replacement.Id));

        Assert.NotNull(result);
        Assert.True(replacement.IsActive);
        Assert.False(current.IsActive);
    }

    [Fact]
    public async Task Deleting_the_active_set_activates_a_ready_fallback_and_cleans_storage()
    {
        await using var db = CreateDbContext();
        var track = new Track { Title = "Track", TrackPath = "tracks/master.wav" };
        db.Tracks.Add(track);
        await db.SaveChangesAsync();
        var active = CreateReadySet(track.Id, StemSetSource.AiGenerated, "active");
        var fallback = CreateReadySet(track.Id, StemSetSource.ArtistUploaded, "fallback");
        active.Activate();
        db.TrackStemSets.AddRange(active, fallback);
        await db.SaveChangesAsync();
        var storage = CreateStorage();

        var deleted = await new ManageTrackStemSetsHandler(db, storage.Object).Delete(
            new DeleteTrackStemSetCommand(track.Id, active.Id));

        Assert.True(deleted);
        Assert.True(fallback.IsActive);
        Assert.DoesNotContain(db.TrackStemSets, x => x.Id == active.Id);
        storage.Verify(
            x => x.DeleteAsync(It.Is<string>(key => key.StartsWith("active/")), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Deleting_a_processing_set_is_rejected_as_a_state_conflict()
    {
        await using var db = CreateDbContext();
        var track = new Track { Title = "Track", TrackPath = "tracks/master.wav" };
        db.Tracks.Add(track);
        await db.SaveChangesAsync();
        var processing = new TrackStemSet(
            track.Id,
            StemSetSource.AiGenerated,
            null,
            "test",
            null,
            null,
            "two-stem-vocals");
        processing.MarkProcessing();
        db.TrackStemSets.Add(processing);
        await db.SaveChangesAsync();

        var handler = new ManageTrackStemSetsHandler(db, CreateStorage().Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Delete(
            new DeleteTrackStemSetCommand(track.Id, processing.Id)));
        Assert.Contains(db.TrackStemSets, x => x.Id == processing.Id);
    }

    private static TrackStemSet CreateReadySet(int trackId, StemSetSource source, string keyPrefix)
    {
        var set = new TrackStemSet(trackId, source, null, "test", null, null, "two-stem-vocals");
        set.AddStem(CreateStem(set.Id, StemType.Vocals, $"{keyPrefix}/vocals.wav"));
        set.AddStem(CreateStem(set.Id, StemType.Instrumental, $"{keyPrefix}/instrumental.wav"));
        set.MarkReady();
        return set;
    }

    private static TrackStem CreateStem(Guid setId, StemType type, string key) =>
        new(setId, type, "test", key, "audio/wav", 100, null, null, null, null, null, null);

    private static Mock<IMediaStorage> CreateStorage()
    {
        var storage = new Mock<IMediaStorage>();
        storage.Setup(x => x.CreateReadUrlAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, TimeSpan _, CancellationToken _) => new Uri($"https://media.test/{key}"));
        storage.Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return storage;
    }

    private static MusicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MusicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new MusicDbContext(options);
    }
}
