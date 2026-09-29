using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using RS1_2024_25.API.Endpoints.PlaylistEndpoints;

namespace RS1_2024_25.Tests.Application;

public sealed class PlaylistEndpointDiscoveryTests
{
    [Fact]
    public void Add_to_liked_songs_endpoint_is_discovered_as_a_controller()
    {
        var manager = new ApplicationPartManager();
        manager.ApplicationParts.Add(
            new AssemblyPart(typeof(AddTrackToLikedSongsEndpoint).Assembly));
        manager.FeatureProviders.Add(new ControllerFeatureProvider());

        var feature = new ControllerFeature();
        manager.PopulateFeature(feature);

        Assert.Contains(
            feature.Controllers,
            controller => controller.AsType() == typeof(AddTrackToLikedSongsEndpoint));
    }
}
