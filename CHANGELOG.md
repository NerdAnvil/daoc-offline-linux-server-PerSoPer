# Changelog

What each update added, newest first. How to use it all: [docs/PLAYING.md](docs/PLAYING.md).

## Dialogue pass 1 (November 2026)

It builds and its tests pass; most of it is checked in game, and the parts that aren't are
listed at the end.

- **Every named bot has its own lines.** All 11 named characters have their own dialogue file
  in their own voice, on top of their type's lines, with realm-correct names in shared lines.
- **Dialogue files you can edit:** the `.dlg` files are read from the release folder's
  `dialogue/` folder, so the writing can be changed and reloaded in game (`/dialogue reload`)
  without rebuilding. Lines can carry rules (`[if ...]` conditions, `[once]`, cooldowns), and
  bots answer to their nicknames.
- **Scenes:** two or three bots play short back-and-forth scenes in group, nearby or realm
  chat, from `scenes.dlg`.
- **Persoper asks questions** now and then and remembers your answers.
- **Bots understand more of what you say:** jokes, short reactions ("lol", "brb", "sorry"),
  and questions about themselves, answered from what they are really doing.
- **Group calls:** "who wants to group?" or "lfg" gets answers from personality bots nearby.
- **Orders in chat:** "follow me", "stay here", "come here", "assist me", "rest up" and
  "leave the group", each answered in the bot's own voice; a bot may refuse in character.
- **Named pull:** "Eagle, pull this" has that bot pull your target and the group attack.
- **Guild chat:** named bots in your guild talk there in their own voice.
- Fixes: named bots answer first in every channel, fewer quick repeats, and others.

**Not checked in game yet:** every line of every bot (checks sample them), and repeat
avoidance over a long session (unit tests only).

## Alpha 1 (October 2026)

The first release, on the Offline DAoC 0.37c server, checked in game:

- **33 named personality bots**, on by default: one of each of 11 types in each realm, every
  one with its own name, class, voice, memory and patience (see
  [docs/THE-NAMED-BOTS.md](docs/THE-NAMED-BOTS.md)).
- **Persoper**, a summoned healer companion (`/persoper`): greets you, reacts to fights, deaths
  and loot, takes heal requests, keeps her own surname and memory between summons, fights
  back when you die, and resurrects fallen group members.
- **Kobaldjovi**, a Kobold Skald who lives in the world as a normal gamebot and mostly sings.
- **Personality types** for generated world bots (hardcore, casual, min/maxer, joker, loner,
  Leeroy): their own chat voice, memory, patience, emotes and small habits.
- **Patience:** personality bots grumble, warn, and can rage-quit and sulk if you annoy them.
- **Group commands** that work with bots: `/pull`, `/grind`, `/aggressive`, `/defensive`.
- **Dialogue in text files:** the bots' lines live in `.dlg` files (one per type) that are
  built into the server, so the writing can be changed without touching the code.
- Everything is plain C# rules and phrase lists inside the server: no AI model, no internet.

**Not in Alpha 1:** free conversation, relationships between bots, the bigger behaviours in
the named bots' characters (the Crafter's selling runs, the Homeowner's house, the Herbal
Doctor's buff-and-smoke breaks) and an AI model are goals, not built yet.
