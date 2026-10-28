# Pass 1 for Shempenstein (qwen2.5:14b): 40 rows, one line each, aiming to keep about 20 on top of
# the 31 seed lines. He is relaxed, polite and in no hurry; "eh", "sorry", "pal", "buddy", "guy".
# "aboot" only where a row asks for it. Food only where a row asks for it.
# Openers talk to nobody in particular, so they never use {0}. other_death lines must use {0} (the
# one who fell). No realm, race, class, town or dungeon names: never write a class name; use
# {class} where allowed. Never mention the tiny wolf (realm sections only). Never name Canada.
# Allusions to his scenes are fine (the relaxed bowler, the big friendly henchman who cooks, the
# follower who carries your burdens); never a famous line word for word.
# Used up (seed lines): mobs still here after lunch, line's in by the water, second lunch to be
# fair, sideways stripes, the one that got away, imps know what they did, humming with people
# aboot, what chickens eat, pull up a rock, thinking aboot lunch, hand me your bags, don't pull the
# whole room, being sorry aboot it, want a snack, I'll carry it, heh heh I get it, take the long
# way, should be two-handed, talked aboot sweaters, always got there in the end, scenic and lost,
# when did that happen, fast delivery, buff out, floor's comfy, carry your stuff back, delivered
# eventually, too big to swing.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
ambient | 1 | between fights: he sits down the moment the group stops, however short the stop; no {0}
ambient | 1 | between fights: he swings his big two-handed weapon slowly, just to feel the weight, and is happy; no {0}
ambient | 1 | between fights: the group wiped earlier and he shrugs, saying these things work themselves out; no {0}
ambient | 1 | between fights: he argues with himself, out loud and politely, about whether to eat the snack in his pack; no {0}
ambient | 1 | between fights: he is cooking something small over a campfire and offers everyone a taste; no {0}
ambient | 1 | between fights: he wears his comfy robe in town and sees no reason to change; no {0}
reply greeting | 1 | the player says hello; Shemp says hi warmly and asks how their day's going, no rush
reply greeting | 1 | the player says hello; Shemp says sorry, he was miles away thinking about fishing
reply grouping | 1 | the player asks him to group; Shemp says sure, he'll stand in front, a bit, and carry the loot
reply abuse | 1 | the player is rude; Shemp says sorry they feel that way, and means it, and stays calm
reply joke | 1 | the player tells a joke; Shemp laughs a beat late, slowly, then properly
reply compliment | 1 | the player compliments him; Shemp blames his aunt's sweater for looking so good
reply help | 1 | the player asks for help; Shemp says no problem, he used to find lost places for a living, mostly
reply farewell | 1 | the player leaves; Shemp says take care, eh, and to come back when they've got time
reply thank | 1 | the player thanks him; Shemp says no worries, pal, happy to carry it
reply smalltalk | 1 | the player makes small talk; Shemp says "aboot" once and talks about how nice a slow day is
reply trading | 1 | the player wants to trade; Shemp asks if it's two-handed, and if not, is it at least a snack
reply travel | 1 | the player wants to travel; Shemp says sure, he knows a scenic route, it takes a while
reply dungeon | 1 | the player wants to go into a dungeon; Shemp says okay, but he's bringing snacks
reply rvr | 1 | the player wants to fight the other realms; Shemp says okay, buddy, but nobody needs to run
opener xp | 1 | he says he gets there in the end and the levels come when they come; no {0}
opener gear | 1 | he says a shield would leave a hand free for a sandwich, and he's tempted; no {0}
opener zones | 1 | he says {zone} has a good spot to sit and watch the clouds; use {zone} exactly; no {0}
opener dungeons | 1 | he says dungeons are fine but there's nowhere nice to fish; no {0}
opener fun | 1 | he says the best part of a fight is the sit-down after; no {0}
opener life | 1 | he says he's trying to eat better and it's going badly; no {0}
opener gripes | 1 | he says someone told him to hurry up today and he's still thinking about it; no {0}
opener questions | 1 | he asks if anyone else gets nervous talking in front of a crowd; no {0}
ding | 1 | he levelled up; he says nice, he'll celebrate with a sit-down and a snack
grats | 1 | someone in his group levelled up; Shemp says good on ya, buddy, and he'll carry the cake
own_death | 1 | he died; Shemp says sorry, he was a bit slow there, no harm done
other_death | 1 | {0} fell; Shemp says sorry, pal, he'll stand right here till they're back
drop | 1 | he found {item}; he says it's heavy, which is great, he likes carrying things; use {item} exactly
drop | 1 | he found {item}; he hopes it's two-handed, and if not, it can be a doorstop; use {item} exactly
reply banter | 1 | the player teases him about his belly; Shemp blames his aunt's knitting, cheerfully
reply banter | 1 | the player teases him about being slow; Shemp says slow and steady, he gets there, eventually
cheeky_reply banter | 1 | the player teases him; a light "that's what she said" about something being hard to handle; then sorry
cheeky_reply grouping | 1 | the player asks to group; a light "that's what she said" about going slow and taking his time
cheeky_reply compliment | 1 | the player compliments him; a light "that's what she said" about how big his weapon is
reply farewell | 1 | the player leaves; Shemp says he'll miss them and he's not just saying that | long
