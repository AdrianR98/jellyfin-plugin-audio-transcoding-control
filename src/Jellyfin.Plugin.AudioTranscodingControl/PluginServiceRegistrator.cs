// SPDX-License-Identifier: GPL-3.0-only
using Jellyfin.Plugin.AudioTranscodingControl.Filters;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.AudioTranscodingControl;

/// <summary>Registers the request-scoped AC3 filter with the host MVC pipeline.</summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.Configure<PluginConfiguration>(options =>
        {
            var saved = Plugin.Instance?.Configuration;
            options.IsEnabled = saved?.IsEnabled ?? false;
            options.EnableDebugLogging = saved?.EnableDebugLogging ?? false;
            options.Ac3Rule = new Ac3DeviceRule
            {
                IsEnabled = saved?.Ac3Rule?.IsEnabled ?? false,
                DeviceId = saved?.Ac3Rule?.DeviceId ?? string.Empty,
                UserId = saved?.Ac3Rule?.UserId ?? Guid.Empty,
            };
        });
        serviceCollection.AddScoped<PlaybackInfoActionFilter>();
        serviceCollection.PostConfigure<MvcOptions>(options =>
            options.Filters.AddService<PlaybackInfoActionFilter>());
    }
}
