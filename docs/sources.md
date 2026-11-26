# Versionsgebundene Quellen

M1 ergänzt die bestehenden Quellen um das offizielle portable 10.11.11-Paket und tatsächliche Laufzeitbelege: [Binäridentität, Hashes und nativer Servernachweis](m1-verification.md). Der in dessen Jellyfin.Api-Assembly vermerkte Source-Commit ist identisch mit dem hier analysierten Server-Commit. Die M0-Quellen wurden nicht erneut vollständig recherchiert.

Geprüft am 26.11.2026. Analysiert wurden lokale, flache Git-Checkouts der folgenden unveränderlichen Commits. Keine Analyse gegen bewegliches `master`/`main` als Versionsbeleg.

| Quelle | Ref bei Abruf | Vollständiger Commit |
| --- | --- | --- |
| [jellyfin/jellyfin](https://github.com/jellyfin/jellyfin/tree/1fbd8739292cce610231be93daf43368733edf63) | v10.11.11 | `1fbd8739292cce610231be93daf43368733edf63` |
| [jellyfin/jellyfin-androidtv](https://github.com/jellyfin/jellyfin-androidtv/tree/45c52925661ff51427cea46ebabcd3de656276cc) | v0.20.0 | `45c52925661ff51427cea46ebabcd3de656276cc` |
| [jellyfin/jellyfin-plugin-template](https://github.com/jellyfin/jellyfin-plugin-template/tree/c93225a0a5a76d3843db05b4b5b77fcfc482fba3) | Default-Branch, Snapshot | `c93225a0a5a76d3843db05b4b5b77fcfc482fba3` |
| [upchui/Jellyfin-DirectPlayForce](https://github.com/upchui/Jellyfin-DirectPlayForce/tree/4b6abc8325f3b7ad8a7c90e062dd52154dd60547) | Default-Branch, Snapshot | `4b6abc8325f3b7ad8a7c90e062dd52154dd60547` |

Die S-Nummern werden in der Analyse als Belege verwendet. Jede Fundstelle führt auf den fixierten Commit und die geprüfte Zeile. Methodennamen im Linktext können auch Aufrufstellen bezeichnen; der umgebende Code liefert den Zusammenhang.

<a id="s01"></a>
## S01 — Server-Framework und NuGet-Identitäten

- `MediaBrowser.Controller/MediaBrowser.Controller.csproj`: [&lt;PackageId&gt; — L10](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaBrowser.Controller.csproj#L10); [&lt;VersionPrefix&gt; — L11](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaBrowser.Controller.csproj#L11); [&lt;TargetFramework&gt; — L39](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaBrowser.Controller.csproj#L39); [&lt;PackageLicenseExpression&gt; — L13](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaBrowser.Controller.csproj#L13).
- `MediaBrowser.Model/MediaBrowser.Model.csproj`: [&lt;PackageId&gt; — L10](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/MediaBrowser.Model.csproj#L10); [&lt;VersionPrefix&gt; — L11](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/MediaBrowser.Model.csproj#L11); [&lt;TargetFramework&gt; — L17](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/MediaBrowser.Model.csproj#L17); [&lt;PackageLicenseExpression&gt; — L13](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/MediaBrowser.Model.csproj#L13).
- `MediaBrowser.Common/MediaBrowser.Common.csproj`: [&lt;PackageId&gt; — L10](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Common/MediaBrowser.Common.csproj#L10); [&lt;FrameworkReference — L17](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Common/MediaBrowser.Common.csproj#L17).
- `Jellyfin.Api/Jellyfin.Api.csproj`: [&lt;TargetFramework&gt; — L9](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Jellyfin.Api.csproj#L9).
- `global.json`: ["version" — L3](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/global.json#L3).

<a id="s02"></a>
## S02 — PlaybackInfo-Controller

- `Jellyfin.Api/Controllers/MediaInfoController.cs`: [GetPlaybackInfo( — L75](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/MediaInfoController.cs#L75); [GetPostedPlaybackInfo( — L119](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/MediaInfoController.cs#L119); [var profile = — L137](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/MediaInfoController.cs#L137); [userId ??= — L151](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/MediaInfoController.cs#L151); [_mediaInfoHelper.SetDeviceSpecificData( — L194](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/MediaInfoController.cs#L194); [OpenLiveStream( — L271](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/MediaInfoController.cs#L271).

<a id="s03"></a>
## S03 — Gebundenes PlaybackInfoDto

- `Jellyfin.Api/Models/MediaInfoDtos/PlaybackInfoDto.cs`: [public class — L9](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Models/MediaInfoDtos/PlaybackInfoDto.cs#L9); [DeviceProfile? — L54](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Models/MediaInfoDtos/PlaybackInfoDto.cs#L54); [MaxAudioChannels — L39](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Models/MediaInfoDtos/PlaybackInfoDto.cs#L39); [AllowVideoStreamCopy — L74](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Models/MediaInfoDtos/PlaybackInfoDto.cs#L74).

<a id="s04"></a>
## S04 — Wiedergabeentscheidung und Response

- `Jellyfin.Api/Helpers/MediaInfoHelper.cs`: [GetPlaybackInfo( — L87](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L87); [SetDeviceSpecificData( — L170](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L170); [options.EnableDirectStream = false — L252](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L252); [GetOptimalAudioStream — L257](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L257); [EnableAudioPlaybackTranscoding — L233](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L233); [mediaSource.TranscodingUrl = streamInfo.ToUrl — L300](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L300); [OpenMediaSource( — L405](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/MediaInfoHelper.cs#L405).

<a id="s05"></a>
## S05 — Profilmodelle

- `MediaBrowser.Model/Dlna/DeviceProfile.cs`: [public class — L15](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/DeviceProfile.cs#L15).
- `MediaBrowser.Model/Dlna/TranscodingProfile.cs`: [public class — L12](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/TranscodingProfile.cs#L12); [public TranscodingProfile(TranscodingProfile — L26](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/TranscodingProfile.cs#L26); [MaxAudioChannels — L41](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/TranscodingProfile.cs#L41).
- `MediaBrowser.Model/Dlna/CodecProfile.cs`: [public class — L12](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/CodecProfile.cs#L12); [ApplyConditions — L20](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/CodecProfile.cs#L20).
- `MediaBrowser.Model/Dlna/DirectPlayProfile.cs`: [public class — L9](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/DirectPlayProfile.cs#L9).

<a id="s06"></a>
## S06 — StreamBuilder

- `MediaBrowser.Model/Dlna/StreamBuilder.cs`: [_supportedHlsAudioCodecsTs — L32](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamBuilder.cs#L32); [GetOptimalAudioStream( — L51](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamBuilder.cs#L51); [SetStreamInfoOptionsFromTranscodingProfile( — L184](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamBuilder.cs#L184); [GetVideoTranscodeProfile( — L794](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamBuilder.cs#L794); [BuildStreamVideoItem( — L766](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamBuilder.cs#L766); [GetAudioBitrate( — L1076](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamBuilder.cs#L1076); [GetVideoDirectPlayProfile( — L733](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamBuilder.cs#L733); [ApplyTranscodingConditions( — L192](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamBuilder.cs#L192).

<a id="s07"></a>
## S07 — StreamInfo und URL-Serialisierung

- `MediaBrowser.Model/Dlna/StreamInfo.cs`: [TargetAudioCodec — L330](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamInfo.cs#L330); [ToUrl( — L882](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamInfo.cs#L882); [foreach (var pair in StreamOptions) — L1123](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamInfo.cs#L1123); [GetTargetAudioChannels( — L1355](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Model/Dlna/StreamInfo.cs#L1355).

<a id="s08"></a>
## S08 — Streaming-Request zu Encoding-State

- `Jellyfin.Api/Helpers/StreamingHelpers.cs`: [GetStreamingState( — L46](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/StreamingHelpers.cs#L46); [state.SupportedAudioCodecs = — L99](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/StreamingHelpers.cs#L99); [encodingHelper.AttachMediaSourceInfo( — L157](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/StreamingHelpers.cs#L157); [state.OutputAudioChannels = — L183](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/StreamingHelpers.cs#L183); [encodingHelper.TryStreamCopy( — L203](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/StreamingHelpers.cs#L203); [ParseStreamOptions( — L65](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/StreamingHelpers.cs#L65).

<a id="s09"></a>
## S09 — HLS-Endpunkte und FFmpeg-Argumente

- `Jellyfin.Api/Controllers/DynamicHlsController.cs`: [GetMasterHlsVideoPlaylist( — L412](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/DynamicHlsController.cs#L412); [GetMasterHlsAudioPlaylist( — L588](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/DynamicHlsController.cs#L588); [GetHlsVideoSegment( — L1106](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/DynamicHlsController.cs#L1106); [GetCommandLineArguments( — L315](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/DynamicHlsController.cs#L315); [GetAudioArguments( — L1673](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/DynamicHlsController.cs#L1673); [GetVideoArguments( — L1672](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/DynamicHlsController.cs#L1672).

<a id="s10"></a>
## S10 — HLS-Master- und Folge-URLs

- `Jellyfin.Api/Helpers/DynamicHlsHelper.cs`: [GetMasterHlsPlaylist( — L96](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/DynamicHlsHelper.cs#L96); [newQuery["AudioCodec"] — L158](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/DynamicHlsHelper.cs#L158); [var playlistUrl = — L189](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/DynamicHlsHelper.cs#L189); [GetPlaylistAudioCodecs( — L422](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Helpers/DynamicHlsHelper.cs#L422).

<a id="s11"></a>
## S11 — EncodingHelper

- `MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs`: [_audioTranscodeChannelLookup — L130](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs#L130); [GetAudioEncoder( — L746](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs#L746); [CanStreamCopyVideo( — L2339](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs#L2339); [CanStreamCopyAudio( — L2543](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs#L2543); [GetAudioBitrateParam(int? — L2718](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs#L2718); [GetNumAudioChannelsParam( — L2865](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs#L2865); [TryStreamCopy( — L7129](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs#L7129); [AttachMediaSourceInfo( — L7328](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs#L7328); [ShiftAudioCodecsIfNeeded( — L7422](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs#L7422); [GetProgressiveVideoAudioArguments( — L7581](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingHelper.cs#L7581).

<a id="s12"></a>
## S12 — Kanalparameter im Encoding-State

- `MediaBrowser.Controller/MediaEncoding/EncodingJobInfo.cs`: [GetRequestedAudioChannels( — L719](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/MediaEncoding/EncodingJobInfo.cs#L719).

<a id="s13"></a>
## S13 — Statisches und progressives Video

- `Jellyfin.Api/Controllers/VideosController.cs`: [GetVideoStream( — L316](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/VideosController.cs#L316); [// Static stream — L467](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/VideosController.cs#L467); [GetProgressiveVideoFullCommandLine — L485](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/VideosController.cs#L485).

<a id="s14"></a>
## S14 — Reines Audio und Universal-Audio

- `Jellyfin.Api/Controllers/AudioController.cs`: [public class — L19](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/AudioController.cs#L19); [AudioCodec = audioCodec — L158](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/AudioController.cs#L158).
- `Jellyfin.Api/Controllers/UniversalAudioController.cs`: [public class — L32](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/UniversalAudioController.cs#L32); [var isStatic = — L182](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/UniversalAudioController.cs#L182); [AudioCodec = mediaSource.TranscodeReasons — L207](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/UniversalAudioController.cs#L207).

<a id="s15"></a>
## S15 — Plugin-DI und MVC

- `MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs`: [public interface — L11](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs#L11); [void RegisterServices — L18](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs#L18).
- `Emby.Server.Implementations/Plugins/PluginManager.cs`: [public void RegisterServices — L206](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Emby.Server.Implementations/Plugins/PluginManager.cs#L206); [Activator.CreateInstance(pluginServiceRegistrator) — L225](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Emby.Server.Implementations/Plugins/PluginManager.cs#L225).
- `Emby.Server.Implementations/ApplicationHost.cs`: [_pluginManager.RegisterServices — L462](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Emby.Server.Implementations/ApplicationHost.cs#L462).
- `Jellyfin.Server/Extensions/ApiServiceCollectionExtensions.cs`: [AddJellyfinApi( — L114](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server/Extensions/ApiServiceCollectionExtensions.cs#L114); [.AddMvc(opts =&gt; — L123](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server/Extensions/ApiServiceCollectionExtensions.cs#L123); [return mvcBuilder.AddControllersAsServices() — L166](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server/Extensions/ApiServiceCollectionExtensions.cs#L166).

<a id="s16"></a>
## S16 — Authentifizierte Geräteidentität

- `Jellyfin.Api/Auth/CustomAuthenticationHandler.cs`: [new Claim(InternalClaimTypes.DeviceId — L65](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Auth/CustomAuthenticationHandler.cs#L65); [new Claim(InternalClaimTypes.Device, — L66](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Auth/CustomAuthenticationHandler.cs#L66); [new Claim(InternalClaimTypes.Client, — L67](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Auth/CustomAuthenticationHandler.cs#L67).
- `Jellyfin.Api/Constants/InternalClaimTypes.cs`: [public const string DeviceId — L16](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Constants/InternalClaimTypes.cs#L16); [public const string Device = — L21](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Constants/InternalClaimTypes.cs#L21); [public const string Client = — L26](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Constants/InternalClaimTypes.cs#L26).
- `Jellyfin.Api/Extensions/ClaimsPrincipalExtensions.cs`: [GetDeviceId( — L31](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Extensions/ClaimsPrincipalExtensions.cs#L31); [GetDevice( — L39](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Extensions/ClaimsPrincipalExtensions.cs#L39); [GetClient( — L47](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Extensions/ClaimsPrincipalExtensions.cs#L47).

<a id="s17"></a>
## S17 — Gespeicherte Client-Capabilities

- `Jellyfin.Api/Controllers/SessionController.cs`: [PostFullCapabilities( — L380](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Api/Controllers/SessionController.cs#L380).
- `Emby.Server.Implementations/Session/SessionManager.cs`: [_deviceManager.SaveCapabilities — L1779](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Emby.Server.Implementations/Session/SessionManager.cs#L1779).
- `Jellyfin.Server.Implementations/Devices/DeviceManager.cs`: [public void SaveCapabilities — L69](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server.Implementations/Devices/DeviceManager.cs#L69); [public ClientCapabilities GetCapabilities — L123](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/Jellyfin.Server.Implementations/Devices/DeviceManager.cs#L123).

<a id="s18"></a>
## S18 — Android-TV DeviceProfile

- `app/src/main/java/org/jellyfin/androidtv/util/profile/deviceProfile.kt`: [fun createDeviceProfile( — L93](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/util/profile/deviceProfile.kt#L93); [val allowedAudioCodecs — L128](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/util/profile/deviceProfile.kt#L128); [name = "AndroidTV-Default" — L171](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/util/profile/deviceProfile.kt#L171); [/// Transcoding profiles — L177](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/util/profile/deviceProfile.kt#L177); [/// Direct play profiles — L223](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/util/profile/deviceProfile.kt#L223); [ProfileConditionValue.AUDIO_CHANNELS — L554](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/util/profile/deviceProfile.kt#L554).

<a id="s19"></a>
## S19 — Android-TV Video-PlaybackInfo und Response

- `app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackManager.kt`: [private fun createStreamInfo( — L20](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackManager.kt#L20); [fun changeVideoStream( — L76](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackManager.kt#L76); [private suspend fun getVideoStreamInfoInternal( — L95](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackManager.kt#L95); [api.mediaInfoApi.getPostedPlaybackInfo( — L100](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackManager.kt#L100).
- `app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackController.java`: [private VideoOptions buildExoPlayerOptions( — L515](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackController.java#L515); [public void switchAudioStream( — L795](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackController.java#L795); [public void seek(long pos, boolean — L969](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackController.java#L969).

<a id="s20"></a>
## S20 — Android-TV Rewrite-Player und SDK

- `playback/jellyfin/src/main/kotlin/mediastream/JellyfinMediaStreamResolver.kt`: [override suspend fun getStream( — L27](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/playback/jellyfin/src/main/kotlin/mediastream/JellyfinMediaStreamResolver.kt#L27); [private suspend fun getPlaybackInfo( — L81](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/playback/jellyfin/src/main/kotlin/mediastream/JellyfinMediaStreamResolver.kt#L81); [val response by — L86](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/playback/jellyfin/src/main/kotlin/mediastream/JellyfinMediaStreamResolver.kt#L86).
- `app/src/main/java/org/jellyfin/androidtv/di/PlaybackModule.kt`: [val deviceProfileBuilder — L95](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/di/PlaybackModule.kt#L95).
- `app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackLauncher.kt`: [userPreferences[UserPreferences.playbackRewriteVideoEnabled] — L62](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/ui/playback/PlaybackLauncher.kt#L62).
- `app/src/main/java/org/jellyfin/androidtv/preference/UserPreferences.kt`: [var playbackRewriteVideoEnabled — L197](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/app/src/main/java/org/jellyfin/androidtv/preference/UserPreferences.kt#L197).
- `gradle/libs.versions.toml`: [jellyfin-sdk = " — L32](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/gradle/libs.versions.toml#L32).

<a id="s21"></a>
## S21 — Offizielle Template-Basis

- `Jellyfin.Plugin.Template/Plugin.cs`: [public class Plugin — L15](https://github.com/jellyfin/jellyfin-plugin-template/blob/c93225a0a5a76d3843db05b4b5b77fcfc482fba3/Jellyfin.Plugin.Template/Plugin.cs#L15).
- `Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs`: [public class PluginConfiguration — L24](https://github.com/jellyfin/jellyfin-plugin-template/blob/c93225a0a5a76d3843db05b4b5b77fcfc482fba3/Jellyfin.Plugin.Template/Configuration/PluginConfiguration.cs#L24).
- `Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj`: [&lt;TargetFramework&gt; — L4](https://github.com/jellyfin/jellyfin-plugin-template/blob/c93225a0a5a76d3843db05b4b5b77fcfc482fba3/Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj#L4); [Jellyfin.Controller — L13](https://github.com/jellyfin/jellyfin-plugin-template/blob/c93225a0a5a76d3843db05b4b5b77fcfc482fba3/Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj#L13); [Jellyfin.Model — L16](https://github.com/jellyfin/jellyfin-plugin-template/blob/c93225a0a5a76d3843db05b4b5b77fcfc482fba3/Jellyfin.Plugin.Template/Jellyfin.Plugin.Template.csproj#L16).
- `LICENSE`: [GNU GENERAL PUBLIC LICENSE — L1](https://github.com/jellyfin/jellyfin-plugin-template/blob/c93225a0a5a76d3843db05b4b5b77fcfc482fba3/LICENSE#L1).

<a id="s22"></a>
## S22 — DirectPlayForce: Registrierung und Filter

- `PluginServiceRegistrator.cs`: [public class — L14](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/PluginServiceRegistrator.cs#L14); [PostConfigure&lt;MvcOptions&gt; — L24](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/PluginServiceRegistrator.cs#L24).
- `Filters/AudioTranscodeFilter.cs`: [public class DirectPlayForceFilter — L20](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/Filters/AudioTranscodeFilter.cs#L20); [public async Task OnActionExecutionAsync — L33](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/Filters/AudioTranscodeFilter.cs#L33); [private void ForceDirectPlay — L144](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/Filters/AudioTranscodeFilter.cs#L144); [private static void ParseAuthHeader — L207](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/Filters/AudioTranscodeFilter.cs#L207).
- `Configuration/PluginConfiguration.cs`: [public class PluginConfiguration — L9](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/Configuration/PluginConfiguration.cs#L9); [public class DirectPlayRule — L32](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/Configuration/PluginConfiguration.cs#L32).

<a id="s23"></a>
## S23 — DirectPlayForce: Distribution und Lizenzbefund

- `Jellyfin.Plugin.DirectPlayForce.csproj`: [&lt;TargetFramework&gt; — L4](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/Jellyfin.Plugin.DirectPlayForce.csproj#L4); [Jellyfin.Controller — L15](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/Jellyfin.Plugin.DirectPlayForce.csproj#L15).
- `manifest.json`: ["versions" — L10](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/manifest.json#L10).
- `build.sh`: [docker build — L17](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/build.sh#L17).
- `README.md`: [## License — L197](https://github.com/upchui/Jellyfin-DirectPlayForce/blob/4b6abc8325f3b7ad8a7c90e062dd52154dd60547/README.md#L197).

<a id="s24"></a>
## S24 — ASP.NET Core: gebundene Action-Argumente

- [ControllerActionInvoker, v9.0.3, ActionBegin/BindArgumentsAsync vor ActionNext](https://github.com/dotnet/aspnetcore/blob/v9.0.3/src/Mvc/Mvc.Core/src/Infrastructure/ControllerActionInvoker.cs#L57).
- [ActionExecutingContext.ActionArguments, v9.0.3](https://github.com/dotnet/aspnetcore/blob/v9.0.3/src/Mvc/Mvc.Abstractions/src/Filters/ActionExecutingContext.cs#L38).

Diese Framework-Referenz ist auf einen Release-Tag gebunden. Die Projekt-Tests laufen gegen die lokal vorhandene ASP.NET-Core-9-Laufzeit; kein Beleg für die konkrete Laufzeit des produktiven Servers.
