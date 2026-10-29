# Bot dialogue: design

Status: draft for review (2026-10-23). Nothing here is built yet unless it says so.

## Goals

- Anyone can change, add or remove bot dialogue with a text editor, without rebuilding the server.
- Named bots feel natural in everyday chat: they pick out the common kinds of thing a player types
  (greetings, thanks, questions about their likes, grouping, trading), react to what just happened,
  remember answers, and rarely repeat themselves. Their understanding is limited: anything outside
  those kinds gets an in-character "didn't catch that" rather than a real answer.
- The game stays pure C#. No AI model ships with the server, and no one needs one to play.
- A local AI model may be used **while writing** to draft lines and scenes. Its output is a normal
  dialogue file that must pass the checker before it goes into the game.

## Where the ideas come from

| Idea | Borrowed from |
|---|---|
| File layout: blocks, sections, `[if ...]` | Dialogue Manager (Nathan Hoad, Godot) |
| Lines carry conditions; the most specific match wins; say-once and cooldowns | Valve's dynamic dialog (Left 4 Dead, Team Fortress 2), Hades |
| Swap-in parts for variety | Tracery (Kate Compton), RimWorld |
| Sort the player's text into intents, then answer the intent | Facade (Mateas and Stern) |
| Short question with branching answers | Dialogue Manager, Yarn Spinner |

## Files

```
dialogue/
  casual.dlg, joker.dlg, ...     the six personality types (one file each)
  kobaldjovi.dlg, persoper.dlg   one file per unique bot
  spyburd.dlg, eaglefang.dlg ... one file per named character (11), shared by its three realm
                                 bots: "=== spyburd" for the character, and optional
                                 "=== midgard", "=== albion", "=== hibernia" for realm flavour
  scenes.dlg                     scenes between two or more bots
  intents.dlg                    words and phrases that sort player text into intents
characters/
  kobaldjovi.md, spyburd.md ...  one character sheet per unique bot or named character
drafts/                          model or writer drafts; never loaded by the game
```

- The game reads `dialogue/` from the release folder. The built-in copies stay in the server as
  a fallback: if a file is missing or has errors, the errors are logged and the built-in copy of
  that file is used, so a broken file never leaves a bot silent.
- A named character's lines are mixed on top of its type's lines (`hardcore.dlg`, `gruffdoc.dlg`...),
  so the character grows out of its type. A named bot without a character file still uses its
  block in its type's file.
- Character sheets describe voice, likes, dislikes, catchphrases, lore limits and relationships,
  plus a short backstory rolled from `drafts/backstory-rolls.md` and then edited by hand.
  The game doesn't read them.

## Format additions

All additions are optional. A file that uses none of them works as it does today.

