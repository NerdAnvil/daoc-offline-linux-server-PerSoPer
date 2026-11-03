# The 33 named bots

Eleven personality types, one named bot of each in every realm (11 x 3 = 33). A fresh server
creates all 33 at level 1 on first start, and a server that has already been played gets them
the first time it starts with this version. They level, loot and earn armour like any gamebot.
Their names are reserved, so a player can't pick one. Each realm's version shares its type's
character but has its own name, class, memory and patience.

| Type | Midgard | Albion | Hibernia |
|---|---|---|---|
| Hardcore | xXxNataSehSxXx | xAxNataSehSxAx | xHxNataSehSxHx |
| Casual | Shempenstein | Shempenshire | Shempenmore |
| Leeroy | TheriousTham | TheriousThamm | TheriousThamh |
| Loner | FemurTamer | Deaditetamer | Shroomtamer |
| Min/maxer | Redtornader | Greenfogger | Sunrayder |
| Joker | Shnacksize | Shnacksizer | Shnacksizey |
| Crafter | Muckaduck | Muckaducke | Muckaduckeen |
| Roleplayer | Hunterson | FitzHunter | MacHunter |
| Homeowner | Barbalicious | Barbaliciouse | Barbalicioush |
| Herbal Doctor | Spyburd | Spyburde | Spyburdagh |
| Gruff Doc | Eaglefang | Eaglefange | Eaglefangh |

Classes, races and characters are in [BOT-PERSONALITIES.md](BOT-PERSONALITIES.md).
In short: the Joker talks about his homeland Swissland, the Roleplayer speaks his realm's
old-fashioned tongue (and gets it a little wrong), the Homeowner brags about the house, the
Herbal Doctor buffs and chills, and the Gruff Doc is a former field-hospital medic.

Turn them off or on from the terminal or the server console. A bot that is off keeps its
progress, and one that is off (or deleted) is not recreated on restart:

```bash
./daoc-admin bots named list              # the 33, their state and level
./daoc-admin bots named off joker         # a whole type (all three realms)
./daoc-admin bots named on Shnacksize     # one bot, by name
./daoc-admin bots create --personality Shnacksize   # after a delete: starts again at level 1
```

Back to the [README](../README.md); playing with them: [PLAYING.md](PLAYING.md).
