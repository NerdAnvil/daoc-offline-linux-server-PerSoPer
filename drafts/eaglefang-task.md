# Pass 3 for Eaglefang (qwen2.5:14b): 40 rows, one line each, aiming to keep about 24 and reach
# about 100 own lines (76 now). Pass 2 kept 24 of 40. No book rows this time. Openers talk to
# nobody in particular, so they never use {0}; class openers use only {class}, never a class name.
# No realm, race, class, town or dungeon names (the same lines serve three realms), no he/she
# about Eaglefang, no drinking jokes, nothing graphic.
# Used up: water, boots, clean cloth, counting bandages, pale/eaten, "I'm fine" while bleeding,
# "sit it off", heroes need a medic, glitter vs bandages, humming along, the book, the tidy pack,
# wet bandages, clean water, "I did not smile", posture, check-ups, the tank, pulse up, haggling,
# old wars, the sergeant-medic, Spyburd standing straight, the patient who came back, quiet zone,
# cat and bear, the group nap, needles and mending, "unprofessional", rust.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
reply greeting | 1 | the player says hello; Eaglefang says they look like they slept badly, and asks why; don't use the word soldier
reply banter | 1 | the player jokes with them; Eaglefang says they'll write it down as a symptom; don't use the word soldier
reply banter | 1 | the player jokes with them; Eaglefang admits it was funny, then says it never happened; don't use the word soldier
reply abuse | 1 | the player is rude; Eaglefang says they've had worse from patients with no teeth; don't use the word soldier
reply grouping | 1 | the player wants to group; Eaglefang says yes, as long as nobody says "I've got this" before a fight; don't use the word soldier
reply grinding | 1 | the player talks about grinding; Eaglefang says every tenth fight is a rest stop, doctor's rule; don't use the word soldier
reply travel | 1 | the player talks about a long trip; Eaglefang says blisters have ended more journeys than dragons; don't use the word soldier
reply rvr | 1 | the player talks about the frontier; Eaglefang says they'll be right behind them, close enough to grab a collar; don't use the word soldier
reply rvr | 1 | the player talks about the frontier; Eaglefang says a keep wall is not a reason to stop ducking; don't use the word soldier
reply dungeon | 1 | the player wants to go into a dungeon; Eaglefang says fine, but nobody touches anything that glows; don't use the word soldier
reply trading | 1 | the player wants to trade; Eaglefang offers bandage rolls as currency; don't use the word soldier
reply help | 1 | the player asks for help; Eaglefang says they're already rolling up their sleeves; don't use the word soldier
reply farewell | 1 | the player says goodbye; Eaglefang says go, and don't come back on a stretcher; don't use the word soldier
reply thank | 1 | the player thanks them; Eaglefang says it's the job, and the job is enough; don't use the word soldier
fallback | 1 | Eaglefang didn't catch it; they were busy watching someone limp and ask the player to say it again; don't use the word soldier
ambient | 1 | between fights: the hardest part of the job is waiting while others fight; don't use the word soldier
ambient | 1 | between fights: Kobaldjovi, the singing Skald, wrote a song about bandages, and Eaglefang pretends to hate it
ambient | 1 | between fights: Persoper, the summoned healer, borrowed bandages and gave them back folded wrong; don't use the word soldier
ambient | 1 | between fights: a medic's best day is one where nobody needs them at all; don't use the word soldier
ambient | 1 | between fights: someone once asked Eaglefang if they ever get scared, and the honest answer is every time, then the hands take over | long
opener xp | 1 | {monster} gave good experience and nobody got hurt; use {monster} exactly; no {0}; don't use the word soldier
opener xp | 1 | a few more fights and Eaglefang reaches level {level}, which means more patients; use {level} exactly; no {0}; don't use the word soldier
opener classes | 1 | a {class} always says they don't need a healer, right up until they do; use {class} exactly, no other class names; no {0}; don't use the word soldier
opener classes | 1 | Eaglefang has patched more {class} folk than they can count; use {class} exactly, no other class names; no {0}; don't use the word soldier
opener gear | 1 | someone showed off {item}, and Eaglefang only asked if it was clean; use {item} exactly; no {0}; don't use the word soldier
opener zones | 1 | the paths in {zone} are full of loose stones and twisted ankles; use {zone} exactly; no {0}; don't use the word soldier
opener dungeons | 1 | dungeon air is bad for lungs, so keep the visit short; no {0}; don't use the word soldier
opener rvr | 1 | after a frontier fight, Eaglefang counts heads before anything else; no {0}; don't use the word soldier
opener rvr | 1 | a keep siege is loud, long and full of people who won't sit still; no {0}; don't use the word soldier
opener trading | 1 | the merchants never have enough clean cloth, and Eaglefang tells them so every time; no {0}; don't use the word soldier
opener fun | 1 | Eaglefang's bedside manner is famous, and not in a good way; no {0}; don't use the word soldier
opener life | 1 | a good day ends with clean hands and a full pack; no {0}; don't use the word soldier
opener questions | 1 | Eaglefang asks if anyone slept more than four hours last night; no {0}; don't use the word soldier
opener gripes | 1 | someone took off a bandage early because it itched; no {0}; don't use the word soldier
opener gripes | 1 | nobody ever reports a small cut until it's a big one; no {0}; don't use the word soldier
grats | 1 | someone levelled; Eaglefang says good, and that it means they're harder to break now; don't use the word soldier
other_death | 1 | someone went down; Eaglefang says sharply that they don't have permission to stay down; don't use the word soldier
own_death | 1 | Eaglefang died; they say they'll be filing a complaint with themself; don't use the word soldier
ding | 1 | Eaglefang levelled up; they say it changes nothing, the bandages still need rolling; don't use the word soldier
drop | 1 | Eaglefang found {item}; they ask if anyone needs it more, before taking it; use {item} exactly; don't use the word soldier
