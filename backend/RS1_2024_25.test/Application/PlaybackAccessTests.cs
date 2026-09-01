using _808Music.Application.Playback;
using _808Music.Application.Tracks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using RS1_2024_25.API.Controllers.V2;
using RS1_2024_25.API.Data;
using RS1_2024_25.API.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace RS1_2024_25.Tests.Application;

public sealed class PlaybackAccessTests
{
    [Fact]
    public async Task Authenticated_listener_without_subscription_can_stream()
    {
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var legacyDbContext = new ApplicationDbContext(dbOptions);

        var manifest = new GetTrackPlaybackManifestResult(
            new PlaybackTrackDto(42, "Track", false, 180, 0, []),
            new PlaybackStreamDto(
                DateTimeOffset.UtcNow.AddHours(1),
                new PlaybackAssetDto(
                    "Master",
                    "audio/mpeg",
                    new Uri("https://media.example/track.mp3")),
                null));

        var manifestHandler = new Mock<IGetTrackPlaybackManifestHandler>();
        manifestHandler
            .Setup(handler => handler.Handle(
                It.IsAny<GetTrackPlaybackManifestQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);

        var trackAccess = new Mock<ITrackArtistAccessQuery>();
        trackAccess
            .Setup(query => query.GetLeadArtistId(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .Build();
        var tokenProvider = new TokenProvider(configuration, legacyDbContext);
        var controller = new PlaybackController(
            manifestHandler.Object,
            trackAccess.Object,
            legacyDbContext,
            tokenProvider,
            configuration);

        var userId = "123";
        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(ClaimTypes.Role, "None")
            ]));
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, "None")
            ], "test"))
        };
        httpContext.Request.Headers.Authorization = $"Bearer {token}";
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var response = await controller.Get(42, false, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Same(manifest, okResult.Value);
    }
}
