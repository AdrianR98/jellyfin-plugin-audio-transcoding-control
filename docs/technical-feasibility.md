# Technische Machbarkeit

**Aktualisierung M1:** Ansatz A ist implementiert. Der native Server-/HLS-/FFmpeg-Nachweis liegt vor; siehe [Implementierung, Beweisstärken und Einschränkungen](m1-verification.md). Die folgenden Abschnitte halten die ursprüngliche M0-Analyse fest. Aussagen über „noch nicht implementiert“ beschreiben dort den damaligen M0-Stand, nicht den aktuellen Branch.

## Ergebnis und Evidenzstand

**M0-Ergebnis:** Ein Server-Plugin kann über `IPluginServiceRegistrator` globale MVC-Action-Filter registrieren. Ein solcher Filter kann nach Model-Binding und vor `MediaInfoController.GetPostedPlaybackInfo` das Profil im gebundenen Request ersetzen. Ansatz A ist der bevorzugte M1-Eingriffspunkt. Die Quellen und isolierten Tests stützen die Machbarkeit; eine in Jellyfin geladene, Audio verändernde Implementierung existiert noch nicht.

Für die Belegklassen gelten die Definitionen aus [architecture.md](architecture.md). Prüfstand ist 26.11.2026, fixierte Versionen siehe [sources.md](sources.md).

## A. DeviceProfile vor der Entscheidung — bevorzugt

