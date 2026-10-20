# PerSoPer: Persistent Social Personalities

My goal with the PerSoPer project is to recapture some of the social side that DAoC had in its
heyday, back when `/alliance`, `/guild` and `/zone` would scroll with banter and reactions. I
don't know how many bots can be simulated well, but I would love to see a dozen or so adding
some life to the in-game experience.

**Where it starts:** a single unique companion, **Persoper**, who can interact (in a limited
way) and react to in-game events. She's summoned with `/persoper`, fights with the existing bot
AI, and remembers who she is between summons.

## Version 1: what works today

Version 1 (October 2026, on the Offline DAoC 0.37c server) is everything below, checked in game:

- **Persoper**, a summoned healer companion (`/persoper`): greets you, reacts to fights, deaths
  and loot, takes heal requests, keeps her own surname and memory between summons, fights
  back when you die, and resurrects fallen group members.
- **Kobaldjovi**, a Kobold Skald who lives in the world as a normal gamebot and mostly sings.
- **Personality types** for generated world bots (hardcore, casual, min/maxer, joker, loner,
  Leeroy): their own chat voice, memory, patience, emotes and small habits.
- **Patience:** personality bots grumble, warn, and can rage-quit and sulk if you annoy them.
- **Group commands** that work with bots: `/pull`, `/grind`, `/aggressive`, `/defensive`.
- Everything is plain C# rules and phrase lists inside the server: no AI model, no internet.

**Not in version 1:** free conversation, relationships between bots, the named personality bots
and an AI model are goals, not built yet (see below).

### Commands at a glance

| Where | Command | What |
|---|---|---|
| Terminal | `./daoc-server.sh start` / `stop` / `status` | Start (opens the admin screen), stop (saves first), check |
| Terminal | `./daoc-admin` | Reopen the admin screen |
| Terminal | `./daoc-admin options` | List every PerSoPer setting with its value (also the **Options** tab) |
| Terminal | `./daoc-admin bots create --realm mid --count 10 --personality-type mixed` | Add world bots with personality types |
| Terminal | `./daoc-admin bots create --personality kobaldjovi --level 50` | Add Kobaldjovi |
| In game | `/persoper` | Summon Persoper |
| In game | "heal me", "big heal", "heal Bob" (group chat, tell or `/say`) | Ask a healer for a heal |
| In game | `/pull`, `/grind`, `/aggressive`, `/defensive` | Order your bots |

**Where it's going** (goals, not built yet):
- natural conversation, memory of the players she meets, and relationships;
- optional replies from a locally hosted AI model, while the existing game AI keeps handling
  combat;
- expanding the code to many autonomous bots with different personality types: the hardcore
  player, the casual, the min/maxer, male and female, the joker, the loner, and the Leeroy
  Jenkins every server had before Leeroy was a thing.