**Nicknames** (in the bot's block):
```
=== kobaldjovi
names: Jovi, KJ
```
Players can address the bot by any of these; other bots may use them too.

**Conditions, say-once and cooldowns** (per line):
```
~ reply help
Help with what, {0}?
Hold still, {0}, I'm coming! [if player_dead]
Again, {0}? That's three today. [if player_dead, deaths_today >= 3]
I'd sing you back to life, but that's not how it works. [if player_dead, mood warm] [once]
Ooh, shiny! Save some for me, {0}? [if recent loot] [cooldown 11m]
```
Conditions are split by commas and are about the player the bot is talking to. Forms:
`[if player_dead]`, `[if not in_combat]`, `[if health < 30]`, `[if mood warm]`,
`[if zone camelot city]`. A cooldown takes seconds, minutes or hours (`30s`, `11m`, `2h`, at most
24h). For now, rules work in `reply`, `cheeky_reply` and `fallback` sections; `[once]`, cooldowns
and recent events are kept in memory and start fresh when the server restarts.

**Swap-in parts** (defined once per block, used inside lines):
```
~ part greet
Hail
Well met
Oi

~ reply greeting
{greet}, {0}!
```
Rules: a part is defined with `~ part name` (2 to 24 lower-case letters or `_`) **before** the lines that
use it, and one alternative goes on each line. Lines use it as `{name}`. The game's own slots
(`{zone}`, `{monster}`, `{level}`, `{class}`, `{item}`, `{a}`, `{b}`, `{c}`) can't be part names.
Parts are filled in when the file is read: a line with a part becomes one line per alternative (all
pairings when it has several), and each keeps the line's `[if]`, `[once]` and cooldown tags. So a
`[once]` or cooldown line with parts is once per filled-in line, not once per written line. A line
with parts is more likely to be picked than a plain one, as it counts as several lines. Caps (the checker
reports them): 8 parts per block, 20 alternatives per part, 3 parts per line, a part may use a part, but not one that uses a part (2 levels),
24 filled-in lines per written line. A part's lines carry no tags, and
parts don't work in `~ ask` yet.

**Intents** (`intents.dlg`):
```
=== intents
~ intent thank
thanks, ty, cheers, appreciate it
```
A phrase counts when its words appear in a row; the longest phrase wins. Rude and real-world
words are checked first; text no phrase matches goes to the built-in classifier as before. A bot
answers an intent from its `~ reply <intent>` section, or its fallback lines.

**A question with branching replies** (one question, one reply):
```
~ ask fav_music
What do you listen to, {0}? Metal or ballads?
-> metal: A person of taste!
-> ballads: Ooh, a softie. I respect it.
-> other: Never heard of it. I'll write a song about it anyway.
```
The answer is saved per player (`player_likes metal`) and later lines can check it.

**Scenes** (`scenes.dlg`): short scripts between two or more bots, picked by the same rules.
Target: 10 per pair.

## How a line is picked

1. Collect the lines of the matching section.
2. Drop lines whose conditions are false, `[once]` lines already said to this player, and lines
   still on cooldown.
3. Keep the lines with the most conditions (the most specific match).
4. Among those, skip recently used lines where possible, then pick at random.
5. Fill in `{0}`, nicknames and parts.

Every step has a fixed cap (lines per section, parts per line, nested parts), as all bot code must.

## Facts the rules can check (starter set)

Alive or dead, zone, time of day, health, mood, relationship level, recent events (death, kill,
loot, level-up), who is nearby, group size, in combat, deaths today, and saved player answers.
More can be added later; the checker rejects unknown facts.

Names in step 1: `player_dead`, `in_combat` (yes/no); `health` (percent), `group_size`,
`deaths_today` (numbers); `mood` (warm, neutral, cool; this is also the relationship level for
now); `time_of_day` (morning, day, evening, night, by the game clock); `recent` (death, kill,
loot, level_up, within 5 minutes); `zone` and `nearby` (a name). Step 5 adds `player_likes`
(`[if player_likes metal]`: the player's saved answer to some question is `metal`; `other` never
counts).

## Player names

Bots start shortening a player's name (e.g. "Kevinator" to "Kev") once the relationship is
friendly.

## Tools

- **Checker and preview** (a separate small program): reads any `.dlg` file and reports mistakes
  as `file:line: what`; previews what a bot would say in a given situation. Used both by writers
  and to check model drafts.
- **Reload**: a server admin command so edited files take effect without a restart.
- **Drafting with a local model**: the model (for example a Hermes model in Ollama, LM Studio or
  llama.cpp, on a local GPU; speed doesn't matter) gets the character sheet, the format rules and
  example lines, and writes to `drafts/`. Files that pass the checker can be copied into
  `dialogue/`. A person reads them first. For a new named character, a writer first does the
  character sheet and about 20 to 30 seed lines that set the voice; the model drafts the rest from those.

## Build order (one branch each)

1. Rules in the reader (`[if]`, `[once]`, cooldowns, most specific wins, no repeats) and the
   starter facts.
2. Nicknames and `intents.dlg`.
3. Load `dialogue/` from the release folder, plus the checker, preview and reload.
4. Scenes moved from C# into `scenes.dlg`.
5. Questions with branching replies, and saved answers per player.
6. Swap-in parts.
7. Character sheets and the drafting workflow; Persoper and Kobaldjovi to 503 lines each, then the
   11 named characters (about 100 lines each to start).

## Decisions (owner, 2026-10-23)

- Model use is for writing only; nothing ships with the game.
- Valve/Hades-style rules, optional per line.
- Nicknames live in the `.dlg` blocks.
- Persoper and Kobaldjovi: 503 lines each. Named characters start at about 100 lines of their own
  and grow later if the chatter isn't too much (owner, 2026-10-25). Mostly model-drafted and reviewed.
- A small set of swap-in parts.
- Intents in an editable file; one question and one reply; answers saved per player.
- Starter set of about 15 facts; 10 scenes per pair; short player names once friendly.
- Each unique bot (Persoper, Kobaldjovi) and each named character (one for the three realm bots of
  a type) has its own `.dlg` file and character sheet. The realm versions differ only in a few lines.
- Checker and preview are a separate small program.

## Open

- A locally run web page for the server, the checker and the dialogue files. It needs its own
  design (it opens a local port).
