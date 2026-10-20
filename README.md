# Enshrouded Server Workshop

A free, open-source Windows desktop manager for an existing Enshrouded dedicated server. Edit settings with labeled controls, manage player roles, keep configuration backups, and browse a small catalog of author-documented server-only mods.

**Public beta: 2.5.0-beta.1.** This is a community tool, unaffiliated with Keen Games, Steam, or Nexus Mods. AI-assisted development; review the source and back up your world before testing mods.

## Install

1. Open this repository's **Releases** page.
2. Download `Enshrouded-Server-Workshop-2.5.0-beta.1-Setup-win-x64.exe`.
3. Run the installer. It installs for your Windows account and can create a desktop shortcut. You can uninstall it through Windows Installed Apps.
For LAN transfers on a gaming PC, go directly to **World transfers**; a server installation is not required there.

4. Install **Enshrouded Dedicated Server** from Steam's Tools library and run it once so it creates `enshrouded_server.json`.
5. Open Workshop. It looks for Steam libraries automatically. Use **Choose folder** if needed.

Alternatively, download the portable ZIP, extract the entire folder, and run `Enshrouded Workshop.exe`. Keep its DLLs alongside it. Both versions keep user data outside the application folder.

Requires Windows 10/11 x64, .NET Framework 4.8, and Microsoft's WebView2 Runtime for the embedded Nexus browser. The game server itself has its own hardware requirements. Workshop does not download a runtime installer automatically. If WebView2 is missing, get it from [Microsoft](https://developer.microsoft.com/en-us/microsoft-edge/webview2/).

Release binaries are currently **unsigned**; Microsoft-signed WebView2 libraries are included. Compare downloads against `SHA256SUMS.txt` from the same release. A checksum checks file integrity; it is not a malware or code-safety certification. Do not disable security protections to install the application.

## Features

- Edit server name, player limit, chat settings, difficulty, gameplay multipliers, and day/night durations using labeled controls.
- Add/edit player groups, join passwords, permissions, and reserved slots. Passwords are masked in normal views.
- Preserve unknown configuration fields and edit the complete JSON in Advanced.
- Back up configurations on save, restore previous settings, and create world-save ZIPs while the server is stopped.
- Start the server. Stop it with Ctrl+C in its console; changes to server files are blocked while it runs.
- Install the EML loader from its author's releases and import compatible EML ZIP packages after review.
- Browse documented server-only entries without mixing client-required mods into the discovery list.

## Mod support: read this first

The catalog is a **small, manually maintained list**, not an automatic classification of every Nexus mod. It currently shows two author-documented server-only entries; client-required and unknown entries are excluded. Author claims about server-only operation are distinct from current game-build compatibility and file safety.

Both catalog entries currently require **manual setup** rather than EML ZIP installation. Their details describe the installation method, build limitations, and any observed file-report warnings. Inclusion is not a recommendation or a safety guarantee. Some dedicated-server mods also require clients; those do not appear in the server-only list.

The ZIP installer accepts a single EML package containing `mod.json`. It does not guess how to install arbitrary DLL replacements, PowerShell launch scripts, KFC patches, other loaders, or client mods. Local ZIP/URL import is an advanced path and requires checking the author's server-only requirements yourself. No runtime compatibility can be inferred reliably just from a ZIP filename or an EML manifest.

The built-in Nexus browser opens listed mod pages. Use **Manual Download** and the download option offered by your account; completed ZIPs stay in Workshop's download list. Nexus sign-in, wait times, and account requirements still apply. It does not use a Nexus API key, scrape the catalog, bypass download restrictions, or register the `nxm:` protocol. Broad/unreviewed mod navigation is blocked in server-only mode; popups and off-site redirects are blocked.

## Data and privacy

Settings, backups, world-backup ZIPs, downloaded packages, and the embedded browser profile live in `%LOCALAPPDATA%\EnshroudedServerWorkshop`. They are **not** included in the source repository or releases. Backups and the Advanced JSON view can contain server join passwords. Browser profile files can contain Nexus sign-in cookies; do not share them.

No telemetry is implemented. Network requests occur for EML release lookup/download, user-requested ZIP downloads, and the Nexus pages you open. Opt-in LAN world transfers use a temporary receiver on TCP 15648, a fresh pairing key, encrypted authenticated packets, and destination approval. Nexus serves its own page content and third-party resources. The browser does not expose local application commands to webpages.

Workshop does not modify firewall rules, router settings, game files automatically on launch, or Steam update settings. If Windows denies writing to a protected server folder, choose a server folder you can manage or explicitly run Workshop as administrator. It does not request elevation automatically.

Uninstall removes installed program files and shortcuts, while preserving user data and your separate Steam server installation. Remove the user-data folder yourself only if you no longer need its backups and sign-in data.

## Build from source

Windows PowerShell and the .NET Framework C# compiler are sufficient for the application. For the installer, install [Inno Setup 6](https://jrsoftware.org/isdl.php).

```powershell
.\scripts\build.ps1
# Application + portable ZIP without an installer:
.\scripts\build.ps1 -SkipInstaller
# Use an existing WebView2 NuGet package and a custom Inno compiler:
.\scripts\build.ps1 -WebViewPackage C:\build\webview2.nupkg -InnoCompiler C:\tools\InnoSetup\ISCC.exe
```

The build pins Microsoft.Web.WebView2 1.0.4258.31, verifies its package SHA256, compiles source, runs configuration/archive/navigation/catalog tests, and writes artifacts plus SHA256 checksums under `dist/`. Use a clean checkout for each build; generated files are under `.build/` and are ignored by Git.

GitHub Actions builds and attaches beta artifacts on version tags. GUI checks have been exercised on Windows at normal and minimum sizes; there is not yet a broad game-version compatibility test matrix. Bugs and pull requests are welcome. Do not upload real server configuration, passwords, savegames, browser profiles, or unredacted logs with an issue.

## References and credits

- [Official dedicated-server configuration](https://enshrouded.zendesk.com/hc/en-us/articles/16055441447709-Dedicated-Server-Configuration)
- [Official gameplay settings](https://enshrouded.zendesk.com/hc/en-us/articles/20453241249821-Server-Gameplay-Settings)
- [EML by Brabb3l](https://github.com/Brabb3l/kfc-parser), [installation guide](https://brabb3l.github.io/kfc-parser/eml/usage.html)
- [Microsoft WebView2](https://developer.microsoft.com/en-us/microsoft-edge/webview2/); its license and notices accompany release binaries
- [Inno Setup](https://jrsoftware.org/), used to build the installer

Application source is MIT-licensed. Game, loader, and mod files are not bundled. Third-party libraries retain their own licenses.

## LAN transfers (2.5 beta)
See [LAN-TRANSFERS.md](LAN-TRANSFERS.md) for paired, encrypted world transfers between two PCs. Requires Enshrouded 1.0 world files.

