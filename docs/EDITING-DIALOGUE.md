# Editing the bots' dialogue

Everything the PerSoPer bots say lives in plain text files. You can change a line, add new
ones or give a bot a new joke, check your work, and reload it in game without rebuilding or
restarting the server.

## Quick example

In your server folder (the one with `daoc-server.sh`):

1. Open `dialogue/casual.dlg` in any text editor.
2. Find the `~ reply greeting` section and add a line under it:
   ```
   ~ reply greeting
   oh hey {0}, didn't see you there
   ```
3. Check the files: `./dialogue-check`. It prints mistakes as `file:line: what`, or nothing.
4. Reload: `./daoc-admin dialogue reload`, or `/dialogue reload` in game (GM and up).
5. Say hi to a casual bot in game. Your line is now one of the ones it can pick.

`./dialogue-check --preview KJ thank` shows what a bot (here Kobaldjovi) could answer to an
intent right now. A file with a mistake is not used: the problem goes to `logs/server.log` and
the built-in copy of that file stays, so a bot never goes silent.

## Where the files are

`dialogue/` in the server folder. Missing files are copied there from `defaults/dialogue/` when
the server starts, and an upgrade never overwrites your edits (see "Upgrading" below).

| File | Who |
|---|---|
| `casual.dlg`, `hardcore.dlg`, `minmaxer.dlg`, `joker.dlg`, `loner.dlg`, `leeroy.dlg` | The six personality types of generated world bots |
| `crafter.dlg`, `roleplayer.dlg`, `homeowner.dlg`, `herbaldoctor.dlg`, `gruffdoc.dlg` | The five types only the named bots have |
| `shempenstein.dlg`, `eaglefang.dlg`, ... (11 files) | One per named character, shared by its three realm bots |
| `persoper.dlg`, `kobaldjovi.dlg` | The two unique bots |
| `scenes.dlg` | Short scenes between two or three bots |
| `intents.dlg` | The words that tell the bots what a player meant |

Which type each named bot is: [THE-NAMED-BOTS.md](THE-NAMED-BOTS.md). A named bot uses its own
file **and** its type's file: in a section both have, its own lines come up about half the time.
A section only one of them has is used as it is.

## How a file is laid out

```
# A comment line.
=== shempenstein
names: Shemp, Shempy

~ ambient
Nice day for it, eh?

~ reply greeting
Hey there, {0}!
```

- `=== name` starts a block: the type, character or bot the lines belong to.
- `names:` lists nicknames. Players can address the bot by any of them.
- `~ section` starts a section; every line under it, up to the next section, is one thing the bot
  may say. Blank lines are ignored.
- A named character's file can add realm blocks after its main block (`=== midgard`,
  `=== albion`, `=== hibernia`); their lines are added for that realm's bot only.
- A line is said as written. Emotes are plain text, for example `::shrugs::`.

## The sections

**Chat on its own**
- `~ ambient`: lines a bot says now and then when nobody spoke to it.
- `~ opener <topic>` and `~ topic_reply <topic>` (type files): a bot starts a topic, and another
  bot answers it. Topics: `xp`, `gear`, `zones`, `classes`, `dungeons`, `rvr`, `trading`, `realm`,
  `life`, `fun`, `gripes`, `questions`.
- `~ ding`, `~ grats`, `~ own_death`, `~ other_death`, `~ drop` (type files): its level-up, a
  "grats" to another bot's, its own death, someone else's death, and loot.

**Answers to a player** (`~ reply <intent>`): what the bot says when a player's line means
that intent. `~ fallback` is used when it has no reply for it.
- Everyday chat: `greeting`, `farewell`, `thank`, `sorry`, `compliment`, `agree`, `disagree`,
  `banter`, `smalltalk`, `question`, `help`, `laugh`, `quote`, `grumble`, `abuse`, `away`,
  `back`, `trading`, `travel`, `dungeon`, `rvr`, `grinding`.
- About itself, from what it's really doing: `where`, `level`, `groupstatus`, `activity`.
- Group calls ("who wants to group?"): `groupyes`, `groupno`, `groupbusy`, `grouphave` (already
  in a group), `grouplevel` (too far apart in level).
