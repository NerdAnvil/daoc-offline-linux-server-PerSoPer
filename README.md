# PerSoPer: Persistent Social Personalities

My goal with the PerSoPer project is to recapture some of the social side that DAoC had in its
heyday, back when `/alliance`, `/guild` and `/zone` would scroll with banter and reactions. I
don't know how many bots can be simulated well, but I would love to see a dozen or so adding
some life to the in-game experience.

**Where it starts:** a single unique companion, **Persoper**, who can interact (in a limited
way) and react to in-game events. She's summoned with `/persoper`, fights with the existing bot
AI, and remembers who she is between summons.

## What it does

- **33 named personality bots**, one of each of 11 types in every realm, each with its own name,
  class, voice, memory and patience ([the list](docs/THE-NAMED-BOTS.md)).
- **Persoper**, a summoned healer companion (`/persoper`) who reacts, takes heal requests and
  remembers who she is; and **Kobaldjovi**, a Kobold Skald who mostly sings.
- **Personality types** for generated world bots (hardcore, casual, min/maxer, joker, loner,
  Leeroy), with their own voice, habits and patience: annoy them and they sulk.
- **Talk to them:** orders ("follow me", "stay here", "Eagle, pull this"), group calls,
  questions, jokes and guild chat, answered in each bot's own voice.
- **Scenes:** bots chat and bicker with each other in group, nearby and realm chat.
- **Dialogue in text files** you can edit and reload in game: [how](docs/EDITING-DIALOGUE.md).
- Everything is plain C# rules and phrase lists inside the server: no AI model, no internet.

**Status:** Alpha 1 plus dialogue pass 1; see the [changelog](CHANGELOG.md) for what each added
and what isn't checked in game yet. How to use it all: [docs/PLAYING.md](docs/PLAYING.md).

**Where it's going** (goals, not built yet):
- natural conversation, deeper memory of the players they meet, and relationships between bots;
- the named bots' bigger behaviours: the Crafter chasing profitable drops, the Homeowner's
  house and trophies, the Herbal Doctor's buff-then-smoke breaks;
- optional replies from a locally hosted AI model, while the existing game AI keeps handling
  combat.

PerSoPer is built on the work of other projects: the server, world and bots come from them
(see [Credits](#credits)). This repo adds the personalities and the Linux packaging around them.

## Quick start

You need x86-64 Linux, and to build it either distrobox or the .NET 10 SDK, cmake and g++. The
game client is not included. Full steps, requirements and possible conflicts:
[docs/INSTALL.md](docs/INSTALL.md).

```bash
git clone https://github.com/NerdAnvil/daoc-offline-linux-server-PerSoPer.git
cd daoc-offline-linux-server-PerSoPer
tools/setup-build-box.sh --build     # builds into dist/daoc-server (several minutes)
cd dist/daoc-server
./daoc-server.sh start               # starts the server and opens the admin screen
```

Then point the game client at this machine on port **10300**
([connecting](docs/INSTALL.md#5-connect-with-the-game-client)). Questions and problems with
PerSoPer belong on [this repo's issues page](https://github.com/NerdAnvil/daoc-offline-linux-server-PerSoPer/issues),
not on the projects it builds on.

## More

- [CHANGELOG.md](CHANGELOG.md): what each update added
- [docs/PLAYING.md](docs/PLAYING.md): commands, things you can say to the bots, Persoper's settings
- [docs/THE-NAMED-BOTS.md](docs/THE-NAMED-BOTS.md): the 33 named bots, and turning them on or off
- [docs/EDITING-DIALOGUE.md](docs/EDITING-DIALOGUE.md): changing what the bots say
- [docs/INSTALL.md](docs/INSTALL.md): installing, building, connecting, and what's in the game
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
