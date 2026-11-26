# Playback-Pipeline: Server 10.11.11 / Android TV 0.20.0

Alle Aussagen unter „Belegt“ beziehen sich auf die in [sources.md](sources.md) fixierten Commits. Lokale Experimente und noch offene Hardwarefragen werden getrennt bezeichnet.

## 1. Profilbildung und Request im Client — belegt

`app/.../util/profile/deviceProfile.kt`, beide Überladungen von `createDeviceProfile`, erzeugt `AndroidTV-Default`. Die erste Überladung sammelt Codec-Fähigkeiten, Bitstream-Einstellungen, Stereo-Downmix, AVC-/HEVC-Level und HDR-Overrides. Die zweite erzeugt das SDK-`DeviceProfile`. Audio-Passthrough-Einstellungen filtern unter anderem AC3, EAC3, DTS und TrueHD. Die Kanalbedingung für Audio liegt standardmäßig bei acht bzw. bei Stereo-Downmix bei zwei Kanälen. Das Profil ist eine softwareseitige Fähigkeitsbeschreibung, kein vermessener Beleg für die gesamte ARC-Kette. [S18](sources.md#s18).

Video-Transcoding enthält zwei HLS-Profile:

| Segmentcontainer | Angebotsliste Audio vor Filterung durch Geräteeinstellungen |
| --- | --- |
| TS | AAC, AC3, EAC3, MP3 |
| MP4/fMP4 | AAC, AC3, EAC3, MP3, ALAC, FLAC, OPUS, DTS |

Video-Codecs sind abhängig von HEVC-Unterstützung HEVC und H264. Die Listen werden mit `allowedAudioCodecs` geschnitten. DirectPlay für Video erlaubt mehrere Container und dieselbe ermittelte Audio-Codecliste. Das eigene reine Audio-Transcoding-Profil ist HLS/TS/AAC. [S18](sources.md#s18).

Im standardmäßig verwendeten Video-Pfad baut `PlaybackController.buildExoPlayerOptions` das Profil. `PlaybackManager.getVideoStreamInfoInternal` sendet über SDK `mediaInfoApi.getPostedPlaybackInfo` ein `PlaybackInfoDto` an `POST /Items/{itemId}/PlaybackInfo`: Profil, MediaSource-ID, Startzeit, Audio-/Untertitelindex, MaxAudioChannels, DirectPlay-/DirectStream-Schalter und beide Copy-Erlaubnisse. `autoOpenLiveStream=true`; das SDK ist in `gradle/libs.versions.toml` auf **1.8.12** festgelegt. [S19](sources.md#s19), [S20](sources.md#s20).

Es existiert außerdem der Rewrite-Pfad: `PlaybackModule` übergibt denselben Profilgenerator an `jellyfinPlugin`; `JellyfinMediaStreamResolver.getPlaybackInfo` sendet ebenfalls `PlaybackInfoDto`, dort mit `autoOpenLiveStream=false`. `PlaybackLauncher` wählt für Video abhängig von `playbackRewriteVideoEnabled`, dessen Standardwert in 0.20.0 **false** ist. Audio verwendet den neuen MediaManager. Der Resolver besitzt andere Auswahlgrenzen und akzeptiert in dieser Version nur lokale File-Quellen. Beide Pfade sind im Quellbestand berücksichtigt; ihre vollständige Laufzeit wurde nicht getestet. [S20](sources.md#s20).

## 2. Server-API, Binding und Profilherkunft — belegt

`Jellyfin.Api/Controllers/MediaInfoController.cs` ist autorisiert. Der GET-Endpunkt `GetPlaybackInfo` liefert grundlegende Medieninformationen, ohne die POST-spezifische Profilentscheidung. Der relevante POST-Endpunkt ist `GetPostedPlaybackInfo`. [S02](sources.md#s02).

Der Body bindet an `Jellyfin.Api.Models.MediaInfoDtos.PlaybackInfoDto`; sein Profiltyp ist `MediaBrowser.Model.Dlna.DeviceProfile`. Die Action liest zuerst `playbackInfoDto?.DeviceProfile`. Fehlt es, lädt sie `IDeviceManager.GetCapabilities(User.GetDeviceId()).DeviceProfile`. Capabilities können über `Sessions/Capabilities/Full` gemeldet werden; `SessionManager.ReportCapabilities` speichert sie über den DeviceManager. Dessen Map liefert vorhandene Objekte zurück. Ein Plugin darf daher kein solches Objekt direkt verändern. [S02](sources.md#s02), [S03](sources.md#s03), [S17](sources.md#s17).

Für zahlreiche Felder gilt **Query vor Body**: `maxAudioChannels ??= playbackInfoDto?.MaxAudioChannels`, ebenso Stream-Indizes, DirectPlay-/Transcoding-/Copy-Schalter. Es reicht bei vorhandenem Querywert nicht, nur das DTO-Feld zu setzen. Kein Profil bedeutet außerdem nicht automatisch, dass der Server keine passende gecachte Capability hat. [S02](sources.md#s02).

`RequestHelpers.GetUserId` und Library-/UserManager-Aufrufe bleiben im Controller. Dieser ruft `MediaInfoHelper.GetPlaybackInfo` auf, beendet bei einem `ErrorCode` und berechnet bei vorhandenem Profil für jede Medienquelle die gerätespezifischen Eigenschaften mit `SetDeviceSpecificData`. Bei `autoOpenLiveStream` kann anschließend `OpenMediaSource` aufgerufen werden. In diesem Zweig wird erneut das **DTO-Profil** in den LiveStreamRequest übernommen, nicht die lokale Variable `profile`; das ist für einen späteren Capability-Fallback-Adapter relevant. [S02](sources.md#s02), [S04](sources.md#s04).

## 3. DirectPlay, Transcoding und Codec-Auswahl — belegt

`MediaInfoHelper.SetDeviceSpecificData` erstellt `new StreamBuilder(_mediaEncoder, _logger)` und ein `MediaOptions` mit dem übergebenen Profil. Ein angeforderter Audioindex wird nur bei übereinstimmender MediaSource-ID explizit in die Optionen übernommen. Der Helper berücksichtigt Request-Schalter, Benutzerrechte sowie lokale/entfernte Bitratenlimits. Im regulären Pfad setzt er `options.EnableDirectStream=false`, weil HTTP-DirectStream dort als defekt kommentiert ist. Danach folgen `GetOptimalVideoStream` oder `GetOptimalAudioStream`. [S04](sources.md#s04).

Für Video prüft `StreamBuilder.GetVideoDirectPlayProfile` die `DirectPlayProfiles` zusammen mit Container-, Video-, Audio- und Untertitelbedingungen. DirectPlay wird bei zulässiger Gesamtheit bevorzugt. Ein höher priorisierter AC3-Transcoding-Eintrag erzwingt deshalb keine Umwandlung einer weiterhin als direkt kompatibel beschriebenen AAC-5.1-Spur. [S06](sources.md#s06).

Falls nötig bewertet `GetVideoTranscodeProfile` passende `TranscodingProfiles` nach Medientyp/EncodingContext und Kompatibilität: Video-Copy sowie Audio-Copy werden bevorzugt; bei gleicher Bewertung beeinflusst die Profilreihenfolge die Wahl. `BuildStreamVideoItem` beschränkt Codec-Listen auf die serverseitig für HLS/TS bzw. HLS/MP4 zugelassenen Formate, prüft Audio-Kandidaten und wendet Codec-Profile an. Ein expliziter Streamindex und die verfügbaren Kandidaten beeinflussen damit auch, ob eine alternative Tonspur gewählt werden kann. [S06](sources.md#s06).

`TranscodingProfile.MaxAudioChannels` wird als Zeichenkette geparst und nach `StreamInfo.TranscodingMaxAudioChannels` übertragen. Ein globales MaxAudioChannels stammt aus dem Request. `CodecProfile.Conditions` können codecqualifizierte Kanaloptionen erzeugen; Audiobitraten werden unter anderem durch `GetAudioBitrate` und Profile begrenzt. **Wichtige Einschränkung:** Die `Conditions` des Transcoding-Profils werden im Video-Pfad nur bei Video-/Containerbitrate-Gründen zusätzlich angewandt. Für ausschließlich inkompatiblen Ton ist die Annahme „Bitrate in TranscodingProfile.Conditions genügt“ nicht tragfähig. [S05](sources.md#s05), [S06](sources.md#s06).

Reines Audio verwendet den eigenen `GetOptimalAudioStream`-Pfad und Profile mit Typ `Audio`, einschließlich MusicStreamingTranscodingBitrate. Eine Anpassung von Video-Audioprofilen ist kein vollständiger Musik-Transcoding-Mechanismus. [S06](sources.md#s06).

## 4. PlaybackInfo-Antwort und URL — belegt

`MediaInfoHelper.SetDeviceSpecificData` setzt `SupportsDirectPlay`, `SupportsDirectStream`, `SupportsTranscoding`, Container/Protokoll, Standard-Audioindex und Untertitelinformationen anhand des ermittelten `StreamInfo`. Die Flags sind nicht frei austauschbar: insbesondere wird `SupportsDirectStream` im Helper eingeschränkt und im Universal-Audio-Pfad selbst als missverständlich kommentiert. [S04](sources.md#s04), [S14](sources.md#s14).

Wenn kein DirectPlay möglich ist und Transcoding/DirectStream angeboten wird, setzt der Helper den Plan auf `PlayMethod.Transcode` und erzeugt `TranscodingUrl` über `StreamInfo.ToUrl`. Zusätzliche Request-Verbote werden als `allowVideoStreamCopy=false`/`allowAudioStreamCopy=false` angehängt. Eine erzwungene Remote-Transcoding-Policy hat einen eigenen Zweig mit beiden Copy-Verboten. Kein Plugin darf diesen Zweig über Antwortflags umgehen. [S04](sources.md#s04).

`MediaBrowser.Model/Dlna/StreamInfo.ToUrl` serialisiert:

| Planbestandteil | URL-Bestandteil / Bedeutung |
| --- | --- |
| Audio-/Video-Medientyp, HLS | `/audio/{id}/master.m3u8` bzw. `/videos/{id}/master.m3u8` |
| Progressiv | `/stream.{container}` |
| Codec-Listen | `AudioCodec`, `VideoCodec` |
| Auswahl | `MediaSourceId`, `AudioStreamIndex`, Untertitelindex/-methode |
| Grenzen | `AudioBitrate`, `TranscodingMaxAudioChannels`, qualifizierte Optionen wie `aac-audiochannels` aus `StreamOptions` |
| HLS | `SegmentContainer`, Segmentlänge, Mindestsegmente, Copy-/Break-Optionen |
| Zeit/Sitzung | `StartTimeTicks` bei progressiven URLs; `PlaySessionId`, ggf. `LiveStreamId` |
| Authentifizierung | `ApiKey` kann Teil der URL sein; deshalb niemals komplette URLs loggen |

Nicht jede Kanalanforderung steht als allgemeines `MaxAudioChannels` in der URL; wichtig sind auch die qualifizierten Optionen und das Transcoding-Maximum. [S07](sources.md#s07).

Clientseitig bevorzugt `PlaybackManager.createStreamInfo` DirectPlay und baut dafür selbst eine `Videos/.../stream`-URL mit `static=true`. Andernfalls übernimmt er die Server-`TranscodingUrl`. Der Rewrite-Resolver folgt demselben grundsätzlichen Flags-/URL-Vertrag. Eine bloße Codec-Ersetzung in einer Antwort ohne konsistente Flags erreicht daher unter Umständen gar nicht den verwendeten Stream. [S19](sources.md#s19), [S20](sources.md#s20).

## 5. Folge-Requests und FFmpeg — belegt

`DynamicHlsController.GetMasterHlsVideoPlaylist` bindet die Queryparameter an einzelne Action-Argumente, baut daraus `VideoRequestDto` und delegiert an `DynamicHlsHelper.GetMasterHlsPlaylist`. HLS-Audio besitzt einen separaten Master-Endpunkt. Der Helper erzeugt `main.m3u8`/`live.m3u8`-Folge-URLs und übernimmt die Queryparameter; gegebenenfalls ersetzt er `AudioCodec` passend zum tatsächlich ermittelten State. Segmentanfragen laufen wieder über den DynamicHlsController. [S09](sources.md#s09), [S10](sources.md#s10).

`StreamingHelpers.GetStreamingState` baut den Encoding-State aus der Streaminganfrage und der Medienquelle, nicht erneut aus dem ursprünglichen DeviceProfile. Es zerlegt Codec-Angebote, bevorzugt einen verfügbaren Audioencoder, lädt Medieninfos, ruft `EncodingHelper.AttachMediaSourceInfo`, berechnet Output-Kanäle und Output-Bitrate und prüft Stream-Copy. Klein geschriebene Query-Optionen werden als `StreamOptions` übernommen. Die Profilentscheidung muss daher bereits einen konsistenten URL-Vertrag erzeugt haben. [S08](sources.md#s08).

`EncodingHelper.AttachMediaSourceInfo` kann Codec-Reihenfolgen ändern: `ShiftAudioCodecsIfNeeded` verschiebt DTS/TrueHD bei mindestens sechs Eingangskanälen und AC3/EAC3 bei weniger als sechs nach hinten, sofern andere Formate vorhanden sind. Das bloße Voranstellen von AC3 ist für alle Eingangsfälle keine feste Codec-Zusage. [S11](sources.md#s11).

`EncodingJobInfo.GetRequestedAudioChannels` beachtet zuerst codecqualifizierte Kanäle, danach allgemeine MaxAudioChannels/AudioChannels/TranscodingMaxAudioChannels. `EncodingHelper.GetNumAudioChannelsParam` begrenzt anhand Quelle und Encoder; AC3 ist auf sechs Kanäle begrenzt. Mono/Stereo wird dabei nicht auf sechs angehoben. Bei segmentiertem Streaming gibt es aber Sondernormalisierung für ungewöhnliche 3-/4-/5-/7-Kanalzahlen; insbesondere kann 5 auf 6 steigen. Eine allgemeine Garantie gegen jedes Hochmischen wäre deshalb zu weitgehend. [S11](sources.md#s11), [S12](sources.md#s12).

`EncodingHelper.CanStreamCopyAudio` prüft unter anderem Copy-Erlaubnis, enthaltenen Quellcodec, Kanal-/Bitraten-/Sampleraten-/Bittiefengrenzen und AutoStreamCopy. `TryStreamCopy` setzt dann gegebenenfalls Audio/Video auf `copy`; bei fehlendem Transcoding-Recht kann es Copy auch trotz Inkompatibilität erzwingen. Das ist kein Nachweis erfolgreicher Wiedergabe bei gesperrten Rechten. [S11](sources.md#s11).

`EncodingHelper.GetAudioEncoder` bildet `aac` abhängig von verfügbaren Encodern auf `aac_at`, `libfdk_aac` oder `aac` ab; `ac3` bleibt `ac3`. `GetAudioBitrateParam` begrenzt hier die genannten verlustbehafteten Formate bei mindestens sechs Eingangs-/Ausgangskanälen auf höchstens 640000. `DynamicHlsController.GetAudioArguments` erzeugt bei Video `-codec:a:0`, Kanalzahl und `-ab`; Copy vermeidet Audiokodierung. Progressive Videoausgabe nutzt `EncodingHelper.GetProgressiveVideoAudioArguments`. [S09](sources.md#s09), [S11](sources.md#s11).

**Abweichung zur Ausgangsbeobachtung:** Der gemeldete Aufruf `libfdk_aac -ac 6 -ab 768000` wird als Nutzerbeobachtung festgehalten. Aus dem hier geprüften regulären 10.11.11-Pfad mit den beschriebenen Kanälen folgt dagegen ein 640000-Limit. M1 muss deshalb eine bereinigte vollständige Anfrage-/Response-/FFmpeg-Aufrufkette mit Server-Buildidentität abgleichen. Der Ausschnitt allein belegt weder den genauen Requestpfad noch die verwendete Binärversion.

## 6. Streamingarten und Wiederholungen

| Art | Belegter Ablauf | Konsequenz für das Plugin |
| --- | --- | --- |
| Statisches DirectPlay | `VideosController.GetVideoStream`, `static=true`, lokale/remote Dateiantwort ohne normale FFmpeg-Transcodingausgabe [S13](sources.md#s13) | Kein Codec-Override durch bloße Zusatzparameter; Inkompatibilität vorher korrekt beschreiben |
| Remux | Container-/Segmentwechsel; Video und Audio können kopiert werden | AC3 möglichst kopieren; nicht pauschal `AllowAudioStreamCopy=false` setzen |
| Nur Audio umkodieren in Video | Videofähigkeiten erlauben Copy; Audio benötigt anderen Codec | Kann trotzdem als `Transcode` erscheinen und HLS verwenden; tatsächliche FFmpeg-Zeile prüfen |
| Video transkodieren | Codec-/Profil-/Bitrate-/Untertitelgründe verhindern Copy | Audio-Plugin darf diese Gründe nicht durch fingierte Video-Fähigkeiten beseitigen |
| HLS | Master → Media-Playlist → Segmente, getrennte Requests | Parameter müssen durch die ganze Kette bestehen bleiben; PlaybackInfo wird nicht pro Segment wiederholt |
| Progressiv | Stream-Endpunkt mit Encoding-State, bei Bedarf FFmpeg | Eigener Ausgabeweg und Seek-Verhalten; kein allgemeiner HLS-Zwang |
| Reines Audio | AudioController, UniversalAudioController, eigener Audio-StreamBuilder-Pfad [S14](sources.md#s14) | In M1 zunächst aus dem Eingriff ausnehmen, gesondert planen |

**Audio-Track-Wechsel:** `PlaybackController.switchAudioStream` kann bei nicht transkodierter Wiedergabe lokal den ExoPlayer-Track wechseln. Dann gibt es keinen neuen Serverentscheid, und ein Plugin kann die lokal gewählte inkompatible Spur nicht nachträglich abfangen. Andernfalls startet der Client mit neuem Audioindex und MediaSource-ID über PlaybackInfo neu. [S19](sources.md#s19).

**Seeking:** `PlaybackController.seek` nutzt bei seekbarer Wiedergabe den bestehenden Player. Bei nicht seekbarem Stream ruft es `changeVideoStream` auf; dieses stoppt gegebenenfalls den alten Encoding-Prozess und fordert neue PlaybackInfo an. Ein Request-Filter muss jeden Neuaufruf unabhängig und ohne zusätzliche Verschärfung einer schon angepassten Kopie behandeln. HLS-Segmentwechsel allein sind kein neuer PlaybackInfo-Aufruf. [S19](sources.md#s19).

**Experiment offen:** tatsächliche Auswahl von TS/fMP4, Video-Copy, Audio-Copy, Kanalbelegung, Geräteeinstellungen, Retry-Verhalten und HDR/Dolby Vision auf der angegebenen Wiedergabekette. Die Quellcodeanalyse beweist keine durchgängige Hardwarekompatibilität.
