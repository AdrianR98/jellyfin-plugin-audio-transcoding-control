// SPDX-License-Identifier: GPL-3.0-only
using Jellyfin.Plugin.AudioTranscodingControl.Filters;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.AudioTranscodingControl;

/// <summary>Registers the inert M0 action-filter seam with the host MVC pipeline.</summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddScoped<PlaybackInfoActionFilter>();
        serviceCollection.PostConfigure<MvcOptions>(options =>
            options.Filters.AddService<PlaybackInfoActionFilter>());
    }
}
