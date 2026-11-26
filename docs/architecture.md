# Architektur

## M1: implementierter Stand

Der aktuelle Prototyp ist in [m1-verification.md](m1-verification.md) beschrieben und serverseitig bis FFmpeg nachgewiesen. Eine einzelne aktivierbare Regel verlangt exakte Device-ID **und** Benutzer-ID; globale Regeln, Namen und Prioritäten mehrerer Regeln bleiben spätere Erweiterungen. Der folgende M0-Vertrag bleibt als Herkunft der Entscheidung und als weiterführender Entwurf erhalten.

Umgesetzt sind der native `PlaybackInfoDto`-Zugriff vor der echten Controller-Action, Host-/API-Commitprüfung, Request-Konfigurationssnapshot, Rechte-/Encoder-/Quellenauswertung, tiefe Profilkopie und AC3-Transformation für geeignete 6-/8-Kanalspuren. Die einzige Requestmutation ist der Austausch von `DeviceProfile`. Mono/Stereo, ungeeignete Profile und Nichttreffer bleiben unverändert. Die native Metadatenabfrage darf wie die Action probieren; ein noch nicht vollständig gescannter lokaler Titel wird dadurch schon beim ersten PlaybackInfo-Aufruf korrekt behandelt.

Die API-Assembly wird aus einem gepinnten portablen 10.11.11-Paket referenziert und nicht mitgeliefert. Keine DTO-Nachbildung und kein Reflection-Adapter. Direkte Streamingrequests ohne PlaybackInfo bleiben außerhalb des Filters; ein AAC-Kontrollstream ist als Grenze praktisch belegt. Der tatsächliche Android-TV-Client und die HDMI-Kette bleiben ungetestet.

## M0-Entscheidung und Geltungsbereich

