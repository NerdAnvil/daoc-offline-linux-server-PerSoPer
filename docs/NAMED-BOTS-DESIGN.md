# Named personality bots: design

How the 33 named bots in [BOT-PERSONALITIES.md](BOT-PERSONALITIES.md) get built. That file stays
the master for names, classes and characters; this page only says *how*.

Status: **approved 2026-10-19.** All steps (1 to 9 and 3b) done, the last on 2026-10-22.

## Builds on what exists

- **Kobaldjovi** is already a named world bot: a normal roster bot (`OfflineWorldBotRecord`)
  with its own identity file and `BotPersonality`, created with
  `daoc-admin bots create --personality kobaldjovi`.
- **Personality types** (`PersonalityTypes.cs`) give generated bots a type, saved in our own
  table `persoper_bot_type`; the roster table is untouched.
- The 33 reuse both: they are ordinary roster bots (they level, loot and earn armour like any
  gamebot), with a personality looked up from our own table.

## Storage

- **The lineup is code**, not data: one list of 33 entries (key, name, realm, class, race,
  gender, type), e.g. `joker-mid` = Shnacksize, Valkyn Berserker, female, joker.
- **One new table, `persoper_named_bot`**, one row per lineup entry:
  - `NamedKey` (`joker-mid`), `BotId` (the roster bot), `State`: `on`, `off` or `deleted`.
  - A missing row means "never created yet". That is the only case where the server creates it.
- **Characters:** one file per type (11 files) holding all three realm versions, since the
  realms share a character. Roleplayer gets three separate voices inside its file.
  (BOT-PERSONALITIES.md says "its own identity file" per bot; this is the same idea, grouped.)
- Each named bot gets its own `BotPersonality` with its own memory key (`named:shnacksize`),
  so its memories and patience are its own, like Kobaldjovi's.
- A named bot is matched by its `BotId` from the table, not by name, so renames or look-alike
  names can't confuse it.

## On by default, on/off and delete

- **On server start**, for each of the 33 with no row: create the roster bot at level 1 (no
  armour, "Random" gender rolled now and saved), save the row as `on`. Bounded: 33 entries, once.
- **New admin commands** (same grammar for `daoc-admin` and the server console):
  - `bots named list`: the 33, their state, level and whether they're online.
  - `bots named off <name|type>`: e.g. `off Shnacksize` or `off joker` (all three realms).
    The bot logs out of the world; its character and progress stay; row becomes `off`.
  - `bots named on <name|type>`: row back to `on`; it logs in with its saved progress.
- **Delete:** the existing `bots delete <name>` works as today, and also sets the row to
  `deleted`. `bots create --personality <name>` makes it again, fresh at level 1, row `on`.
  The level-50 option is refused for the 33 (Kobaldjovi keeps it).
- **Not recreated:** because `off` and `deleted` rows exist, the start-up step leaves them alone.
- The population controller skips bots whose row is `off`.

## Played (already running) servers

- The same start-up step runs, so a played server gets all 33 the first time it starts with
  this update. Nothing else on the server changes: generated bots, their types, Kobaldjovi and
  player characters are untouched.
- **The 33 names are reserved** (like Persoper's and Kobaldjovi's): a real player can never
  choose one, neither when creating a character nor when renaming one, and no generated bot
  gets one, so a named bot can always be created and log in.
- **Name already taken on a played server:**
  - by a generated bot: that bot is renamed with a fresh generated name (logged in
    `server.log`), and the named bot is created as normal;
  - by a real player character made before this update (the only way it can happen): the
    player's character is never touched; the named bot is
    skipped, logged, and saved as `off`. The owner can rename the character and use
    `bots named on`.
- **Population limit:** with `population max` set, bots log in by roster number, so the 33
  (newest numbers) might never log in. Named bots that are `on` log in first.
- Named bots are never given a random personality type by the type roll.

## Scope of this stage

This stage gives the 33 their **identity and voice**: chat replies, ambient lines, memory,
patience. The bigger behaviours in their characters (Crafter chasing profitable drops,
Homeowner's house and trophies, Herbal Doctor's buff-then-smoke) are **not** in this stage;
they go to a later stage, one at a time. Combat stays with the existing gamebot AI.

## Steps

Each step is small, on its own branch, with build and tests passing before the in-game check.

1. **Lineup and reserved names.** The 33-entry list, a unit test that every race/class pair is
   valid (if one isn't, the owner picks the replacement and BOT-PERSONALITIES.md is updated),
   and the names reserved for players (creating and renaming) and generated bots.
   *Done when I can see:* the character-creation screen refuse a new character named
   Shempenstein, and a rename to Shnacksize refused.
2. **Table and creation on start.** `persoper_named_bot`, the start-up step (including renaming
   a generated bot that holds a named bot's name), `--personality` accepting the 33, named bots
   logging in first.
   *Done when I can see:* after one restart, `daoc-admin bots named list` shows all 33 at
   level 1, and `/who Shnacksize` finds her in Midgard.
3. **Off, on and delete.** The `bots named` commands and the `deleted` mark.
   *Done when I can see:* `bots named off joker` logs out all three Jokers; after a restart
   they're still gone; `bots named on joker` brings them back at the same level.
3b. **Dialogue in text files** (added 2026-10-20). The six types' lines move out of the C#
   code into one text file per type (`dialogue/<type>.dlg`, a small format inspired by
   Dialogue Manager: blocks, sections, lines, comments, simple conditions), built into the
   server DLL and read by our own small C# reader. Same lines, nothing new in game. Persoper,
   Kobaldjovi and the scenes move in a later step. Step 4 then adds each named bot's lines as
   its own block in its type's file.
   *Done when I can see:* a Joker and a Min/maxer world bot still chatting their usual lines in
   `/say`, and `server.log` reporting the dialogue files loaded with no errors.
4. **Voices for the six existing types** (Hardcore, Casual, Leeroy, Loner, Min/maxer, Joker):
   each named bot's own lines on top of its type's.
   *Done when I can see:* Shnacksize talk about Swissland in `/say`, and Redtornader brag
   about his red tornado.
5. **Crafter voice.** *Done when I can see:* Muckaduck answer a craft request with an excuse
   and boast about her riches.
6. **Roleplayer voices** (three realms). *Done when I can see:* FitzHunter say "forsooth" and
   mix up thy and thine, and MacHunter bless me in made-up Gaelic.
7. **Homeowner voice.** *Done when I can see:* Barbalicious invite me over to see the house,
   oh dontcha know.
8. **Herbal Doctor voice.** *Done when I can see:* Spyburd ask whether I really get buffing,
   bub.
9. **Gruff Doc voice.** *Done when I can see:* Eaglefang grumble about a death like a field
   medic, then go soft.

## Decisions (owner, 2026-10-19)

- One character file per type (11), not one per bot.
- Named bots that are `on` log in first when a population limit is set.
- The 33 names are reserved so the bots can always log in; a clash on a played server is
  handled as above.
- Admin commands: `bots named list|on|off`.