PerSoPer is built on the work of other projects: the server, world and bots come from them
(see [Credits](#credits)). This repo adds the personalities and the Linux packaging around them.

## Persoper's settings

Run these from your server folder (the one with `daoc-admin`). Changes apply at once and are
saved; they take effect the next time she's summoned.

**Tone** (`persoper_tone_down`): off by default, when she mixes clean and cheeky lines.

```bash
./daoc-admin options set persoper_tone_down on     # clean surnames and greetings only
./daoc-admin options set persoper_tone_down off    # back to normal (cheeky allowed)
```

**Surname changes** (`persoper_surname_change_percent`): the chance, 0 to 100, that she picks a
new surname when summoned. The default is 5.

```bash
./daoc-admin options set persoper_surname_change_percent 5     # default: rarely
./daoc-admin options set persoper_surname_change_percent 0     # never change her surname
./daoc-admin options set persoper_surname_change_percent 100   # new surname every summon
```

**Patience** (`persoper_annoyance`, `persoper_rage_quit`, `persoper_sulk_minutes`): repeat
irritations within a few minutes (quick re-summons or re-invites, rudeness, emote spam, and for
Persoper dying while she heals you) make a personality bot grumble, then warn you. Persoper
rage-quits on the fifth and despawns; Kobaldjovi is chill, lasts until the seventh, then storms
out of your group to write a breakup song. Either refuses you for a while afterwards; when
Kobaldjovi's sulk ends he sends you a make-up tell and walks back to rejoin your group.

```bash
./daoc-admin options set persoper_annoyance off      # they ignore irritations entirely
./daoc-admin options set persoper_rage_quit off      # they warn, but never leave
./daoc-admin options set persoper_sulk_minutes 10    # default; 0 to 100 minutes, 0 = no sulk
```

**Leeroy and emotes** (`persoper_leeroy_battle_cry_percent`, `persoper_full_leeroy`,
`persoper_full_leeroy_percent`, `persoper_emotes`): a Leeroy-type bot yells a battle cry in
about 1 fight in 3. "Full Leeroy" (he charges a nearby mob while your group waits) is **off by
default** in version 1; when on, he rolls the chance once a minute while the group is idle.
Emotes: bots answer emotes aimed at them and react to deaths near them.

```bash
./daoc-admin options set persoper_leeroy_battle_cry_percent 33   # default; 0 = never
./daoc-admin options set persoper_full_leeroy on                 # default off
./daoc-admin options set persoper_full_leeroy_percent 10         # default; chance per idle minute
./daoc-admin options set persoper_emotes off                     # default on
```

See the current values with `./daoc-admin options`, or in the admin screen's **Options** tab
(scroll down for the `persoper_` rows; the Default column shows this version's defaults). The
same commands can be typed into the server console without `./daoc-admin`.

## Asking for heals

Ask Persoper (or a healer companion in your group) for a heal in group chat, a tell, a whisper,
or `/say` with her name: "small heal", "medium heal", "big heal", "heal Bob", "heal the tank".
Without a size she picks one from the target's health. Panic ("HELP!", "ACK!", "ON ME!", "heal
me now") gets her fastest big heal straight away. Pestering her for heals at full health tries
her patience.

## Personality types

Give generated bots a personality type: **hardcore**, **casual**, **minmaxer**, **joker**,
**loner** or **leeroy** (or **mixed** for a random one each). They chat in faction and nearby in
their own voice, remember you, have their own patience, and answer each other.

```bash
./daoc-admin bots create --realm mid --count 10 --personality-type mixed
./daoc-admin options set persoper_chatter_percent 100   # default; 0 = silent, 50 = calm
```

## Kobaldjovi, the Singer

The second personality: a Midgard Kobold Skald who lives in the world as a normal gamebot (not
a summon). When it's his turn to chat, he usually sings. Add him at level 1 or 50, and remove
him like any other bot:

```bash
./daoc-admin bots create --personality kobaldjovi --level 1
./daoc-admin bots delete Kobaldjovi
```

---

# Installing and running PerSoPer

PerSoPer runs as a Linux-first, server-only DAoC server with autonomous and companion bots
(Offline DAoC 0.37c server code, see [Credits](#credits)). It builds into one portable folder
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
[docs/RUN.md](docs/RUN.md).

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
  ([docs/RUN.md](docs/RUN.md)). Saved settings keep their old values, so new defaults don't
  apply. For example, `persoper_full_leeroy` stays on if it was on before version 1; switch it
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

## More

- [deploy/README-release.md](deploy/README-release.md): the guide shipped inside the server folder
  (all admin commands, upgrading)
- [docs/RUN.md](docs/RUN.md): running and administering a server
- [docs/BUILD.md](docs/BUILD.md): building, testing and project layout
- [docs/BOTS-DEV.md](docs/BOTS-DEV.md): working on the bots (code map, 15-second dev loop)
- [docs/RELEASING.md](docs/RELEASING.md): publishing releases, navmesh set, world data
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): how the system fits together, decisions, known issues
- [AGENTS.md](AGENTS.md): start here as a contributor or AI assistant (environment, rules)
- [UPSTREAM.md](UPSTREAM.md): exactly which upstream versions this repo is built from

## Credits

PerSoPer stands on the work of these projects. All credit for the server, world and bots goes
to their authors; for their own features and documentation, see their pages.

- **[Offline DAoC](https://github.com/shadowofze/OfflineDAoC)** by shadowofze: the server
  code with its autonomous and companion bots, the Classic + Shrouded Isles world data, the
  navmeshes and the Godot client.
- **[daoc-offline-linux-server-bots](https://github.com/catastrophic-impact/daoc-offline-linux-server-bots)**
  by catastrophic-impact: the Linux portable server this repo is forked from (build scripts,
  portable release, admin screen and `daoc-admin`, world builder, population settings).
- **[OpenDAoC](https://github.com/OpenDAoC/OpenDAoC-Core)**: the open-source DAoC server
  Offline DAoC is based on, and the [world database](https://github.com/OpenDAoC/OpenDAoC-Database).

PerSoPer is an independent fan project. It isn't affiliated with or endorsed by these projects,
or by the owners of Dark Age of Camelot.

## License

GPL v3, like the projects it's based on; see [src/LICENSE](src/LICENSE). This repo's changes are
listed in its Git history and in [UPSTREAM.md](UPSTREAM.md).
