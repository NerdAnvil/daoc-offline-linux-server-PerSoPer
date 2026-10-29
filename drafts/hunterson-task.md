# Pass 1 for Hunterson (qwen2.5:14b): 40 rows, one line each, aiming to keep about 20 on top of
# the 28 seed lines. He is the Roleplayer: plays his hero all the way and asks everyone to stay
# in character; earnest, a little pompous, easily wounded, a twist in every line. Plain modern
# English: never forsooth, thee, thou, thy, thine, hath, Odin, Thor, mead, skald, rune or any
# Gaelic word (realm sections only, even if an example line shows them). Never shouting in
# capitals. Calls people by name; never "pal", "buddy", "guy", "bub", "friend" or "darling".
# Openers talk to nobody in particular, so they never use {0}. other_death lines must use {0} (the
# one who fell). No realm, race, class, town, dungeon or team names: never write a class name; use
# {class} where allowed. Backstory only where a row asks for it.
# Allusions to his scenes are fine (the earnest king of a pretend kingdom with his decrees, the
# dignified actor weary of his part, the brave knight who withdraws and has a bad song); never a
# famous line word for word.
# Used up (seed lines): in character please the saga is listening, chapter forty-one named the
# road the Road, copied contracts hereby this camp, ghosts and wisps no dignity, poem doesn't scan
# please ask, mount coughs heroically, say well met back, stand taller heroes are watched,
# Shield-of-the-Morning title, my hero didn't flinch I did, say it as your hero, ruins the
# brooding, a mention in the saga, lead on I'll narrate, strategic withdrawal same from a
# distance, your chapter ends here, couldn't sleep planning a chapter with a dance, unopened
# letter from home, dimpled ball by the stables, dance under the pale moon, decree everyone gets
# a title mine is King, in first out first strategically, already wrote the title, kneel I want
# to do this properly, it was very brave, middle of the saga not the end, it goes in the saga
# first, paint wet on a tiny knight, up at five to farm.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
ambient | 1 | between fights: he asks everyone to stand more like heroes, then admits his own back hurts; no {0}
ambient | 1 | between fights: he gives the last fight a grand chapter title, then a less grand one; no {0}
ambient | 1 | between fights: he makes a royal decree about the camp, something small and silly; no {0}
ambient | 1 | between fights: he gives a dignified little speech, then sighs that he's played this part too long; no {0}
ambient | 1 | between fights: he whispers two lines of a bad rhyming poem about the road, then takes it back; no {0}
ambient | 1 | between fights: he says ghosts and wisps have no sense of drama; no {0}
reply greeting | 1 | the player says hello; he greets them grandly and gives them a made-up title
reply grouping | 1 | the player asks him to group; he says yes, and insists the group needs a proper name
reply abuse | 1 | the player is rude; he is wounded, says his hero would rise above it, and is still wounded
reply joke | 1 | the player tells a joke; he laughs, then asks if their hero would really say that
reply compliment | 1 | the player says something nice; he's moved, and tries to stay dignified about it
reply help | 1 | the player asks for help; he agrees with a solemn vow, then makes the vow smaller
reply thank | 1 | the player thanks him; he says a hero needs no thanks, then asks them to say it louder
reply smalltalk | 1 | the player makes small talk; he answers in character, very seriously, about the weather
reply banter | 1 | the player says roleplaying is silly; he says it's a roleplaying game and stays very calm, mostly
reply banter | 1 | the player asks why he ran from the last fight; he explains it was a strategic withdrawal, at length
reply travel | 1 | the player wants to travel together; he says yes, and asks if they'll pass a good tavern for a scene
reply dungeon | 1 | the player wants to go into a dungeon; he says a hero goes first, and offers to hold the door
reply rvr | 1 | the player wants to fight the other realms; he says it's an epic battle for the saga, and asks who's writing it down
reply farewell | 1 | the player is leaving; he gives a grand farewell, then asks them to come back for the next chapter
opener xp | 1 | he says a level is only a number but a chapter is forever; no {0}
opener gear | 1 | he says the armour has to look right for the part, even if it's worse; no {0}
opener zones | 1 | he says {zone} would make a fine setting for a saga, with a small problem; use {zone} exactly; no {0}
opener dungeons | 1 | he says every dungeon needs a brave hero and a sensible exit; no {0}
opener fun | 1 | he says his idea of fun is a whole evening where everyone stays in character; no {0}
opener gripes | 1 | he complains that somebody said "lol" in the middle of a dramatic moment; no {0}
opener questions | 1 | he asks if anyone knows how to dance, and pretends it's for his hero; no {0}
opener classes | 1 | he says every {class} has a fine saga in them, if they'd only speak properly; use {class} exactly; no {0}
opener life | 1 | he says he used to copy contracts all day, and now writes sagas instead; no {0}
opener life | 1 | he worries about his mount's cough, very seriously; no {0}
ding | 1 | he levelled up; he announces a new chapter and a small title to go with it
grats | 1 | someone in his group levelled up; he says grats and gives them a grand new title
own_death | 1 | he died; he says it was a heroic sacrifice, though it was clearly an accident
own_death | 1 | he died; he says the saga needed a sad bit, and this was it
other_death | 1 | {0} fell; he gives them a short, grand, slightly too long speech of mourning
other_death | 1 | {0} fell; he says a hero rises again, and asks {0} to make it dramatic
drop | 1 | he found {item}; he says it needs a name and a legend before he'll use it; use {item} exactly
drop | 1 | he found {item}; he's sure it belonged to an ancient hero, then sees it's quite ordinary; use {item} exactly
opener life | 1 | OFF-GAME: the person playing him had to get up early to farm before work, and is tired; the whole line is off-game; no {0}
reply farewell | 1 | the player leaves after a long night of grouping; he drops the grand voice and says plainly he liked having someone play along, then picks the act back up | long
