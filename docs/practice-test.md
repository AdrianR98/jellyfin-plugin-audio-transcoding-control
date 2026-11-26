# Kontrollierter Praxistest — Version 0.1.0, vorbereitet, nicht installiert

Ausgangscode: M1 `d173b67c3a46ea28bc850333d943572d4e5cd6a1`, `feature/m1-ac3-prototype`, Jellyfin Server 10.11.11 / Android TV 0.20.0. [Draft-PR #1](https://github.com/AdrianR98/jellyfin-plugin-audio-transcoding-control/pull/1). M1.1 ändert die Paketversion und ergänzt Bereitstellungswerkzeuge; der Audioeingriff bleibt unverändert.

## Lesend geprüfte Voraussetzungen

- Server-/API-Version und API-SHA256 entsprechen dem bereits getesteten nativen 10.11.11-Stand. Installation und Datenpfad wurden aus dem tatsächlichen Prozess ermittelt, nicht aus Standardpfaden geraten.
- Ausführung über Windows-Tray als Elternprozess, kein Jellyfin-Windows-Dienst gefunden. Neustart später über **Stop Jellyfin**, Dateiarbeiten bei beendetem Server, dann **Start Jellyfin**. Kein gewaltsames Beenden, keine zusätzliche zweite Serverinstanz. [Offizielle Tray-Dokumentation](https://github.com/jellyfin/jellyfin-server-windows/blob/master/README.md).
- Das Zielgerät wurde über die vorhandene lokale Datenbank im Nur-Lesen-Modus und eine reguläre GET-Abfrage aktiver Sitzungen identifiziert. Genau eine aktuelle Fire-TV-Sitzung meldete Android TV 0.20.0 und einen vorhandenen Benutzer. Aktuelle API-Device-ID hat Vorrang vor historischen Datenbankkennungen.
- Vollständige Device-/Benutzerkennungen stehen ausschließlich in ignorierter lokaler Konfiguration. Ein vorhandener API-Key wurde nur im Arbeitsspeicher für den lesenden GET verwendet; kein neuer Key, keine gespeicherten Tokens, keine Kennungen in PR, Dokumentation oder Testfixtures.
- Das eigene Plugin-Verzeichnis und seine XML-Konfiguration existierten noch nicht. Andere Plugins bleiben unberührt. Die konkreten Pfade stehen ausschließlich im lokalen Installationsplan/der lokalen Anleitung.

## Paket und benötigte Dateien

`scripts/package.ps1 -OutputDirectory <lokales-Ausgabeverzeichnis>` erstellt ein unveröffentlichtes ZIP mit genau vier Einträgen:

1. `Jellyfin.Plugin.AudioTranscodingControl.dll`, Assembly-/Dateiversion **0.1.0.0**.
2. `LICENSE`.
3. `THIRD-PARTY-NOTICES.md`.
4. `package-manifest.json` mit Plugin-ID, Version, Zielserver und DLL-SHA256.

Nur die eigene DLL ist eine Laufzeitdatei. Keine Jellyfin.Core-/Controller-/Model-/Api-DLL, keine ASP.NET-/NuGet-/Testbibliotheken, kein FFmpeg, keine PDB-/Entwicklungsdateien und keine Geräte- oder Zugangsdaten. ZIP-SHA256 wird separat als `.sha256` ausgegeben und muss beim Installieren ausdrücklich angegeben werden. Das Repository-`manifest.json` behält leere `versions`; kein Release und kein Plugin-Katalogeintrag.

## Private Konfiguration und Diagnose

Das native Konfigurationsformat ist XML: `<Plugins>/configurations/Jellyfin.Plugin.AudioTranscodingControl.xml`, Root `PluginConfiguration`, Felder `IsEnabled`, `EnableDebugLogging`, `Ac3Rule.IsEnabled`, `Ac3Rule.DeviceId`, `Ac3Rule.UserId`. Die vorbereitete lokale JSON-/XML-Konfiguration aktiviert beide Schalter und Diagnose ausschließlich für das ermittelte exakte Geräte-/Benutzerpaar.

AC3, höchstens sechs Kanäle und 640000 bit/s sind im Prototyp fest. Kompatibles AC3 bleibt direkt abspielbar/kopierbar; Stereo wird nicht verändert. Gültige Rechte, explizite lokale Medienquelle und bereits angebotener AC3-HLS-Support bleiben Voraussetzungen. Ein fehlender Treffer oder nicht unterstützter Pfad wird nicht durch eine globale Regel ersetzt.

Die vorgefundene globale Logstufe ist Information. Für Debug-Logs genügt deshalb der Plugin-Schalter allein nicht. Der Installer bereitet eine **neue**, ausschließlich auf die eigene Logkategorie begrenzte `logging.json` vor:

```json
{"Serilog":{"MinimumLevel":{"Override":{"Jellyfin.Plugin.AudioTranscodingControl":"Debug"}}}}
```

`logging.default.json` und andere Servereinstellungen werden nicht geändert. Falls inzwischen eine `logging.json` existiert, bricht Installation mit Diagnosekonfiguration ab, statt sie zu überschreiben. Die erzeugte Datei wird beim Rollback nur entfernt, wenn sie noch genau diesen Inhalt hat. Abweichungen verlangen manuelle Prüfung. Die Installation ist somit auf eigene Plugin-Dateien, eigene Regel und eigenen Diagnose-Override beschränkt.

## Installation erst nach ausdrücklicher Freigabe

Vor einer Freigabe sind nur `Status` und `Install -WhatIf` vorgesehen. Diese beiden Prüfungen wurden bereits am produktiven Pfad ausgeführt; kein Plugin installiert und kein Neustart ausgelöst. Installieren, Deaktivieren und Entfernen wurden zusätzlich in einem separaten Dateisystem-Sandboxplan geprüft; fremde Plugin-/Serverdateien blieben unverändert.

```powershell
$plan = '<privater-lokaler-Plan.json>'
$package = '<lokales-Plugin-ZIP>'
$sha256 = '<separat-geprüfte-ZIP-SHA256>'
./scripts/manage-practice.ps1 -Action Status -PlanPath $plan
./scripts/manage-practice.ps1 -Action Install -PlanPath $plan -PackagePath $package -ExpectedSHA256 $sha256 -WhatIf
```

**Erst nach Freigabe:** laufende Wiedergaben beenden, aktive Fire-TV-/Benutzerzuordnung nochmals abgleichen, Tray → Stop Jellyfin und Prozessende bestätigen. Dann denselben Install-Befehl ohne `-WhatIf` ausführen und die eigene ShouldProcess-Bestätigung prüfen. Der Installer verweigert tatsächliche Änderungen bei laufendem Zielserver. Er führt keinen Neustart selbst aus.

Erzeugt werden ausschließlich ein neues eigenes Plugin-Verzeichnis, eine neue eigene XML-Konfiguration und der neue Diagnose-Override. Vorhandene Versionen, Konfigurationen, unbekannte ZIP-Einträge, Hash-/Versionsabweichungen und Reparse-Points führen zum Abbruch. Ein privates lokales Zustandsprotokoll verzeichnet, welche Dateien tatsächlich angelegt wurden. Teilinstallationen ohne eindeutig verzeichnete Eigentümerschaft werden nicht blind gelöscht.

Tray → Start Jellyfin, Public-Systeminfo erreichbar/Version 10.11.11 prüfen. Danach `Status`: im neuen Startabschnitt muss `Loaded assembly Jellyfin.Plugin.AudioTranscodingControl, Version=0.1.0.0` erscheinen; optional im Dashboard Pluginstatus prüfen. Erst dann einen kurzen Test beginnen. Ein Dateiverzeichnis allein beweist keinen geladenen Filter; zusätzlich `ATC applied` und tatsächlichen Stream überprüfen.

## Deaktivieren, Deinstallieren und Rückkehr zum Ausgangszustand

Ohne geeignete Aufnahme/AVR-Ausgabe, bei Rucklern oder bei unerwarteten Video-Neukodierungen Wiedergabe beenden. Für die hier vorbereiteten dateibasierten Abläufe zunächst den Server über die Tray stoppen:

```powershell
./scripts/manage-practice.ps1 -Action Disable -PlanPath $plan -WhatIf
./scripts/manage-practice.ps1 -Action Disable -PlanPath $plan
# Alternativ/vollständig:
./scripts/manage-practice.ps1 -Action Uninstall -PlanPath $plan -WhatIf
./scripts/manage-practice.ps1 -Action Uninstall -PlanPath $plan
```

`Disable` setzt nur im eigenen XML beide Regelschalter und den Diagnoseschalter auf false. Nach Tray-Start greift die Regel nicht mehr. `Uninstall` entfernt nur das vom Installer angelegte eigene Verzeichnis, eigene XML-Datei und den unveränderten eigenen Diagnose-Override. Unbekannter Inhalt oder fremd veränderte Logging-Dateien führen zum Abbruch. Andere Plugins, globale Konfigurationsdateien und Medien werden nicht gelöscht oder aus alten Backups überschrieben.

Da vorher keine eigene Plugin-/Regeldatei und kein Override vorhanden waren, stellt deren Entfernung den ursprünglichen Plugin-/Logging-Zustand wieder her. Danach Tray-Start und nativen Kontrolltest ohne Regel durchführen. Normale Laufzeitlogs und Datenbank-Buchführung des Servers werden nicht zurückgedreht. Das private Eigentumsprotokoll wird lokal als entfernt archiviert, nicht auf GitHub hochgeladen.

## Kompakte Testmatrix

Zuerst einen kurzen bekannten Kontrollausschnitt ohne Regel protokollieren. Pro tatsächlichem Gerätetest dieselbe Medienquelle und denselben Audioindex verwenden; vorhandene Originaldateien nicht ändern. Keine große Kreuzproduktmatrix.

| Fall | Erwartung am Server | Zusätzlich am Gerät beobachten |
| --- | --- | --- |
| AC3 5.1 | Möglichst DirectPlay oder Audio-Copy, kein unnötiger AC3-Re-Encode | AVR zeigt erwartetes Dolby-Digital-Mehrkanalsignal |
| EAC3 5.1 | Bei passendem Profil AC3 ≤6ch/640000, Video möglichst Copy | Kanalrouting, Tonstabilität, Bildmodus |
| EAC3 Atmos | AC3-5.1-Fallback; Atmos-Erhaltung wird nicht erwartet | Mehrkanal statt Stereo; keine Atmos-Zusage |
| TrueHD Atmos 7.1 | AC3 auf höchstens 6ch; kein Atmos-Erhalt | Korrektes Downmix-/Kanalrouting, Synchronität |
| DTS-HD MA | DTS/DCA-Quelle → AC3 ≤6ch | Kein Stottern, korrekte Center-/Surround-Ausgabe |
| AAC 5.1 | AC3 statt vorherigem AAC-Pfad | AVR-Mehrkanalanzeige und hörbare Kanalbelegung |
| Stereo | Unverändertes Stereo, keine Erhöhung auf sechs Kanäle | Stereo bleibt korrekt |
| Tonspurwechsel | Neue PlaybackInfo-Anfrage neu bewerten; lokaler Wechsel ohne Anfrage ist Filtergrenze | Kein Tonausfall; gewählter Track stimmt |
| Seeking | Parameter durch HLS/erneute PlaybackInfo erhalten | Keine dauerhaften Aussetzer/AV-Drift |
| Anderes Gerät | Kein Regelmatch und native Entscheidung | Verhalten gegenüber Baseline unverändert |

Für jede Zeile getrennt notieren:

- **Server:** Regel angewandt/Skip-Grund, PlayMethod, ausgewählter Audioindex, tatsächliche FFmpeg-Audio-/Videoencoder, Outputkanäle und Audiobitrate; Video wirklich `copy` oder warum transkodiert? Bei DirectPlay/Copy gibt es keinen neuen Audioencoder und eventuell keinen FFmpeg-Prozess.
- **Gerät:** AVR-Anzeige und hörbares Kanalrouting; HDR-/Dolby-Vision-Anzeige des Projektors; Ruckler, Tonaussetzer und AV-Synchronität. Diese Beobachtungen sind kein Ersatz für Serverdaten und umgekehrt.
- **Ergebnis:** Servernachweis, Hardwarebeobachtung, bestanden/fehlgeschlagen/offen. `codec:v copy` allein beweist keine HDR-/Dolby-Vision-Erhaltung.

## Verwendet der Fire TV tatsächlich die TranscodingUrl?

1. Kurzes Zeitfenster und ausgewählten Track festhalten, ohne Mediennamen/Identitäten nach GitHub zu kopieren.
2. `ATC applied` bestätigt nur die Profiländerung. Danach Sitzungs-/Dashboard-Transcodingdaten und zugehöriges neues FFmpeg-Log lesen; nur die relevanten Codec-/Kanal-/Bitratenwerte übernehmen.
3. Erwartete Kette: PlaybackInfo liefert AC3-Angebot → HLS-Master/Playlist behalten `AudioCodec=ac3` → echtes FFmpeg verwendet AC3 und das Segment ebenfalls. M1 hat diese Kette am nativen Testserver belegt; die konkrete App ist noch offen.
4. Wenn `ATC applied` erscheint, der echte Stream aber AAC bleibt: prüfen, ob der Client eine eigene Streaming-URL erzeugt oder lokal einen Track wechselt. Reguläre Session-/Serverdaten können das tatsächliche Outputformat belegen; die **exakte ursprüngliche PlaybackInfo-Antwort und Requestfolge** sind mit normalen Informationslogs nicht immer vollständig sichtbar. Kein pauschales HTTP-/Header-Logging aktivieren, weil es Tokens enthalten kann. Falls zur Aufklärung ein gezielter temporärer Requesttrace nötig wird, diesen separat freigeben und sofort lokal bereinigen.
5. Keine globale AAC-Sperre als Behelf. Direkte AAC-Aufrufe ohne PlaybackInfo umgehen den M1-Filter nachweislich. Einen zusätzlichen sitzungsgebundenen Streamingfilter erst anhand des konkreten Gegenbelegs und eines neuen Auftrags planen.

Praxistest noch **nicht durchgeführt**. Installation/Neustart bleiben bis zur ausdrücklichen Freigabe aus.
