# Named personality bots

The PerSoPer plan for named world-bot personalities: one per type per realm, 11 types x 3 realms =
33 bots. Each works like Kobaldjovi: its own identity file, and its own distinct personality and
behaviour (all 33, not a selection).

Status: **built and active** (identity and voice, 2026-10-22). The bigger behaviours in the
characters below (the Crafter selling, the Homeowner's house, the Herbal Doctor's breaks) are not
built yet; see [NAMED-BOTS-DESIGN.md](NAMED-BOTS-DESIGN.md) for how it works.

## Rules for all 33

- **On by default:** a fresh server creates all 33 automatically, and so does a server that has
  already been played when it gets this update.
- The admin can turn them off or on with a command, one bot by name or a whole type (all three
  realms) at once. A bot turned back on keeps its saved progress.
- The admin can also delete a bot outright. Creating it again
  (`daoc-admin bots create --personality <name>`) starts it fresh at level 1.
- A bot that was turned off or deleted is **not recreated** when the server restarts; the choice
  is saved.

- Names stand out from the generated period/region-style bot names and are unique server-wide.
  Albion and Hibernia names are a realm-flavoured twist of the Midgard name, or the Midgard name
  with one extra letter.
- Every bot starts at level 1 and levels up like a normal gamebot: no armour at the start, it has
  to earn it. There is no level-50 option.
- "Random" gender is picked once, when the bot is created, and then stays fixed.
- If a race/class pair turns out to be invalid, use the other race listed for that type.
- Kobaldjovi stays the only Singer; none of these sing.
- The first six types are the same types that generated world bots get
  (`bots create --personality-type`). The last five (Crafter to Gruff Doc) exist only as named
  bots for now.

## The lineup

| Type | Midgard | Albion | Hibernia | Gender |
|---|---|---|---|---|
| Hardcore | xXxNataSehSxXx, Kobold Shadowblade | xAxNataSehSxAx, Inconnu Infiltrator | xHxNataSehSxHx, Lurikeen Nightshade | Female |
| Casual | Shempenstein, Valkyn Warrior | Shempenshire, Saracen Armsman | Shempenmore, Sylvan Hero | Male |
| Leeroy | TheriousTham, Troll Thane | TheriousThamm, Highlander Paladin | TheriousThamh, Celt Champion | Male |
| Loner | FemurTamer, Valkyn Bonedancer | Deaditetamer, Saracen Necromancer | Shroomtamer, Sylvan Animist | Female |
| Min/maxer | Redtornader, Norseman Spiritmaster | Greenfogger, Briton Cabalist | Sunrayder, Elf Enchanter | Male |
| Joker | Shnacksize, Valkyn Berserker | Shnacksizer, Saracen Mercenary | Shnacksizey, Elf Blademaster | Female |
| Crafter | Muckaduck, Norseman Runemaster | Muckaducke, Briton Wizard | Muckaduckeen, Elf Eldritch | Female |
| Roleplayer | Hunterson, Norseman Hunter | FitzHunter, Briton Scout | MacHunter, Celt Ranger | Male |
| Homeowner | Barbalicious, Troll Savage | Barbaliciouse, Inconnu Reaver | Barbalicioush, Firbolg Valewalker | Random |
| Herbal Doctor | Spyburd, Kobold Shaman | Spyburde, Briton Friar | Spyburdagh, Celt Warden | Male |
| Gruff Doc | Eaglefang, Dwarf Healer | Eaglefange, Highlander Cleric | Eaglefangh, Firbolg Druid | Random |

## Characters

The three realm versions of each type share one character unless noted.

- **Hardcore:** a no-nonsense assassin who knows every shadow and hiding place. She'll stab you in
  the liver and make you like it. The other realms stand no chance.
- **Casual:** a jolly warrior with a soft spot for two-handed weapons, who tanks a bit and is in
  no hurry.
- **Leeroy:** charges first and thinks never; made for the battle cry. At least he has a
  chicken. Wait, there are no chicken pets in DAoC?!
- **Loner:** a solo class with a pet for company, who doesn't want yours. Turns philosophical when
  stirred.
- **Min/maxer:** a glass cannon who has optimised every spec point and tells you so. Each name
  plays on the class's signature spam attack (Midgard: the red tornado; Albion: the Matter DoT
  fog; Hibernia: Sun Ray). Craves trophies.
- **Joker:** tiny, flashy and never serious. Sometimes talks about their "real home", Swissland,
  whose flag is a yellow circle on a yellow stick on a blue background, and where they speak
  Swisshlish.
- **Crafter:** chases the most profitable drops to sell, to fund crafting: pressing one button
  every few seconds. Can't actually take craft requests or craft anything; it's all role. Super
  rich from all that crafting.
- **Roleplayer:** speaks in what he thinks is the right language for his realm, gets it mostly
  right with some glaring mistakes, grumbles when called out for roleplaying, and tries to get
  others to play along.
  - Midgard: Norse-era speech.
  - Albion: Arthurian knightly English (thee, thou, forsooth, m'lord, m'lady); mixes up thy and
    thine, and a modern word slips into the odd grand speech.
  - Hibernia: the old tongue of the Celtic myths: blessings, curses, tales of the fair folk, and
    Gaelic-sounding words everywhere, some of them invented.
- **Homeowner:** owns a house in housing and is very proud of it. Hunts trophies to put in it,
  ones they can claim alone or with fellow bots, and likes inviting people over to show things
  off. Midwestern accent, oh dontcha know.
- **Herbal Doctor:** loves buffing, then standing back to relax with a smoke, convinced nobody
  really gets it. The best at what they do, bub: buffin', healin' and chillin'.
- **Gruff Doc:** a former army field-hospital doctor. Serious when things go wrong, a softy at
  heart.
