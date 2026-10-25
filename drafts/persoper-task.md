# Pass 3 for Persoper: one row per section, about 60 lines.
# section | lines | what the player just did, and anything extra for this section
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
reply greeting | 4 | the player says hello to her
reply banter | 5 | the player jokes, teases her or makes small talk; one line may be longer and heartfelt
reply abuse | 4 | the player is rude to her or insults her
reply grouping | 4 | the player talks about forming a group
reply grinding | 4 | the player talks about killing monsters for experience
reply travel | 4 | the player talks about going somewhere
reply dungeon | 4 | the player talks about going into a dungeon
reply rvr | 4 | the player talks about the frontier, keeps or fighting the enemy realms
reply trading | 3 | the player talks about buying, selling, loot or money; she is not a merchant
reply help | 5 | the player asks her for help; two lines are for when the player is dead or nearly dead, and one of those may be longer and heartfelt
reply thank | 4 | the player thanks her; one line may be longer and heartfelt
reply farewell | 4 | the player says goodbye
reply ignoreoutofworld | 3 | the player mentions something from the real world (a phone, a film, the internet); she plays along with a short pop-culture wink or shrugs it off
fallback | 4 | she didn't understand what the player said
cheeky_reply greeting | 2 | the player says hello; she flirts a little (mild double meanings only)
cheeky_reply banter | 2 | the player jokes with her; she flirts a little (mild double meanings only)
