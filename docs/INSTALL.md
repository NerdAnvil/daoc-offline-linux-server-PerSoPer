# Installing and running PerSoPer

PerSoPer runs as a Linux-first, server-only DAoC server with autonomous and companion bots
(Offline DAoC 0.37c server code, see [Credits](../README.md#credits)). It builds into one portable folder
that needs no .NET install. Tested on Steam Deck and Linux Mint; it should work on any modern
distro.

The game client is not included; install it separately. The OpenDAoC client is described in
the [OpenDAoC documentation](https://www.opendaoc.com/); Offline DAoC's Godot client also works
(see [step 5](#5-connect-with-the-game-client)).

Questions and problems with PerSoPer belong on
[this repo's issues page](https://github.com/NerdAnvil/daoc-offline-linux-server-PerSoPer/issues),
not on the projects it builds on.

## Quick start

### What you need

**To run the server:** x86-64 Linux with glibc 2.34 or newer (Ubuntu 22.04+, Debian 12+,
Fedora 35+, SteamOS, Arch) and libicu (almost every distro has it). Nothing else; .NET is bundled.

**To build it, either:**
- **distrobox** (step 2a, recommended). It's preinstalled on SteamOS and packaged by most distros
  (https://distrobox.it). The build tools go into a container, not onto your system. **Or:**
- the **.NET 10 SDK, cmake, g++ and curl** installed yourself (step 2b).

Also: about **6 GB** of free disk space, and internet for the first build (for the build container and tools; the world database and navmeshes are already in the source).

Everything else is in the source, including the world and the navmeshes (`world/navmesh/`, four
~90 MB parts).

### 1. Get the source

```bash
git clone https://github.com/NerdAnvil/daoc-offline-linux-server-PerSoPer.git
cd daoc-offline-linux-server-PerSoPer
```

Or download it as a ZIP from GitHub (**Code → Download ZIP**) and unpack it.

### 2a. Build (with distrobox, recommended)

From the source folder:

```bash
tools/setup-build-box.sh --build
```

This one command:
1. Creates a build container (`daoc-server-box`, Ubuntu 22.04).
2. Installs the .NET 10 SDK, cmake and g++ inside it. Nothing is installed on your system.
3. Builds the server, the world and the navmeshes into `dist/daoc-server`.

The first run takes several minutes. Re-running it is safe.

### 2b. Build (without distrobox)

Install the **.NET 10 SDK**, **cmake**, **g++** and **curl** with your package manager, then run
`tools/build-release.sh`.

### 3. The result

```
dist/daoc-server/                              <- the server, ready to start (world and navmeshes installed)
dist/daoc-server-<version>-linux-x64.tar.gz    <- the server without navmeshes, as one file
dist/daoc-navmeshes-classic-si-3.tar.xz        <- the navmeshes, as one file
```

To run it on another Linux machine, copy `dist/daoc-server` (or the two archives, then run
`./navmesh-install.sh` with the navmesh archive next to the folder). It needs no distrobox and no
.NET there.

### 4. Start the server

```bash
cd dist/daoc-server
./daoc-server.sh start
```

The server starts in the background and an admin screen opens in the same terminal:

| Tab | What you can do |
|---|---|
| **Server** | Status, live log, **Stop server** |
| **Bots** | See every bot and what it is doing; **create** real bots (realm, level 1 or 50, class, count); **delete** |
| **Population** | Turn bots on/off; set the most bots in the world at once |
| **Accounts** | Create accounts; make an account **player / GM / admin** |
| **Options** | Server and PerSoPer settings, with their defaults; bot goal percentages |
| **RvR** | Battlegrounds, keeps, relics and raids |

- **Ctrl+Q** closes the screen and asks whether to stop the server or leave it running.
- `./daoc-admin` reopens the screen later.
- `./daoc-server.sh stop` stops the server from any terminal. It saves first.

Rebuilding replaces `dist/daoc-server`, including its world and saves. To keep a world you play
on, run your server from a copy outside `dist/`, and upgrade it as described in
[RUN.md](RUN.md).

### 5. Connect with the game client

Point the DAoC client at this machine on port **10300**. For example, with the Offline DAoC
client under Wine on the same machine:

```bash
wine connect.exe game.dll 127.0.0.1 <account> <password>
```

Offline DAoC's new **Godot client** (from its release downloads) also runs natively on Linux
with the Linux build of Godot 4.7.2 .NET. Start the server first, then:

```bash
DOTNET_ROOT=~/.dotnet <godot-4.7.2-mono-linux> --path <client>/godot -- --server=127.0.0.1 --account=<name> --password=<pw>
```

It needs a .NET SDK at `DOTNET_ROOT`. This repo doesn't include or change the client.

Accounts are created on first login. To make yours an admin, go to the **Accounts** tab, or run
`./daoc-admin accounts set-role <account> admin`. Admin or GM accounts can use GM commands,
for example `/player level 50` (with no target) to set your own level for testing.

**Players on other machines:**
1. Edit `config/serverconfig.xml` and set `<RegionIP>` to this machine's LAN address (for
   example `192.168.1.20`), or to your public address when port-forwarding.
2. Restart the server.
3. Players connect to that address. Forward TCP 10300 and UDP 10400 on your router to let in
   players from outside your home network.

## Before you install: possible conflicts

- **One DAoC server at a time.** This server uses TCP 10300 and UDP 10400, the same ports as
  Offline DAoC (Windows or Wine) and the parent Linux server. Stop any other one first; check with
  `ss -ltnp | grep 10300`.
- **Build container name.** The build container is called `daoc-server-box`, the same as in the
  parent project. If you already have one, `tools/setup-build-box.sh` reuses it. To keep them
  apart, set another name: `BOX_NAME=persoper-box BOX_HOME=~/boxes/persoper-box-home tools/setup-build-box.sh --build`.
- **Rebuilding replaces `dist/daoc-server`,** including its world and saves. Play on a copy
  outside `dist/`.
- **Upgrading a server you already play on** (from this fork or the parent project): replace
  `bin/`, `defaults/` and the scripts; `config/`, `data/` and `navmesh/` are kept
  ([RUN.md](RUN.md)). Saved settings keep their old values, so new defaults don't
  apply. For example, `persoper_full_leeroy` stays on if it was on before Alpha 1; switch it
  off in the **Options** tab. PerSoPer adds its own tables (`persoper_*`) and doesn't change
  the existing ones.
- **`/travel` doesn't work on Linux** (it drives the Windows client's keyboard). Use `/tele`
  and the stablemasters instead.

## What's in the game

- **World:** Classic + Shrouded Isles (the Offline DAoC 0.37 world), with Darkness Falls, the
  Realm Exchange and stablemaster routes.
- **Autonomous bots:** persistent characters that level, hunt, travel, group, raid and fight in
  RvR on their own. Manage them in the admin screen's Bots and Population tabs.
- **Companion bots:** summon helpers into your group:

  | Command | What |
  |---|---|
  | `/spawn [class]` | Companion class menu, or a companion of that class |
  | `/classes` | Your realm's classes and their party roles |
  | `/aggressive`, `/defensive` | Companions assist you, or wait for enemies to approach |
  | `/pull`, `/grind` | Order an engage; stationary automatic pulls |
  | `/raid 40`, `/raid 80` | A level-50 companion raid |
  | `/tele <bot>`, `/mobs`, `/stables` | Teleport to a bot or camp; monsters by level; stable routes |

More: [RUN.md](RUN.md) (running and administering a server), [BUILD.md](BUILD.md) (building
and testing). Back to the [README](../README.md).
