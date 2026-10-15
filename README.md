# PerSoPer: Persistent Social Personalities

My goal with the PerSoPer project is to recapture some of the social side that DAoC had in its
heyday, back when `/alliance`, `/guild` and `/zone` would scroll with banter and reactions. I
don't know how many bots can be simulated well, but I would love to see a dozen or so adding
some life to the in-game experience.

**Where it starts:** a proof of concept with a single unique companion, **Persoper**, who can
interact (in a limited way) and react to in-game events. Today she's summoned with
`/persoper`, fights with the existing bot AI, and remembers who she is between summons.

**Where it's going** (goals, not built yet):
- natural conversation, memory of the players she meets, and relationships;
- optional replies from a locally hosted AI model, while the existing game AI keeps handling
  combat;
- expanding the code to many autonomous bots with different personality types: the hardcore
  player, the casual, the min/maxer, male and female, the joker, the loner, and the Leeroy
  Jenkins every server had before Leeroy was a thing.

Built on [catastrophic-impact's Linux DAoC server](https://github.com/catastrophic-impact/daoc-offline-linux-server-bots)
(described below); all credit for the server and bots to its authors.

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

See the current values with `./daoc-admin options`. The same commands can be typed into the
server console without `./daoc-admin`.

## Asking for heals

Ask Persoper (or a healer companion in your group) for a heal in group chat, a tell, a whisper,
or `/say` with her name: "small heal", "medium heal", "big heal", "heal Bob", "heal the tank".
Without a size she picks one from the target's health. Panic ("HELP!", "ACK!", "ON ME!", "heal
me now") gets her fastest big heal straight away. Pestering her for heals at full health tries
her patience.

## Kobaldjovi, the Singer

The second personality: a Midgard Kobold Skald who lives in the world as a normal gamebot (not
a summon). When it's his turn to chat, he usually sings. Add him at level 1 or 50, and remove
him like any other bot:

```bash
./daoc-admin bots create --personality kobaldjovi --level 1
./daoc-admin bots delete Kobaldjovi
```

---

# DAoC Server

A Linux-first, server-only DAoC server with autonomous and companion bots, based on a pinned
OpenDAoC fork from Offline DAoC (synced to 0.35). It builds into one portable folder that needs no .NET
install. The game client is not included; players install it separately. I recommend the opendaoc client which can be found in OpenDAOC's documenation:
https://www.opendaoc.com/ 

This is based off of Shadowofze's work at: https://github.com/shadowofze/OfflineDAoC

This project is meant to provide a fast and simple portable server on Linux instead of Windows. Tested on steam deck
but should work on any modern distro.

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
dist/daoc-navmeshes-classic-si-2.tar.xz        <- the navmeshes, as one file
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

Accounts are created on first login. To make yours an admin, go to the **Accounts** tab, or run
`./daoc-admin accounts set-role <account> admin`.

**Players on other machines:**
1. Edit `config/serverconfig.xml` and set `<RegionIP>` to this machine's LAN address (for
   example `192.168.1.20`), or to your public address when port-forwarding.
2. Restart the server.
3. Players connect to that address. Forward TCP 10300 and UDP 10400 on your router to let in
   players from outside your home network.

## What's in the game

- **World:** Classic + Shrouded Isles (the Offline DAoC 0.35 world), with Darkness Falls, the
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
- [AGENTS.md](AGENTS.md): start here as a contributor or AI assistant (folders, environment, rules)

## License

GPL v3. Based on OpenDAoC; see [src/LICENSE](src/LICENSE).
