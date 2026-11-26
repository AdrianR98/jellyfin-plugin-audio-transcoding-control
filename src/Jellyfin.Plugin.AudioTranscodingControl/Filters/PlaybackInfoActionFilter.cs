// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jellyfin.Plugin.AudioTranscodingControl.Filters;

/// <summary>
/// Inert extension seam. M1 may adapt a matched PlaybackInfo request before calling the action.
/// M0 always invokes the remaining pipeline exactly once without reading or changing its data.
/// </summary>
public sealed class PlaybackInfoActionFilter : IAsyncActionFilter
{
    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await next().ConfigureAwait(false);
    }
}
