# Playing with the PerSoPer bots

Commands, the things you can say, and the settings. The named bots are listed in
[THE-NAMED-BOTS.md](THE-NAMED-BOTS.md); to change what they say, see [EDITING-DIALOGUE.md](EDITING-DIALOGUE.md).

## Commands at a glance

| Where | Command | What |
|---|---|---|
| Terminal | `./daoc-server.sh start` / `stop` / `status` | Start (opens the admin screen), stop (saves first), check |
| Terminal | `./daoc-admin` | Reopen the admin screen |
| Terminal | `./daoc-admin options` | List every PerSoPer setting with its value (also the **Options** tab) |
| Terminal | `./daoc-admin bots create --realm mid --count 10 --personality-type mixed` | Add world bots with personality types |
| Terminal | `./daoc-admin bots create --personality kobaldjovi --level 50` | Add Kobaldjovi |
| Terminal | `./daoc-admin bots named list` | List the 33 named bots, their state and level |
| Terminal | `./daoc-admin bots named off joker` / `on Shnacksize` | Turn a named bot (or a whole type) off or on |
| In game | `/persoper` | Summon Persoper |
| In game | "heal me", "big heal", "heal Bob" (group chat, tell or `/say`) | Ask a healer for a heal |
| In game | `/pull`, `/grind`, `/aggressive`, `/defensive` | Order your bots |

## Things you can say to the bots

Personality bots (the named bots, Persoper and the typed world bots) answer in their own voice.
Say these in group chat, `/say` or a tell; a bot's name or nickname ("Eagle", "Shemp") points
it at one bot.

- **Orders:** "follow me", "stay here", "come here", "assist me" (while you're fighting),
  "rest up", and "leave the group" (to one named bot). A bot may say no in character: busy,
  fighting, or still sulking.
- **Named pull:** target a mob and say "Eagle, pull this": that bot pulls it, whatever its
  role, and the rest of the group attacks at once. A grey mob gets "too easy" instead.
- **Group calls:** "who wants to group?", "anyone up for a dungeon?" or "lfg" gets a few
  answers from personality bots nearby, from what they're really doing.
- **Questions:** "where are you?", "what level are you?", "are you in a group?", "are you
  busy?" are answered from the bot's real zone, level, group and task.
- **Jokes:** they guess at riddles, groan at bad punchlines and now and then tell one back.
  A really bad joke may make one storm off for a moment.
- **Short reactions:** "lol", "brb", "sorry" get a short answer now and then.
- **Guild chat:** named bots in your guild talk there in their own voice.

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
default** in Alpha 1; when on, he rolls the chance once a minute while the group is idle.
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

Back to the [README](../README.md).