- Orders: `orderfollow`, `orderstay`, `ordercome`, `orderassist`, `orderrest`, `orderleave` and
  `orderpull` when it says yes; `orderno` (won't), `orderbusy` (fighting or riding), `ordernot`
  (you're not its leader), `ordernotarget` (nothing to assist on), `orderfull` (nothing to rest
  off), `orderaway` (still sulking), `orderpullwhat` (no target) and `orderpulleasy` (a grey
  target).
- Jokes: `joke` and `jokeask` (tell one), `jokeguess` (guess at a riddle), `jokelaugh` and
  `groan` (a good or bad punchline), `jokeback` (one back), `jokequit` and `jokereturn` (storms
  off after a bad joke, then comes back).

**Cheeky lines:** `~ cheeky` and `~ cheeky_reply <intent>` need `[if tone_down off]` on the
section line; they're only said while `persoper_tone_down` is off.

**Persoper and Kobaldjovi** have a few sections of their own (`~ greeting`, `~ ask`, `~ song`,
`~ song_reaction` and others) that only they use.

## Fill-ins

Words in braces are filled in when the line is said:

| Fill-in | What |
|---|---|
| `{0}` | The name of the player or bot being answered |
| `{zone}`, `{monster}`, `{level}`, `{class}`, `{item}` | The bot's zone, the monster it's on, a level, a class, its last loot |
| `{destination}`, `{leader}`, `{groupsize}` | Where it's going, its group leader, its group size (in its own state answers) |
| `{bot:shnacksize}` | That named bot's name in the listener's realm (Shnacksize, Shnacksizer, Shnacksizey) |

A line whose fill-in isn't known at that moment is skipped, so a line with `{monster}` is only
said near a monster.

## Rules on a line

Add these at the end of a line, in `reply`, `cheeky_reply`, `fallback` and `ambient` sections:

```
~ reply help
Help with what, {0}?
Hold still, {0}, I'm coming! [if player_dead]
Again, {0}? That's three today. [if player_dead, deaths_today >= 3]
Ooh, shiny! Save some for me? [if recent loot] [cooldown 11m]
A song for you, {0}. [once]
```

- `[if ...]`: only said when all the conditions hold. The bot picks from the lines with the most
  conditions that hold, so a line for a special case wins over the plain ones. Some facts:
  `player_dead`, `in_combat`, `health`, `group_size`, `deaths_today`, `mood` (warm, neutral,
  cool), `time_of_day` (morning, day, evening, night), `recent` (death, kill, loot, level_up),
  `zone`, `nearby`. The checker reports a fact it doesn't know.
- `[once]`: said once to each player. `[cooldown 11m]`: not again for that long (`30s`, `11m`,
  `2h`, at most `24h`). Both start fresh when the server restarts.
- A bot skips lines it said recently while it has others, so more lines means fewer repeats.

**Swap-in parts** save writing variations out:
```
~ part greet
Hail
Well met

~ reply greeting
{greet}, {0}!
```
A part is defined before the lines that use it, one alternative per line.

## Scenes (`scenes.dlg`)

A block is a pair of types (`=== joker-loner`) or a named character with any bot
(`=== shempenstein-any`). Each scene has a size and a name, and each line says who speaks:

```
=== hardcore-casual

~ short trees
A: {b}, still in {zone}? I left there five levels ago.
B: {a}, some of us stop to look at the trees.
A: Trees don't give XP, {b}.
```

`short` has 2 to 4 lines, `medium` 5 to 8, `large` 9 to 12. `A` is the first type or the
character, `B` the other, `C` an optional third bot; `{a}`, `{b}` and `{c}` are their names. Play
one in game with `/dialogue scene hardcore-casual trees`. The top of the file has the full rules.

## What players mean (`intents.dlg`)

```
=== intents
~ intent thank
thanks, ty, cheers, appreciate it
```

A player's line counts as an intent when one of its phrases appears in it; the longest match
wins. Add a phrase here when a bot misreads something players often say. Lines no phrase
matches go to the built-in reader.

The `ordergrind` and `ordergrindstop` sections hold the chat triggers for `/grind` ("let's grind",
"stop grinding"). Only the group leader's lines count. To add a trigger, add a phrase to one of them.

## Upgrading

An upgrade keeps your `dialogue/` folder. A file you never edited still holds the old release's
lines: delete it before starting the new version and the new one is copied in. A file you did
edit keeps your version, so compare it with `defaults/dialogue/` to pick up new lines.

## More

- [DIALOGUE-DESIGN.md](DIALOGUE-DESIGN.md): why the format is the way it is.
- [BOT-PERSONALITIES.md](BOT-PERSONALITIES.md) and the character sheets in `characters/`: each
  bot's voice, to keep new lines in character.
- [RUN.md](RUN.md): running the server.

Back to the [README](../README.md).