**Entscheidung ADR-001:** Ansatz A wird für M1 bevorzugt: das anfrageeigene `DeviceProfile` vor Ausführung von `MediaInfoController.GetPostedPlaybackInfo` anpassen. Jellyfin bleibt verantwortlich für Medienauswahl, Rechte, Wiedergabeentscheidung, Antwort und URL-Erzeugung. Belege: [S02](sources.md#s02), [S04](sources.md#s04), [S15](sources.md#s15), [S24](sources.md#s24).

Evidenzbegriffe in dieser Dokumentation:

- **Belegt:** im fixierten Upstream-Quellcode nachvollzogen.
- **Lokal geprüft:** mit angegebenen isolierten Tests oder Build-Befehlen ausgeführt.
- **Entwurf/Annahme:** geplante Architektur oder plausible Schlussfolgerung, noch kein implementiertes Produktverhalten.
- **Experiment offen:** benötigt einen vollständigen Testserver, FFmpeg oder die echte Gerätekette.

**Implementiert in M0:** `Plugin : BasePlugin<PluginConfiguration>`, feste Plugin-GUID, XML-kompatible Konfiguration mit zwei standardmäßig falschen Schaltern, `PluginServiceRegistrator` und `PlaybackInfoActionFilter`. Der Filter ruft ausschließlich `next()` auf. Es gibt keine Profilmutation, Geräteauswertung, Logs oder Regelverarbeitung. Beide Schalter haben in M0 keine Wiedergabewirkung. Keine Dashboard-Seite und keine produktive Installation.

## Geplanter Ablauf ab M1

1. Filter prüft authentifizierten Kontext, HTTP-Methode und konkrete Controller-/Action-Metadaten. Nur `POST Items/{itemId}/PlaybackInfo` mit erwarteter DTO-Signatur ist zunächst zulässig; ein bloßes Pfad-Suffix genügt nicht.
2. `DeviceIdentityResolver` liest Device-ID, Gerätename und Clientname aus den bereits aufgebauten Claims, optional ergänzt um vom Host gespeicherte Geräteinformationen. Kein eigener Parser des Authorization-Headers. Claim-Namen sind versionsabhängige Adapterdetails. [S16](sources.md#s16).
3. `RuleResolver` verarbeitet einen unveränderlichen Snapshot validierter Konfiguration. Kein Treffer/deaktiviert/unbekannt → unmittelbar weiterleiten.
4. `PlaybackRequestAdapter` liest das gebundene `playbackInfoDto` und effektive Query-/Body-Werte. M0 hat bewusst keine Referenz auf die Server-Assembly `Jellyfin.Api`; der Adapter muss den Typnamen und die erwartete öffentliche `DeviceProfile`-Property gegen den echten Server prüfen. `DeviceProfile` selbst ist über `Jellyfin.Model` typisiert verfügbar. Fehlende/abweichende Form → unverändert weiterleiten.
5. `DeviceProfileTransformer` erstellt eine vollständige tiefe Kopie, verändert nur notwendige Audioeigenschaften und validiert das Ergebnis. Alle Arrays, verschachtelten Profile und Bedingungen müssen isoliert sein; der vorhandene `TranscodingProfile`-Kopierkonstruktor teilt insbesondere sein `Conditions`-Array und genügt allein nicht. [S05](sources.md#s05).
6. Erst nach vollständigem Erfolg wird die neue Profilreferenz am anfrageeigenen DTO gesetzt. Gebundene Query-Argumente werden nur angepasst, wenn ein nachweislich nötiges engeres Limit sonst übersteuert würde. Controller und übrige Filter laufen genau einmal.
7. Jellyfin baut Antwort und Stream-URLs. Der Plugin-Filter schreibt keine Antwortflags und protokolliert keine Streaming-URLs.

**Fallback-Vertrag:** Vorbereitungsfehler dürfen keinerlei Mutation zurücklassen. Keine generelle `try/catch`-Hülle um `next()` mit erneuter Action-Ausführung: das würde Seiteneffekte wiederholen. Controllerfehler und Abbruchsignale bleiben erhalten. Für einen Wiedergabefehler nach ausgelieferter Antwort gibt es ohne erneuten Request keinen transparenten Rücksprung. Ein späterer Retry-Fallback braucht einen gesonderten, begrenzten Zustandsvertrag und ist kein M0-Versprechen.

## Weiterführender Regelvertrag — M0-Entwurf für spätere Ausbaustufen

| Bereich | Geplante Semantik |
| --- | --- |
| Hauptschalter | Standard aus; aktiviert nur validierte Regeln |
| Globale Regel | Separat aktivierbarer Fallback, nur ohne passende Geräteregel |
| Geräteregel | Stabile Rule-ID, Aktivschalter, Priorität, Match-Felder, vollständige Audio-Policy |
| Matching | Device-ID exakt mit ordinalem Vergleich; Namen exakt, ordinal ohne Groß-/Kleinschreibung; alle gesetzten Felder müssen passen; keine Regex/Wildcards in M1 |
| Leere Identität | Leere Match-Felder einer Geräteregel sind kein impliziter globaler Treffer; mindestens ein konkretes Feld erforderlich |
| Auflösung | Aktivierte Geräteregeln: höhere numerische Priorität gewinnt, bei Gleichstand stabile Rule-ID ordinal aufsteigend; genau eine Regel, kein feldweises Zusammenmischen |
| Audio-Policy | Geordnete Codec-Präferenzen, optionale kompatible Copy-Codecs, maximale Kanäle, maximale Bitrate in bit/s; AC3-Vorgabe zunächst 6/640000 |
| Stereo/Mono | Keine Erhöhung der Eingangskanalzahl; kompatibles Stereo erhalten; unklare oder ungewöhnliche Layouts zunächst unverändert lassen |
| Kopieren | Kompatible AC3-Spuren bevorzugt direkt spielen bzw. kopieren; Client-Copy-Verbote nicht aufheben |
| Video | Bestehende Video-/HDR-/Untertitelprofile unverändert übernehmen; Video-Copy wünschen, niemals Fähigkeiten erfinden |
| Rechte | Keine Benutzer-ID oder Berechtigungen ändern; keine Transcoding-Verbote aufheben; bei unzulässiger Zielverarbeitung nicht eingreifen |
| Fallback | Profil fehlt, Zielencoder fehlt, Konfiguration ungültig, unbekannter Adapter oder keine sichere Transformation → ursprüngliches Jellyfin-Verhalten |

Die Device-ID ist ein Match-Schlüssel, keine zusätzliche Sicherheitsberechtigung. Namen und Clientangaben bleiben vom Client berichtete Metadaten, auch wenn sie aus einem authentifizierten Kontext gelesen werden.

## Audio-Anpassung — zu validierender Entwurf

DirectPlay-Profile und Codec-Bedingungen müssen inkompatiblen Mehrkanalton ausschließen, ohne kompatibles Stereo/AC3 auszuschließen. Für Video-Transcoding müssen die passenden HLS-Profile eine zulässige AC3-Auswahl und höchstens sechs Kanäle anbieten. `CodecType.VideoAudio` mit `AudioChannels`/`AudioBitrate`-Bedingungen beeinflusst sowohl Kompatibilitätsprüfung als auch URL-Optionen; die `Conditions` eines Transcoding-Profils werden bei reinem Audiofehler nicht in jedem Fall angewandt. Nur dort 640000 einzutragen genügt deshalb nicht. [S06](sources.md#s06).

Die Discovery-Tests verwenden ein **manuell entworfenes**, kleines Profil: AC3/AAC erlaubt, AAC auf Stereo begrenzt, Transcoding `ac3,aac`, sechs Kanäle. Es erhält AC3 5.1 und AAC-Stereo als DirectPlay und liefert für DTS 7.1/AAC 5.1 einen AC3-priorisierten HLS-Plan. Das ist kein Transformer und kein Nachweis für beliebige Originalprofile.

Insbesondere darf M1 beim Umbauen nicht aus einem leeren Codec-String eine falsche Beschränkung oder unbeabsichtigte Freigabe erzeugen. Die Serverlogik bevorzugt kopierbare Audioformate und kennt qualifizierte Optionen; entsprechend muss der ganze Vertrag geprüft werden, nicht nur das erste Codec-Element. [S05](sources.md#s05), [S06](sources.md#s06), [S11](sources.md#s11).

## Nebenläufigkeit und Diagnose

Scoped Filter, unveränderliche Konfigurationssnapshots, anfrageeigene Profilkopien; zunächst kein geräteübergreifender Cache und kein globaler Playback-Zustand. Neue PlaybackInfo-Aufrufe werden unabhängig und deterministisch behandelt.

Debug-Logging ist opt-in. Geplant sind ausschließlich Reason-Code, Rule-ID, Quell-/Zielcodec, Kanal-/Bitratenlimits und ein eigener flüchtiger Korrelationswert. Keine Header, Tokens, vollständigen Claims/DTOs, Dateipfade, Benutzernamen oder URLs. Auch Exception-Texte können Geheimnisse enthalten; Adapterfehler daher mit definiertem Fehlercode statt ungefiltertem Exception-Dump ausgeben. Unvermeidbare Jellyfin-eigene Logs liegen außerhalb der Plugin-Steuerung.

## Distribution

Version 0.0.1 entspricht Assembly-/File-Version 0.0.1.0. Jellyfin-Hostpakete werden nur zum Kompilieren referenziert und nicht in die Plugin-Ausgabe kopiert. Keine automatische Release-Pipeline. `manifest.json` bleibt mit leerem `versions`-Array unveröffentlicht. Ein späteres Paket erfordert echten Download, korrekte ABI, Prüfsumme, Zeitstempel, Quellen und eine erneut geprüfte Lizenz-/Dateiliste.
