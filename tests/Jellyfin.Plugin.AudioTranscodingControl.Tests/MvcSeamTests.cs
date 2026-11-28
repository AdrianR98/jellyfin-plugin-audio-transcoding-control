// SPDX-License-Identifier: GPL-3.0-only
using System.Net;
using System.Net.Http.Json;
using Jellyfin.Plugin.AudioTranscodingControl.Filters;
using MediaBrowser.Model.Dlna;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Jellyfin.Plugin.AudioTranscodingControl.Tests;

/// <summary>Exercises real MVC binding in memory; this is not a Jellyfin server integration test.</summary>
public sealed class MvcSeamTests
{
    [Fact]
    public void RegistratorAddsScopedFilterAfterHostConfiguration()
    {
        var services = new ServiceCollection();
        HostFixture.RegisterUnusedHostServices(services);
        new PluginServiceRegistrator().RegisterServices(services, null!);
        services.Configure<MvcOptions>(options => options.Filters.Clear());
        using var provider = services.BuildServiceProvider();
        var filters = provider.GetRequiredService<IOptions<MvcOptions>>().Value.Filters.OfType<ServiceFilterAttribute>().ToArray();
        var filter = Assert.Single(filters, f => f.ServiceType == typeof(PlaybackInfoActionFilter));
        Assert.Single(filters, f => f.ServiceType == typeof(ProgressiveAudioActionFilter));
        Assert.Equal(typeof(PlaybackInfoActionFilter), filter.ServiceType);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        Assert.Same(first.ServiceProvider.GetRequiredService<PlaybackInfoActionFilter>(), first.ServiceProvider.GetRequiredService<PlaybackInfoActionFilter>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<PlaybackInfoActionFilter>(), second.ServiceProvider.GetRequiredService<PlaybackInfoActionFilter>());
    }

    [Theory]
    [InlineData("/Items/fixture/PlaybackInfo")]
    [InlineData("/unrelated")]
    public async Task M0LeavesBoundProfileAndQueryPrecedenceUnchanged(string path)
    {
        using var server = CreateServer(mutationProbe: false);
        using var client = server.CreateClient();
        using var response = await client.PostAsJsonAsync(path + "?maxAudioChannels=2", Request());
        response.EnsureSuccessStatusCode();
        var actual = await response.Content.ReadFromJsonAsync<ProbeResponse>();
        Assert.Equal(new ProbeResponse("aac", 2, 8), actual);
    }

    [Fact]
    public async Task TestOnlyFilterCanReplaceBoundProfileBeforeControllerRuns()
    {
        using var server = CreateServer(mutationProbe: true);
        using var client = server.CreateClient();
        using var response = await client.PostAsJsonAsync("/Items/fixture/PlaybackInfo", Request());
        response.EnsureSuccessStatusCode();
        var actual = await response.Content.ReadFromJsonAsync<ProbeResponse>();
        Assert.Equal(new ProbeResponse("ac3", 6, 8), actual);
    }

    [Fact]
    public async Task InvalidModelStillReturnsBadRequest()
    {
        using var server = CreateServer(mutationProbe: false);
        using var client = server.CreateClient();
        using var body = new StringContent("{\"maxAudioChannels\":\"invalid\"}", System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/Items/fixture/PlaybackInfo", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task M0DoesNotSwallowDownstreamExceptions()
    {
        var action = new ActionContext(new DefaultHttpContext(), new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());
        var context = new ActionExecutingContext(action, [], new Dictionary<string, object?>(), new object());
        var expected = new InvalidOperationException("test sentinel");
        var calls = 0;
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => new PlaybackInfoActionFilter(null!, null!, null!, null!, null!, Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaybackInfoActionFilter>.Instance, null!, null!).OnActionExecutionAsync(context, () =>
        {
            calls++;
            throw expected;
        }));
        Assert.Same(expected, actual);
        Assert.Equal(1, calls);
    }

    private static ProbeRequest Request() => new()
    {
        MaxAudioChannels = 8,
        DeviceProfile = new DeviceProfile
        {
            TranscodingProfiles = [new TranscodingProfile { AudioCodec = "aac", Type = DlnaProfileType.Video }],
        },
    };

    private static TestServer CreateServer(bool mutationProbe) => new(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            HostFixture.RegisterUnusedHostServices(services);
            new PluginServiceRegistrator().RegisterServices(services, null!);
            services.AddControllers().AddApplicationPart(typeof(ProbeController).Assembly);
            if (mutationProbe)
            {
                services.Configure<MvcOptions>(options => options.Filters.Add<BoundArgumentMutationProbe>());
            }
        })
        .Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));
}

// Synthetic DTO with Jellyfin.Model's real DeviceProfile; no copied Jellyfin.Api source or DLL.
public sealed class ProbeRequest
{
    public DeviceProfile? DeviceProfile { get; set; }
    public int? MaxAudioChannels { get; set; }
}

public sealed record ProbeResponse(string? Codec, int? EffectiveMaxChannels, int? BodyMaxChannels);

[ApiController]
public sealed class ProbeController : ControllerBase
{
    [HttpPost("Items/{itemId}/PlaybackInfo")]
    [HttpPost("unrelated")]
    public ProbeResponse PlaybackInfo([FromBody] ProbeRequest playbackInfoDto, [FromQuery] int? maxAudioChannels) =>
        new(playbackInfoDto.DeviceProfile?.TranscodingProfiles.FirstOrDefault()?.AudioCodec,
            maxAudioChannels ?? playbackInfoDto.MaxAudioChannels,
            playbackInfoDto.MaxAudioChannels);
}

// Only in the test assembly: proves the MVC hook, not the future rule implementation.
public sealed class BoundArgumentMutationProbe : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var dto = (ProbeRequest)context.ActionArguments["playbackInfoDto"]!;
        dto.DeviceProfile = new DeviceProfile
        {
            TranscodingProfiles = [new TranscodingProfile { AudioCodec = "ac3", Type = DlnaProfileType.Video }],
        };
        context.ActionArguments["maxAudioChannels"] = 6;
        await next();
    }
}
