# DAoC server: start here (AI assistants and developers)

This file is loaded by Claude Code as `CLAUDE.md`. Read it fully before changing anything, then the
doc for your task:

| Task | Read |
|---|---|
| Anything about bots | [docs/BOTS-DEV.md](docs/BOTS-DEV.md): kinds, lifecycle, code map, dev loop, plan |
| How the system fits together | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| Building and testing | [docs/BUILD.md](docs/BUILD.md) |
| Running, admin, upgrades, troubleshooting | [docs/RUN.md](docs/RUN.md) |
| Releases, navmesh set, world data, DB pin | [docs/RELEASING.md](docs/RELEASING.md) |
| Upstream pins | [UPSTREAM.md](UPSTREAM.md) |
| Why things are the way they are | [docs/history/](docs/history/) |

## What this is

A Linux-first **Dark Age of Camelot server** (OpenDAoC fork, .NET 10, SQLite) with
**autonomous gamebots** and **companion bots**. It's released as one portable folder with a
built-in Classic + Shrouded Isles world; navmeshes ship as a separate archive. The game client is
not part of the project. The owner's goal now is to **develop the bots**: smarter, natural
behaviour, dungeons, battlegrounds, keeps and frontier, and later chat.

## Repo and upstream

This repo is github.com/NerdAnvil/daoc-offline-linux-server-PerSoPer, a fork of
github.com/catastrophic-impact/daoc-offline-linux-server-bots. Its server code comes from
https://github.com/shadowofze/OfflineDAoC (see `UPSTREAM.md`); clone that to a scratch folder
when you need to compare or pull. Its release zips hold the world DB and navmeshes.
`rewrite/` and the old `project/` Linux build are gone; see `docs/history/`.

## Environment and how to run things

- **Building:** either the .NET 10 SDK, cmake and g++ installed on the machine, or a distrobox
  build box (`tools/setup-build-box.sh`, default name `daoc-server-box`, home
  `~/boxes/daoc-server-box-home`, SDK at `~/.dotnet/dotnet` inside it; safe to re-run). With a
  box, run the commands below inside it; it sees your home folder, so build the repo in place.
- **Common commands** (from the repo root; use the full path to `dotnet` if it isn't on PATH):
  - Build and test: `cd src && dotnet build DaocServer.sln -c Release && dotnet test Tests/Tests.csproj -c Release --no-build`
  - Full release: `tools/build-release.sh`. About 3 minutes; creates `dist/daoc-server`.
  - Test server: copy `dist/daoc-server` to a folder outside the repo, then
    `tools/dev-deploy.sh <folder>` for quick code updates (about 15 s).
  - Run it: `<folder>/daoc-server.sh start` (or `start --console`); admin:
    `<folder>/daoc-admin bots list` and so on.
- **Ports:** game TCP 10300, UDP 10400. Only one server at a time. Check with
  `ss -ltnp | grep 10300` before starting.

## Gotchas

- **CRLF:** many source files use CRLF line endings. Preserve them when editing (check with
  `file`). Don't rewrite whole files with a different ending.
- **The build needs `src/CoreServer/config/serverconfig.xml`** to exist (it's gitignored). Copy it
  from `serverconfig.example.xml`; the scripts do this.
- **Server root is `bin/`:** `bin/config`, `bin/navmesh` and `bin/logs` are symlinks to the
  folder's own directories.
- **Bots are NPCs pretending to be players** (`GameBot : GameNPC, IGamePlayer`). Core files
  special-case them, so bot changes can touch `GameNPC`, `SpellHandler` and other core files.
- **Navmeshes are in the source** as `world/navmesh/*.part-*` (four ~90 MB parts, under GitHub's
  100 MB limit). `build-release.sh` joins, verifies and installs them into `dist/daoc-server`.
  Rebuilding replaces `dist/`; play on a copy elsewhere.
- **`world/patches/200-*.sql` are generated.** Never hand-edit them; add `300-*` files instead
  (docs/RELEASING.md).
- **Don't trust "it compiles":** check bot behaviour on the dev server (TUI Bots tab, `/tele`,
  `logs/server.log`).
- **`/travel` doesn't work on Linux** (it drives the Windows client's keyboard). `&tc` is
  registered twice. Both are known issues (docs/ARCHITECTURE.md §8).

## Rules

- **Protect player data.** Never commit a played database, logs, backups, credentials,
  `serverconfig.xml` or `admins.json`. `.gitignore` is a safety net only.
- **Keep the owner's gameplay rules:**
  - real loot, inventories, coins and equipment upgrades;
  - the Realm Exchange;
  - stablemaster routes;
  - selling only at natural task breaks;
  - gamebots start without armor and earn it;
  - Darkness Falls is open to solo bots.
- **Tell bot types apart:** real players, companion bots and autonomous gamebots are different.
- **No unbounded loops in bot code.** Every loop, retry and search has an explicit cap.
- **No client assumptions in `src/`:** no `game.dll`, client paths or input injection (the
  `/travel` legacy is a known exception to remove).
- **Don't start a server, or deploy over a running one, without the owner's OK.** The owner often
  has a server running on 10300.
- **Be honest in reports:** automated tests and in-game checks are separate claims. Don't
  describe Darkness Falls raid AI, Legion or the hardest level 70+ content as implemented.
- **Upstream is a static snapshot:** port fixes by hand and cite the upstream commit.
- **Communication:** the owner prefers short, direct answers and is careful with token cost. Do
  the work, report briefly, and don't start large explorations without saying why.
