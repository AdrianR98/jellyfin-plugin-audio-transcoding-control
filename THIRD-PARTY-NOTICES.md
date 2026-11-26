# Herkunft und Lizenzprüfung

Prüfstand: 26.11.2026. Die Prüfung erfolgte vor einer Codeübernahme. Projektlizenz: **GPL-3.0-only**. Der vollständige Lizenztext in `LICENSE` stammt unverändert aus dem offiziellen Template. Diese Bestandsaufnahme ersetzt keine umfassende Prüfung einer späteren Distribution.

## Offizielles Jellyfin-Plugin-Template

- Quelle: `jellyfin/jellyfin-plugin-template`, Commit `c93225a0a5a76d3843db05b4b5b77fcfc482fba3`.
- Geprüft: vollständige `LICENSE` (GPL Version 3), Plugin-Einstiegspunkt, Konfigurationsklasse, Projekt- und Build-Eigenschaften. [Fixierte Quellen](docs/sources.md#s21).
- Übernahme/Anpassung: GPL-Lizenztext sowie das grundlegende Muster `BasePlugin<TConfiguration>` mit hostinjizierten Pfaden und XML-Serializer. `Plugin.cs` wurde umbenannt, mit eigener GUID versehen und auf den M0-Umfang reduziert; Beispiel-Webseite und Beispieloptionen wurden weggelassen.
- Attribution: Jellyfin-Plugin-Template, Jellyfin contributors. Vorhandene Herkunft und Lizenz bleiben hier sowie im Quellcodevermerk dokumentiert.
- Paketversionen des Templates (10.11.5) wurden nicht übernommen; dieses Projekt referenziert die untersuchte Serverversion 10.11.11.

## Jellyfin Server und NuGet-Pakete

- Quelle: Server `v10.11.11`, Commit `1fbd8739292cce610231be93daf43368733edf63`.
- `MediaBrowser.Controller`, `MediaBrowser.Model`, `MediaBrowser.Common` deklarieren die Paket-IDs `Jellyfin.Controller`, `Jellyfin.Model`, `Jellyfin.Common`, Version 10.11.11 und `GPL-3.0-only` in ihren Projektdateien. Der Root-Lizenztext des Server-Repositories ist GPL Version 2; diese unterschiedlichen Hinweise werden nicht pauschal gleichgesetzt. [Paketbelege](docs/sources.md#s01), [Server-Lizenz](https://github.com/jellyfin/jellyfin/blob/1fbd8739292cce610231be93daf43368733edf63/LICENSE).
- Verwendung: öffentliche Plugin-/Modell-APIs per NuGet, Analyse des Servercodes; kein Servercode wurde in die Implementierung kopiert.
- Die beim Restore abgerufenen `.nuspec`-Metadaten von Controller und Model bestätigen jeweils `net9.0`, `GPL-3.0-only` und genau den analysierten Server-Commit. Zusammengefasste Paketbelege stehen in `docs/source-lock.json`.
- Laufzeit: Jellyfin liefert die Hostbibliotheken. `ExcludeAssets="runtime"` verhindert deren Beilage im Plugin-Build. Der Testprozess referenziert sie ausdrücklich zur lokalen Ausführung.
- Aufgelöste transitive Abhängigkeiten und Integritätshashes stehen in den beiden `packages.lock.json`; NuGet-Metadaten bleiben im Paketcache. Vor einer späteren Veröffentlichung sind die dann tatsächlich beigelegten Dateien und deren Lizenzpflichten erneut zu prüfen.

## DirectPlayForce

- Quelle: `upchui/Jellyfin-DirectPlayForce`, Commit `4b6abc8325f3b7ad8a7c90e062dd52154dd60547`.
- Befund: README nennt MIT und verlinkt `LICENSE`, aber im geprüften Git-Baum befindet sich **keine Lizenzdatei**. Damit fehlt der vollständige referenzierte Lizenztext einschließlich eventueller Copyright-Hinweise. [Belege](docs/sources.md#s23).
- Entscheidung: ausschließlich architektonisch gelesen. Keine Implementierung, Konfiguration, HTML-Seite, Grafiken oder Distributionsskripte übernommen. Das eigene Filtergerüst verwendet unabhängig die dokumentierten Jellyfin-/ASP.NET-APIs.
- Die dort verwendeten Zielwerte `net8.0`/Jellyfin 10.9.11 sind kein Kompatibilitätsbeleg für Jellyfin 10.11.11.

## Jellyfin Android TV und Testwerkzeuge

- Android TV `v0.20.0`, Commit `45c52925661ff51427cea46ebabcd3de656276cc`: Root-Lizenztext GPL Version 2; ausschließlich Analyse, keine Übernahme von Clientcode. [Lizenz](https://github.com/jellyfin/jellyfin-androidtv/blob/45c52925661ff51427cea46ebabcd3de656276cc/LICENSE).
- ASP.NET Core und Microsoft-Testwerkzeuge sowie xUnit werden als Framework-/Testabhängigkeiten verwendet; keine Implementierungsdateien daraus kopiert. Die ASP.NET-Core-Quellreferenzen tragen MIT-Hinweise. Testpakete gehören nicht zur Plugin-Ausgabe. Versionen und Abhängigkeitsbäume sind in Projektdateien und Lockfiles festgelegt.
