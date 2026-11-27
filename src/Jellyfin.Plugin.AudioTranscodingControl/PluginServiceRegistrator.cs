// SPDX-License-Identifier: GPL-3.0-only
using Jellyfin.Plugin.AudioTranscodingControl.Filters;
using Jellyfin.Plugin.AudioTranscodingControl.Configuration;
using Jellyfin.Plugin.AudioTranscodingControl.Policy;
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
            var saved = Plugin.Instance?.GetConfigurationSnapshot();
            if (saved is not null) ConfigurationMigration.CopyInto(saved, options);
        });
        serviceCollection.AddScoped<PlaybackInfoActionFilter>();
        serviceCollection.AddSingleton<PlaybackPolicySessions>();
        serviceCollection.AddScoped<ProgressiveAudioActionFilter>();
        serviceCollection.PostConfigure<MvcOptions>(options =>
        {
            options.Filters.AddService<PlaybackInfoActionFilter>();
            options.Filters.AddService<ProgressiveAudioActionFilter>();
        });
    }
}
