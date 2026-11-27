// SPDX-License-Identifier: GPL-3.0-only
using Jellyfin.Plugin.AudioTranscodingControl.Controllers;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.AudioTranscodingControl.Tests;

public sealed class UiDiagnosticsTests
{
    [Fact]
    public void BridgeRequiresNativeAdminPolicyAndRejectsArbitraryClientText()
    {
        var policy = Assert.Single(typeof(UiDiagnosticsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(Policies.RequiresElevation, policy.Policy);
        var controller = new UiDiagnosticsController(NullLogger<UiDiagnosticsController>.Instance);
        var entry = new UiDiagnosticEvent { Event = "error", Step = "configuration", Code = "invalid-schema", Kind = "TypeError", Sequence = 1, Frames = ["normalize:123:4"] };
        var batch = new UiDiagnosticBatch { SessionId = Guid.NewGuid(), Events = [entry] };
        Assert.IsType<NoContentResult>(controller.Post(batch));
        entry.Frames = ["https://sentinel.invalid/private"];
        Assert.IsType<BadRequestResult>(controller.Post(batch));
        entry.Frames = []; entry.Element = "arbitrary-client-text";
        Assert.IsType<BadRequestResult>(controller.Post(batch));
        entry.Element = "none"; entry.Event = "arbitrary-client-text";
        Assert.IsType<BadRequestResult>(controller.Post(batch));
        entry.Event = "error"; batch.Events = Enumerable.Repeat(entry, 33).ToArray();
        Assert.IsType<BadRequestResult>(controller.Post(batch));
    }
}
