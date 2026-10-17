# 2.4.0-beta.1

First public Windows beta of Enshrouded Server Workshop.

Downloads:

- **Setup-win-x64.exe**: per-user installer, Start menu shortcut, optional desktop shortcut, Windows uninstaller.
- **portable-win-x64.zip**: extract the whole folder and launch the EXE.
- **SHA256SUMS.txt**: integrity hashes for those files.

Features include labeled server/gameplay settings, player-role editing, configuration backup and restore, world-backup ZIPs, server startup, EML ZIP review/import, and a small catalog of author-documented server-only mods.

Requires Windows 10/11 x64, .NET Framework 4.8, an existing Steam dedicated-server installation, and WebView2 Runtime for Nexus browsing. Steam libraries are detected automatically; a folder picker is available. User data is stored under Local AppData, outside the installation folder.

Limitations:

- This is beta software with AI-assisted development. Back up your world before changing mods.
- Application and installer binaries are unsigned. No paid signing certificate has been obtained. Do not disable security protections if Windows or your organization blocks them.
- Local configuration/archive/navigation/catalog tests passed. GUI checks were exercised at normal and minimum sizes. Installer compilation passed, but local unattended installation/uninstallation could not be fully validated because the test environment denied registry/shortcut writes and then blocked the test installer under Application Control. CI includes an installer file-deployment/uninstall test that omits OS shortcut/registry registration; that is not a full Windows integration test.
- The catalog has two server-only entries, classified from explicit author statements. It is not a complete/live Nexus index. Current-game compatibility and file safety are not certified. Both catalog entries need their authors' manual setup rather than automatic EML ZIP installation.
- Nexus account/download requirements still apply. No Nexus API key or bypass is used. Off-site navigation and popups are blocked.
- No game, EML loader, or mod binaries are bundled. No telemetry is implemented.

Please report issues with reproduction steps and redacted screenshots. Never attach real join passwords, server settings, saves, browser profiles, or unredacted logs.
