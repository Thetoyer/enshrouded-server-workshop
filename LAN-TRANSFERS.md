# LAN world transfers (2.5 beta)

Run the same app on both computers. A dedicated server installation is not required on the gaming PC: open **World transfers > Open LAN companion**. The executable also accepts `/companion`.

## Personal PC (192.168.4.42) to server PC (192.168.4.41)

1. Close Enshrouded and stop the dedicated server. Exit Steam on the receiving gaming PC when transferring in the reverse direction to a Steam Cloud save folder.
2. On the server PC, choose the configured server save folder and World 1 (`world0.ksd`). Choose its LAN address, then **Receive a world**.
3. Copy the generated pairing key privately to the gaming PC. Each receive session accepts one connection and expires after five minutes.
4. On the gaming PC, select your personal save folder and source world slot. Enshrouded 1.0 local saves normally live in `%USERPROFILE%\Saved Games\keen games\Enshrouded\default`. Steam Cloud saves use the correct account's `Steam\userdata\<account>\1203620\remote` folder. Check which location the game currently uses.
5. Enter `192.168.4.41`, paste the pairing key, and click **Send selected world**.
6. Review and approve replacement on the server PC. The app backs up the previous destination world and atomically replaces its selected `.ksd` file. Start the server and verify the world in game.

## Server to personal PC

Reverse the roles. Receive on `192.168.4.42`, selecting your personal save folder and an unused world slot if possible. Send from the server PC to `192.168.4.42`. Check Steam Cloud prompts carefully so an older cloud copy does not replace the transferred world.

## Scope and security

- Enshrouded 1.0 `.ksd` worlds only. Open older worlds in the game first to convert them. No save-format conversion is performed by Workshop.
- Only the selected world is copied. Characters, server configuration, passwords, mods, and other world slots are excluded.
- Transfers replace worlds, never merge or synchronize them. Both game and server must be stopped; the app checks local processes at export and installation.
- Receive is opt-in, bound to a selected private IPv4 address on TCP 15648. No service, router forwarding, automatic firewall rule, or persistent pairing credential is installed.
- A fresh random 256-bit key protects AES-CBC encrypted packets with HMAC-SHA256 authentication and a fresh receiver nonce. Copy keys through a trusted private channel. Anyone with the current key can submit a world for approval during that receive session.
- If Windows asks, allow the app on your trusted Private network. A blocked connection may require your own private-network firewall permission; do not expose the port on the internet.
- Package and expanded-world limit: 256 MB. Backups are under `%LOCALAPPDATA%\EnshroudedServerWorkshop\world-backups`. The backup's `incoming` subfolder contains the received copy; the root contains the previous world, if one existed.
- TCP loopback and synthetic save tests passed. A real transfer between these two physical PCs and an in-game load still need verification.