**Belegt:** `PluginManager.RegisterServices` findet `IPluginServiceRegistrator`-Implementierungen, erzeugt sie mit parameterlosem Konstruktor und ruft deren Registrierung für unterstützte/aktive Plugins auf. Jellyfin verwendet MVC mit `MvcOptions`. Eine Registrierung mit `PostConfigure<MvcOptions>` und `Filters.AddService<T>()` ist im eigenen M0-Rahmen vorhanden; der Filter ist scoped und vollständig durchleitend. [S15](sources.md#s15).

**Belegt:** ASP.NET Core bindet die Action-Argumente vor dem Aufruf der Action-Filter. `ActionExecutingContext.ActionArguments` ist ein veränderbares Dictionary. Der Server liest das DTO-Profil erst innerhalb von `GetPostedPlaybackInfo` und übergibt es an die native Wiedergabeentscheidung. Es ist daher nicht erforderlich, den bereits gelesenen JSON-Body umzuschreiben oder die Request-Query nach dem Binding erneut zu parsen. [S24](sources.md#s24), [S02](sources.md#s02).

**Lokal geprüft:** Der echte MVC-TestHost bindet ein synthetisches Request-DTO mit dem echten `Jellyfin.Model.DeviceProfile`. Ein ausschließlich im Test enthaltener Filter ersetzt dieses Profil und einen gebundenen Querywert, bevor die Test-Controller-Action beides ausliest. Das echte `Jellyfin.Api.PlaybackInfoDto`, Server-Startup und Serverberechtigungen werden damit nicht ausgeführt. Weitere Discovery-Tests rufen den echten `StreamBuilder` aus dem NuGet-Paket 10.11.11 auf und prüfen ein manuell konstruiertes Kandidatenprofil.

**Entwurf:** Der erste M1-Adapter soll POST-PlaybackInfo mit tatsächlich übermitteltem Profil behandeln. Bei fehlendem Profil bleibt M1 zunächst unverändert, statt den globalen Capability-Speicher zu verändern. Ein späterer Capability-Fallback kann das gespeicherte Profil tief kopieren und auf ein anfrageeigenes DTO setzen; dafür müssen insbesondere leere Bodies und LiveStreams/Open konsistent behandelt werden.

**Entwurf:** Profile nur für die passende Device-ID/Regel umformen. DirectPlay-/Codec-Profile müssen inkompatible Quellformate/Kanalzahlen ausschließen; Transcoding-Profile und qualifizierte Bedingungen müssen den zulässigen AC3-Pfad beschreiben. Bestehende Video-/Untertitelrestriktionen erhalten. Ziele sind höchstens sechs Kanäle und 640000 bit/s, keine Erhöhung niedrigerer Client-/Serverlimits. AC3-Copy muss zulässig bleiben.

**Vorteil:** Jellyfin erzeugt Flags, URL, Stream-Auswahl und Untertitelinformationen aus demselben Profil. Das reduziert die Zahl manuell zu synchronisierender Felder und lässt die bestehenden Rechtepfade bestehen.

**Experiment offen:** echte DTO-Adapterkompatibilität, Reihenfolge gegenüber anderen Plugins, Originalprofil aus dem Fire TV, Muxer-/Encoderverfügbarkeit, LiveStream-/Track-/Seek-Verhalten und tatsächlicher Audio-/Video-Output. Ohne diese Tests wird A nicht als fertig ausgelieferte Lösung bezeichnet.

## B. PlaybackInfo-Antwort konsistent verändern — möglich, zurückgestellt

**Belegt:** Die Antwort enthält pro `MediaSourceInfo` unter anderem Supportflags, `TranscodingUrl`, Transcoding-Container/-Protokoll und Standard-Audioindex. Der Client wählt zunächst anhand der Flags, kann für DirectPlay eine eigene statische URL erzeugen und verwendet sonst die TranscodingUrl. Native URL-Erzeugung und HLS-Weiterleitung hängen mit Codec-/Kanal-/Bitraten- und Untertitelparametern zusammen. [S04](sources.md#s04), [S07](sources.md#s07), [S10](sources.md#s10), [S19](sources.md#s19).

Ein Result-/Action-Filter könnte das Objekt vor der Serialisierung ändern. Für einen belastbaren Ansatz müsste er **alle betroffenen Medienquellen** und mindestens folgenden Vertrag konsistent behandeln:

- `SupportsDirectPlay`, `SupportsDirectStream`, `SupportsTranscoding`, Fehlerzustand und vorhandene Rechte.
- Medienquellen-/Audioindex, Transcoding-Container und HLS-Subprotokoll.
- URL-Endpunkt und SegmentContainer, Codec-Angebote, AudioBitrate, TranscodingMaxAudioChannels sowie allgemeine/codecqualifizierte Kanaloptionen.
- Copy-Erlaubnisse, AutoStreamCopy, Untertitelmethoden/-URLs, Zeit-/Session-/LiveStream-Parameter und alle unveränderten Videobeschränkungen.
- Doppelte Queryschlüssel, Escaping und die Erhaltung vorhandener Authentifizierung ohne Logging.

Ein einfaches Ersetzen von `AudioCodec=aac` durch `ac3` oder das erzwungene Setzen von DirectPlay-Flags ist unzureichend. Der Filter würde eine vom Core bereits getroffene Entscheidung nachträglich rekonstruieren und könnte auf Rechte-/LiveStream-/Untertitelzuständen auseinanderlaufen. Eine Neuberechnung mit dem nativen Helper wäre inhaltlich wieder eine Variante von A, mit zusätzlichem Aufwand und möglichen wiederholten Seiteneffekten.

**Entscheidung:** B nur untersuchen, falls ein konkret belegter A-Fehler nicht über das Eingangsprofil lösbar ist. Keine Response-Patches in M0/M1 als pauschaler Zusatz.

## C. Andere serverseitige Erweiterungspunkte

| Kandidat | Befund / Bewertung |
| --- | --- |
| Capability-Meldungen filtern | `Sessions/Capabilities/Full` und DeviceManager-Speicher sind belegt [S17](sources.md#s17). Ein Client kann in PlaybackInfo trotzdem ein frisches eigenes Profil senden. Globale Cache-Mutation ist anfrageübergreifend riskant; kein primärer Ansatz. |
| Streaming-Action-Argumente vor Controller ändern | MVC kann gebundene Werte verändern; die HLS-/Video-Endpunkte besitzen passende Audioargumente [S09](sources.md#s09), [S13](sources.md#s13). Denkbar für gezielten späteren Guard, aber Plan/Antwort und Master-/Segmentzustand können inkonsistent werden. Nicht M1-Standard. |
| Eigene Plugin-Controller | Jellyfin nimmt Plugin-Assemblies als MVC-ApplicationParts auf [S15](sources.md#s15). Offizielle Clients rufen neue private Endpunkte nicht von selbst auf. Kein Nutzen ohne zusätzliche Umlenkung. |
| MediaInfoHelper ersetzen/dekorieren | Konkrete Klasse, `SetDeviceSpecificData` nicht virtuell; intern `new StreamBuilder`. Kein spezialisierter Plugin-Codecselector in dem geprüften Entscheidungsweg [S04](sources.md#s04). Kein kleiner, austauschbarer Policy-Service als Alternative gefunden. |
| IMediaEncoder/DeviceManager global ersetzen | DI ist erweiterbar, aber diese Dienste werden für viele Aufgaben verwendet. Überschreiben hat breite Nebenwirkungen und löst den Request-/URL-Vertrag nicht automatisch. Zurückgestellt. |
| Middleware/Resource-Filter, Body neu schreiben | Frameworkseitig plausibel; würde vor Model-Binding eingreifen, mit zusätzlichem JSON-/Buffering-/Fehlerbehandlungsaufwand. A erreicht dieselbe Stelle nach dem Binding gezielter. Nicht implementiert. |

Die Aussage „kein spezialisierter Codecselector gefunden“ bezieht sich auf die untersuchten Pfade; sie ist keine Behauptung über sämtliche denkbaren Jellyfin-Erweiterungen. FFmpeg-Binärmanipulation, Harmony, Core- und Clientänderungen sind laut Projektvorgabe ausgeschlossen.

## DirectPlayForce als Referenz

Gelesen wurden scoped/globaler Action-Filter, DI mit PostConfigure, Identifikation per Headerparser, Konfiguration, erstes passendes Regelmatching und Antwortmutation. Distribution erfolgt dort über Docker-/Buildskripte, ZIP-Assets und ein Katalogmanifest. Der Filter greift nach der Controller-Action ein und erzwingt DirectPlay durch Supportflags und Entfernen der TranscodingUrl; das erfüllt dieses Projektziel nicht. [S22](sources.md#s22), [S23](sources.md#s23).

Übernommen wird nur die allgemeine architektonische Erkenntnis, dass ein Plugin in die MVC-Pipeline integriert werden kann; die eigene Implementierung leitet sich aus den öffentlichen Host-/Framework-APIs ab. Der MIT-Verweis ohne vorhandene LICENSE-Datei ist als unvollständige Lizenzgrundlage dokumentiert. Keine Code-, HTML- oder Grafikübernahme. Details: [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

## Bekannte Risiken und Grenzen

| Risiko | Beleg / notwendiger Umgang |
| --- | --- |
| ARC-/EDID-/Passthrough-Kette | Nicht getestet. AC3 im Serverangebot beweist keine Decodierung am AVR; Gerätetest erforderlich. |
| Dolby Vision/HDR | `copy` beschreibt Videokodierung. Container, RPU-/EL-/HDR-Metadaten, Bitstreamfilter, Player und HDMI-Kette können das Ergebnis verändern. Keine Erhaltungsgarantie. |
| Stereo und ungewöhnliche Layouts | Codec-Umsortierung bei weniger als sechs Kanälen und HLS-Kanalnormalisierung sind belegt [S11](sources.md#s11). M1 eng begrenzen und Mono/Stereo sowie bekannte Layouts prüfen. |
| AC3 bereits kompatibel | Muss als DirectPlay oder Audio-Copy bleiben dürfen. Zu enge globale Bitratengrenzen können unnötige Re-Encodes auslösen; Scope der Limits präzise testen. |
| Benutzerrechte | Helper setzt Flags; EncodingHelper kann bei verbotener Transkodierung Copy trotz Inkompatibilität erzwingen [S04](sources.md#s04), [S11](sources.md#s11). Plugin darf daraus keine Rechteumgehung oder Erfolgsgarantie machen. |
| Lokal gewechselte Tonspur | Beim Trackwechsel ohne neuen Serverrequest gibt es keinen Filtereingriff. Wiederholte PlaybackInfo-Aufrufe sind abfangbar, lokale Trackwechsel nicht. |
| Abhängigkeit von Jellyfin.Api-DTO | Nicht Teil der zwei direkten Plugin-Paketreferenzen. Versionsgeprüfter Adapter nötig; bei Abweichungen durchleiten. |
| Konflikte mit weiteren Plugins | Reihenfolge und Mehrfachmutation im realen Host offen; keine globale mutierbare Profilreferenz verwenden. |
| Debug-Daten | Native URLs enthalten ggf. API-Key. Plugin-Diagnose muss auf ausdrücklich erlaubte Einzelfelder beschränkt bleiben. |
| Gemeldete 768000 bit/s | Passt nicht zum regulären untersuchten 6-Kanal-Limit; vollständige bereinigte Aufrufkette vor Implementierung abgleichen. |

## M0-Fazit

Die bevorzugte Erweiterungsstelle ist identifiziert und als inaktiver Plugin-Rahmen kompilierbar. Die abgesicherte Profiltransformation, Rechteprüfung und echte Gerätewirkung sind Arbeit für M1. Der konkrete Ablauf und seine Abnahmekriterien stehen in [roadmap.md](roadmap.md).
