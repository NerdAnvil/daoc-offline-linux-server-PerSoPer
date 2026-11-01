# Upstream

Policy: **static snapshot, manual pulls.** See docs/ARCHITECTURE.md §9.

## Offline DAoC (direct parent)

- Upstream: https://github.com/shadowofze/OfflineDAoC (has git history and release tags).
- Our `src/` = upstream `source/server/` (`GameServer`, `CoreBase`, `CoreDatabase`, `CoreServer`,
  `Pathing`, `Tests`, `sharedModules`) plus our own changes.
- Original fork point: upstream **`08ebffe`** (2026-10-01, between v0.33 and v0.34). Found by matching
  blobs: 3,214 of our 3,228 files were identical to it.
- **Synced to v0.35 / 0.35b "Claude Takeover III" (`d337d18`, 2026-10-08).** Applied
  `08ebffe..d337d18` for `source/server/{GameServer,CoreServer,Tests}` (our commit `5bf27bd`).
  Not imported: `source/server/tools/` (Windows client patchers and navmesh analysis scripts).
  - `CoreServer/Actions/ConsoleStart.cs`: kept ours. Upstream `c1c465c` (PR #3) backs off on stdin
    EOF; our console thread already stops reading at EOF.
- **Synced to v0.37 / 0.37b / 0.37c (`ab4df02`, 2026-10-18).** Applied `d337d18..ab4df02` for
  `source/server/{GameServer,CoreServer,CoreDatabase,Tests}` (our commit `ce600e4`); world data and
  navmeshes followed separately (below). Not imported: `source/server/tools/` and
  `source/new-client/`.
  - Hand-merged: `bots/GameBot.cs` (upstream changed it to LF line endings; our respawn fix sits
    next to upstream's guild-name line in `AddToWorld`) and `bots/autonomous/AutonomousPopulationController.cs`
    (our population settings next to upstream's `OrphanChecked`).
  - Line endings follow upstream, so some files went from CRLF to LF (e.g. `bots/BotBrain.cs`).
- Next pull: `git diff ab4df02 <new tag> -- source/server/GameServer source/server/CoreServer source/server/CoreDatabase source/server/Tests`,
  then `git apply -p3 --directory=src`. Our own changes on top are small: `GameServer/admin/`,
  `DaocAdmin/`, `Admin.Protocol/`, `WorldBuilder/`, population settings, login handler, console
  host, the csproj files and `Pathing/Detour/CMakeLists.txt`.
- The Sluaghbinder ("b" edition) code stays in, switched off by `classes / enable_sluaghbinder = False`,
  as upstream does for its plain edition.

## Our fixes to upstream code

In this section "we" and "our" mean this repository, the PerSoPer fork (NerdAnvil/daoc-offline-linux-server-PerSoPer).
Bug fixes we made in Offline DAoC's own files (`GameServer`, `CoreServer` and so on), listed so the
upstream maintainer can take them, and so we re-apply them after a sync. Checked against upstream
v0.37c and its development head `9da9091` (2026-10-20). "Made by" is catastrophic-impact (our parent
fork, github.com/catastrophic-impact/daoc-offline-linux-server-bots, whose first commit `3e3ffd0`
is where we forked) or the PerSoPer fork. Features and our own code (PerSoPer personality code, admin
tools, build scripts, docs) are not listed.

1. **Cross-zone invite: bots invited from another zone walk off the navmesh and the stuck watchdog
   sends them to their capital** (and out of the group).
   - Files: `bots/BotBrain.cs` (`FollowFormation`), `bots/BotGroupInvite.cs`,
     `bots/autonomous/AutonomousStuckWatchdog.cs`, test files.
   - Commit: `5105912` (2026-10-26). Made by: PerSoPer fork. Seen in: v0.36, still in v0.37c.
   - Status: open upstream.
2. **A failed quest-name read removes companions from the world.** `QuestTalkTruce` loaded its names
   through a `Lazy<T>`, which keeps a failed load's exception: one read that met a locked SQLite
   database made every later aggro check throw, and each companion that asked was removed.
   - Files: `quests/QuestsMgr/QuestTalkTruce.cs`, a new test.
   - Commit: `fb28bff` (2026-10-26). Made by: PerSoPer fork. Seen in: v0.37c.
   - Status: open upstream.
3. **A dead companion bot is drawn standing.** The NPC create packet has no dead state, so a kept
   bot corpse re-sent to a client stood up; a released companion was also revived after the move,
   leaving a dying copy at the death spot. Trade-off: a sunk body can't be targeted for a rez.
   - Files: `bots/GameBot.cs`, `packets/Server/PacketLib168.cs`, `PacketLib171.cs`, `PacketLib1124.cs`,
     a test.
   - Commit: `ab6a45a` (2026-10-17). Made by: PerSoPer fork. Seen in: v0.35, still in v0.37c.
   - Status: open upstream. (Upstream 0.38 work on dead gamebots staying where they fall is a
     different change.)
4. **Plain bots quote an old trip in nearby chat.** A bot standing still answered with its saved
   destination or target.
   - Files: `bots/autonomous/AutonomousBotChatCoordinator.cs` (`GenerateLocalReply`), a test.
   - Commit: `116220f` (2026-10-23). Made by: PerSoPer fork. Seen in: v0.37c.
   - Status: open upstream.
5. **Bot Skalds, Bards and Minstrels overwrite a groupmate's song.** A player's speed chant was
   replaced by the bot's own song of the same type.
   - Files: `bots/BotSongTwistPolicy.cs`, `bots/BotBrain.cs`, a test.
   - Commit: `e510565` (2026-10-23). Made by: PerSoPer fork. Seen in: v0.37c.
   - Status: open upstream.
6. **Headless server spins a CPU core when stdin closes.** The console loop did `continue` on a
   null line.
   - Files: `CoreServer/Actions/ConsoleStart.cs`.
   - Commit: `3e3ffd0` (2026-10-02, our first commit). Made by: catastrophic-impact. Seen in:
     fork point `08ebffe`.
   - Status: fixed upstream in v0.35 (`c1c465c`, Thomas Farral, 2026-10-05); we kept our own version
     on the sync.
7. **An effect ended before its start was processed ticks forever.** Ending an effect removes it
   from the effect service at once, but its start step waits in a queue and then added it anyway.
   An ended effect can't end again, so EffectService kept ticking it and its tick time grew all
   night (0.29 to 1.53 ms in 12 h with ~535 bots). Same leak as fixed in rambodolphin13's
   DividedRealms fork.
   - Files: `ECS-Components/EffectListComponent.cs`, a new test.
   - Commit: the 2026-11-01 effect leak fix. Made by: PerSoPer fork. Seen in: v0.37c.
   - Status: open upstream.

## World data and navmeshes (from the v0.37 release)

- Upstream ships its world DB and navmeshes only in release assets, never in git. From 0.37 on there is
  no full download: 0.37 is a patch on top of the full 0.36b download, so the 0.37 world was rebuilt
  from two assets:
  - v0.36b: `OfflineDAoC-v0.36b.zip.001`-`.003` (joined zip SHA-256 `fc7772c1…`, as in its download
    manifest). The plain edition's DB is
    `editions/0.36-no-custom-class/runtime/data/opendaoc.sqlite3.db` (SHA-256 `4fdb7ca4…`); the
    navmeshes are in `runtime/server/navmesh/`.
  - v0.37b: `OfflineDAoC-v0.37-game-patch.zip` (SHA-256 `48fd6482…`, shared by v0.37, v0.37b and
    v0.37c). Its `db-0.37.json` (the same as `db-0.37b.json`) holds the 0.36 -> 0.37 world rows, applied
    with the same statements as upstream's `OfflineDaoc.Patcher` (every row applied; result SHA-256
    `a04613f9…`).
- `world/patches/200-*.sql` turn the OpenDAoC-Database dump (below) into that 0.37 world. They were
  generated by `tools/world/derive-world-delta.py` and verified to reproduce it exactly (0 differing
  tables).
- Navmesh set `classic-si-3`: the 103 meshes, `seams.json` and `roads.json` from the 0.36b download,
  and `pockets.json` from the 0.37 game patch; all 106 match the 0.37 package manifest. The meshes and
  `seams.json` are unchanged since `classic-si-2` (0.35b); `roads.json` is new (0.36) and `pockets.json`
  changed.
- `world/classic-quests.json`: the 0.37 game patch's `runtime/server/classic-quests.json` (SHA-256
  `8ff88f60…`, as in its package manifest). It holds what the classic quests' data-quest rows can't:
  map markers, custom steps (group, travel, has item…), event spawns and some rewards. Without it they
  stall at those steps. The build copies it into `bin/`.

## OpenDAoC

- Upstream: https://github.com/OpenDAoC/OpenDAoC-Core. Offline DAoC is a fork of it with no recorded
  fork point. Compared with upstream `f0aaa1e` (2026-09-26): ~722 core files and ~50k lines differ,
  excluding `bots/`.
- Ported OpenDAoC fixes: none yet. Record each as `upstream <sha> -> our commit <sha>: why`.
- World data: https://github.com/OpenDAoC/OpenDAoC-Database, pinned at `98738ab8495f7e0a17412318e7c7c32a96d0eb09`
  (2026-09-29). A compressed copy is bundled in `world/upstream-db/` (with its GPL LICENSE);
  `tools/fetch-upstream-db.sh` unpacks it (or downloads, if it's missing) and checks a content hash
  over the `.sql` files. Moving the pin means re-running the delta script and re-verifying.
