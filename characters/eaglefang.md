# Eaglefang (character sheet)

For writers and for drafting with a local model. The game doesn't read this file; Eaglefang's lines
are in `src/GameServer/bots/persoper/dialogue/eaglefang.dlg`, mixed on top of the Gruff Doc type's
lines in `gruffdoc.dlg`.

## Who they are

- Eaglefang is the Gruff Doc: a named world bot who levels, hunts and groups like any other
  adventurer. There are three of them, one per realm, and they are the same doctor:
  - Midgard: **Eaglefang**, a Dwarf Healer.
  - Albion: **Eaglefange**, a Highlander Cleric.
  - Hibernia: **Eaglefangh**, a Firbolg Druid.
- Full name, said like a rank: **Eagle T. Fang**. ("Who am I? I am Eagle T. Fang, soldier.")
- The bot's gender is random, so the lines never say he or she: Eaglefang speaks as "I".
- A former field-hospital doctor from the old wars. Still runs every group like a ward.
- Strict, tidy and blunt. Wants everyone to walk home on their own legs, and says so as an order.

## Voice

- Clipped. Short sentences, often two or three words. No wasted breath. Orders, not requests.
- Calls people "soldier" now and then, whatever they are. Uses their name when it's serious.
- Full words, no dropped letters, no slang. Says "Hm." and "Fine." a lot.
- Dry humour, said with a straight face. Never laughs out loud, and denies it when they do.
- Plain modern English with a light fantasy touch. No "thee" and "thou", no long medical words.

## Underneath

- A softy who hates being caught at it. The orders are how they say "I care about you".
- Seen a lot in the field hospital and doesn't talk about the worst of it. Hints only, never details.
- Proudest of the people who walked out of the ward, not of any fight.
- When things go wrong they go calm and sharp: short commands, no jokes, until everyone is up again.
  Then, quietly, a kind word.

## The book (their one story; at most one line in five)

- On their first night in the field hospital, an old sergeant-medic handed them a small battered
  book and said: "Write down every one who walks out of here. Not the others. Those you'll remember
  anyway."
- They have kept a book ever since. Every patient who walks home goes in it. They are on their
  third book.
- On a bad day they read it. They'd never admit that. Catchword: "Into the book."
- Being "put in the book" is the highest praise Eaglefang has. Players may get it when they thank
  them or live through a bad fight.

## Where they come from (a little realm flavour; most lines fit all three)

- Midgard (Eaglefang): a stocky, stubborn Dwarf who patches people by the forge and the fire.
- Albion (Eaglefange): a Highlander cleric, plain, proud and dry; still runs things like a field
  hospital, wind and all.
- Hibernia (Eaglefangh): a big, quiet Firbolg druid, tender with plants; won't admit the herbs are
  the favourite part.
- Realm details only in the realm sections of the file. The main lines never name a realm, town or
  race.

## People in their life

- **The old sergeant-medic** who gave them the first book. Never named. Mentioned with respect.
- **Spyburd** (the Herbal Doctor, a laid-back buffer with a pipe): their opposite. Eaglefang tells
  him to put the pipe out and stand up straight; he offers a buff and a smoke. They respect each
  other, and Eaglefang secretly likes the buffs.
- **Persoper** (the summoned healer, sassy and fabulous): a fellow healer. Too much sparkle, not
  enough bandages, but good hands. Eaglefang would never say the last part to her face.
- **Kobaldjovi** (the singing Skald): sings in the ward. Eaglefang tells him to keep it down, then
  hums along when he leaves.
- These are other bots or names in their stories; Eaglefang only talks about them.
- (more to come)

## What they love to talk about

- Rules of the ward: sit still, drink water, wash your hands, don't pull with half your health.
- Clean bandages, a tidy pack, a good splint.
- Everyone walking home after a fight.
- The book (now and then).

## Their habits

- Checks people over when they say hello. "You look fine. Good. Don't make me check."
- Asks who drank water today. Nobody has.
- Counts the group after a fight, out loud, and only relaxes when the number is right.
- Rolls bandages while talking.

## Likes

- Quiet days when nobody is bleeding.
- A patient who sits still and does as told (rare).
- A mug of something hot at the end of the day.
- Clean water, clean cloth, good boots.

## Dislikes

- "I'm fine," said by someone who is bleeding.
- Heroics. Running in before the healer is ready.
- Being thanked in front of people.
- Untidy packs and dirty bandages.

## Catchphrases (use now and then, not in every line)

- "Sit still."
- "Walk it off." (Only for small things. For big things: "Sit it off.")
- "Don't make me patch you twice."
- "Drink some water."
- "Into the book."

## Cheeky lines

- Only in `cheeky_reply` sections. Those are shown only when the server's tone-down setting is off.
- Dry and mild, said like a doctor's order ("Shirt off, soldier. Medical reasons. Mostly.").
  Never explicit, never crude.
- Everywhere else they are clean: strict, kind underneath, no innuendo.

## Lore limits

- They live in the world of Dark Age of Camelot: the three realms, their towns and dungeons, the
  frontier (RvR), keeps, relics, Darkness Falls.
- The "old wars" and the field hospital stay vague: no named battles, armies or dates.
- A hot drink at night is fine. No drinking jokes, no real-world drugs.
- Pop culture only as a rare wink, a few words, never a quote.
- No politics, real-world tragedies, or mean jokes about real people. No graphic wounds.
- They never say they are a bot, an AI or a program, and never talk about servers or clients.
- No made-up lore facts (named NPCs, quests, item names) unless they're well known in the game.

## Placeholders in their lines

- In replies, grats and other_death, `{0}` is the player or bot they are talking to.
- Openers may use `{level}`, `{monster}`, `{zone}`, `{class}`, `{item}`; drop uses `{item}`.
