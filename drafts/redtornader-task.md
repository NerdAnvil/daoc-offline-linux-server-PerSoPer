# Pass 1 for Redtornader (qwen2.5:14b): 40 rows, one line each, aiming to keep about 20 on top of
# the 26 seed lines. He is a proud glass cannon who has optimised every spec point and says so;
# short boasts, made-up but precise-sounding numbers, a twist in every line. An exclamation mark
# now and then, never capitals. Calls people by name; never "pal", "buddy", "guy", "bub" or "friend".
# His spell is "my nuke", "my main spell", "my rotation" or "my spec": never red tornado, fog,
# Sun Ray, spirits, simulacrum or any pet's kind (realm sections only, even if an example line
# shows them).
# Openers talk to nobody in particular, so they never use {0}. other_death lines must use {0} (the
# one who fell). No realm, race, class, town, dungeon or team names: never write a class name; use
# {class} where allowed. Backstory only where a row asks for it.
# Allusions to his scenes are fine (the trophy-wall boaster, the proud rival who can't stand a
# lucky natural, the numbers man with the one right answer); never a famous line word for word.
# Used up (seed lines): every spec point every cap ask me how, lamps for a living light up
# monsters, bear stole my kill I have its numbers, forty pennies no idea what for, Tham
# out-damaged me by luck recalculating, emphasis on cannon, perfect timing tell someone my
# numbers, fully optimised and ready, stand behind me not too close I'm fragile, damage meter
# mostly me, well done me as calculated, low damage I checked, 30% more damage that's help, joke
# 12% crit didn't crit, worked out the fastest route, tell people about my numbers, parents think
# I'm still a lamplighter, box of boss teeth sorted by size, rare drop for months drop rate
# doesn't care, back at launch twice as long, three more points I know where they go, spend
# those points properly I'll send a list, calculated calculation was wrong, zero points in
# healing, going on the wall, pretend you didn't say that my spreadsheet won't.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
ambient | 1 | between fights: he reads out his own damage from the last fight, proudly, with a precise made-up number; no {0}
ambient | 1 | between fights: he is re-planning his spec points in his head, for fun, and says so; no {0}
ambient | 1 | between fights: he polishes a trophy from his last kill and admires it out loud; no {0}
ambient | 1 | between fights: he looks at someone's armour and says it's a waste of points, kindly but smugly; no {0}
ambient | 1 | between fights: he admits, in one short line, that his armour is paper, and says that's the point; no {0}
ambient | 1 | between fights: he wants someone to say he did well, hints at it, and pretends he didn't; no {0}
reply greeting | 1 | the player says hello; he says hello back and asks how much damage they did today, hoping it's less than his
reply grouping | 1 | the player asks him to group; he says yes, but first asks for everyone's numbers
reply abuse | 1 | the player is rude; he isn't hurt, he says his damage speaks for itself
reply joke | 1 | the player tells a joke; he works out exactly how funny it was, as a number
reply compliment | 1 | the player says he did well; he is so pleased he forgets to boast, then remembers
reply help | 1 | the player asks for help; he says yes, as long as he can stand at the back and do the damage
reply farewell | 1 | the player leaves; he reminds them who did the most damage, then says come back soon
reply thank | 1 | the player thanks him; he says it was nothing, then says it was actually a lot, with a number
reply smalltalk | 1 | the player makes small talk; he steers it back to his spec within one line
reply trading | 1 | the player wants to trade; he only wants items that add damage, and says so
reply travel | 1 | the player wants to travel together; he says fine, but if a bear shows up he's not sharing the kill
reply dungeon | 1 | the player wants to go into a dungeon; he wants the boss, for a trophy, and says he'll stand well back
reply rvr | 1 | the player wants to fight the other realms; he is excited, says enemy players are the best trophies
reply banter | 1 | the player says he dies a lot; he says glass cannons are meant to, it's part of the build
reply banter | 1 | the player says someone else did more damage; he refuses to believe it and asks to see the numbers
opener xp | 1 | he says he has worked out the fastest way to level, to the minute; no {0}
opener gear | 1 | he says he would trade any armour for one more point of damage; no {0}
opener zones | 1 | he says {zone} has the best monsters for trophies; use {zone} exactly; no {0}
opener dungeons | 1 | he says a boss is just a trophy that hasn't fallen over yet; no {0}
opener fun | 1 | he says his idea of fun is watching a big health bar go down fast; no {0}
opener gripes | 1 | he complains that someone out-damaged him today by pure luck, and it wasn't fair; no {0}
opener questions | 1 | he asks why anyone would put a point into anything that doesn't do damage; no {0}
opener classes | 1 | he says every {class} he meets has their spec wrong, and he could fix it; use {class} exactly; no {0}
opener life | 1 | a chicken was near him today and he did not like it; one short line, no {0}
ding | 1 | he levelled up; he says it happened exactly when he calculated it would
grats | 1 | someone in his group levelled up; he says grats and asks where their points are going
own_death | 1 | he died first again; he says glass breaks, and blames the healer's timing, not his armour
own_death | 1 | he died; he says at least his damage was top before he fell
other_death | 1 | {0} fell; he says he'll avenge them with a big hit, and keep the trophy
other_death | 1 | {0} fell; he admits, briefly, that he needs them alive or he's next
drop | 1 | he found {item}; he checks if it adds damage, and is disappointed it doesn't; use {item} exactly
drop | 1 | he found {item}; he says it will look great on his trophy wall; use {item} exactly
cheeky_reply banter | 1 | the player says something that sounds wrong; he raises an eyebrow and gives it a low score
reply farewell | 1 | the player leaves after a long night of grouping; he admits, a bit awkwardly, that they were a good group and he couldn't have done the damage without them | long
