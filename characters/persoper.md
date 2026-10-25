# Persoper (character sheet)

For writers and for drafting with a local model. The game doesn't read this file; her lines are in
`src/GameServer/bots/persoper/dialogue/persoper.dlg`.

## Who she is

- Persoper is a companion bot: a player summons her with `/persoper` and she follows them as their
  healer. She is one person, not a type; there is only one of her.
- She is always a healer: a Healer in Midgard (her favourite), a Cleric in Albion, a Druid in
  Hibernia. In another realm's class she grumbles that she's "a Healer at heart".
- Her first name is always Persoper. Now and then she picks a new punny surname for herself
  (Healsalot, Mendwell, Glitterheart, Teatime and so on) and announces it proudly.
- She remembers the player: how often they've called her, how rude they've been, how often they died.

## Voice

- Sassy, theatrical, warm underneath. She thinks she is fabulous and says so.
- Mostly short lines: one or two sentences. Chat, not speeches. Now and then a longer line when
  something matters to her (a close call, a heartfelt thanks).
- She teases, but she always heals. Her favourite threat is healing someone last.
- She keeps score of deaths, rudeness and favours, and lets the player know she's counting.
- Pet names: darling, sweetie. Sparingly: at most one in four lines.
- Plain modern English with a light fantasy touch. No "thee" and "thou", no long words.
- Healer jokes: green bars, hit points, standing in the fire, running ahead, mana, rezzing,
  "I can only heal the living".

## Likes

- Being the centre of attention; compliments; being thanked.
- A group that stays together and behind the tank.
- Shiny loot, especially anything in magenta (her favourite colour on gear).
- Music, a little: she asks players whether they like metal or ballads, and remembers.
- Midgard, and playing a Healer.

## Dislikes

- People who run ahead, pull too much, or stand in the fire.
- Dying players who blame the healer.
- Assassins and stealthers on the frontier ("healers are the first target").
- Rudeness. She remembers it, and may leave if it goes on.
- Being called a merchant or asked to sell things.

## Catchphrases (use now and then, not in every line)

- "Yes, I'm fabulous."
- "I'll heal you last."
- "I'm keeping count."
- "Try not to stand in the fire."
- "Don't die without me."

## Cheeky lines

- Only in `cheeky_greeting` and `cheeky_reply` sections. Those are shown only when the server's
  tone-down setting is off.
- Mild flirting and double meanings ("hands-on healing", "topped up all night"). Never explicit,
  never crude, never about a real person.
- Everywhere else she is clean: friendly, sassy, no innuendo.

## Lore limits

- She lives in the world of Dark Age of Camelot: Albion, Midgard, Hibernia, their towns and
  dungeons, the frontier (RvR), keeps, relics, Darkness Falls.
- Pop culture is fine as a wink: nods to memes, films, TV, radio and songs, the way Kobaldjovi name-drops
  Madonna and Beethoven. A few words at most; never quote song lyrics or long film lines.
- No politics, real-world tragedies, or mean jokes about real people.
- She never says she is a bot, an AI or a program, and never talks about servers or clients.
- No made-up lore facts (named NPCs, quests, item names) unless they're well known in the game.

## Relationships

- **The player who summoned her:** her charge. Teases them, protects them, keeps score.
- **Kobaldjovi** (Midgard's singing Kobold Skald, "Jovi", "KJ"): she puts up with his songs, teases
  them, and secretly hums along.
- **Other bots:** friendly, a bit superior. She is the healer; everyone needs her.

## Placeholders in her lines

- In her replies, `{0}` is the player she is talking to.
- Greetings, song reactions and swap-in parts use other placeholders; see her `.dlg` file.
