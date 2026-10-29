# Pass 2 for Barbalicious (qwen2.5:14b): 40 rows, one line each, aiming to keep about 20 on top
# of the 52 lines so far. THIS PASS: never "dontcha know" and never "Sure thing"; Gerald at most
# where a row names him; no trophy wall unless a row asks. Barb is the Homeowner: talks about the house, invites everyone over, does
# every job properly and safely; warm, polite, a little proud, a twist in every line. A light
# Midwestern touch (ope, real nice, oh for sure), at most one per line. GENDER-NEUTRAL: never he,
# she, sir, ma'am, guy, gal, lady or lad about themself. Never shouting in capitals. Calls people
# by name; never "pal", "buddy", "guy", "bub", "friend" or "darling".
# Their weapon is "my weapon" or its name: never its kind (fists, claws, flail, scythe), never
# their race, class, magic or realm (realm sections only, even if an example line shows them).
# Openers talk to nobody in particular, so they never use {0}. other_death lines must use {0} (the
# one who fell). No realm, race, class, town, dungeon or team names: never write a class name; use
# {class} where allowed. Backstory only where a row asks for it.
# Allusions to their scenes are fine (the proud lawn keeper, the patient safety-first helper in
# flannel, the sunny polite cop calm in a mess and thinking about lunch); never a famous line word
# for word.
# Used up (seed lines): diagonal stripes nobody's seen, rat catcher not one rat I check, dad's shop
# bought a house he's come round, snake in the garden check under the porch, eat better then a
# Used up (seed lines and pass 1): diagonal stripes, rat catcher not one rat, dad's shop bought a
# house, snake under the porch, eat better then a hotdish, measure twice pull once, come see the
# porch, wipe your feet, gloves on ready, heck of a thing, tell it at the barbecue, haven't seen the
# porch, the right tool, count the turns, rush in maybe not, boots off, forgot cousin's birthday,
# weapon named Gerald, shelf level first try, cat on the rug, two in the morning, gloves first
# fence fingers, plaque by the door, bars to celebrate, shoulda measured, deep breath nice and
# easy, going on the wall, new fence keeps rats out, porch sky blue, ladders hold the base, trophy
# wall record of deeds, peaceful porch, doozy tell the neighbours, nobody messes with my place,
# step back best way to tackle, house quieter diet success, one board at a time, wind plays tricks,
# lighting and a handrail, fixing fence Saturday, moss off a roof, {class} should own a house,
# rats avoid my house, find my way home, roof like armour, home improvement level, portrait,
# hotdish when I get back, warm bed ready, Gerald will have company, talk about my fence.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
ambient | 1 | between fights: they name their second weapon (make up a plain, homely name) and introduce it politely; no {0}
ambient | 1 | between fights: they say their dad still asks when they're coming to run the shop, and they talk about the house instead; no {0}
ambient | 1 | between fights: they admire somebody's well-kept armour the way they'd admire a tidy lawn; no {0}
ambient | 1 | between fights: they say the lawn would be perfect if the neighbours' weeds stayed on their side; no {0}
ambient | 1 | between fights: they stay calm about something going wrong nearby and ask if anyone packed lunch; no {0}
ambient | 1 | between fights: they say a snake looked at them funny earlier and they haven't forgotten; no {0}
reply greeting | 1 | the player says hello; they say hi and ask how the player's own house is doing
reply grouping | 1 | the player asks them to group; they say yes and ask who's bringing snacks
reply compliment | 1 | the player says something nice; they go a bit shy, then say thanks very politely
reply thank | 1 | the player thanks them; they say it's what neighbours do
reply smalltalk | 1 | the player makes small talk; they say it's a real nice day for yard work
reply trading | 1 | the player wants to trade; they ask if the item is well made, they only buy things built to last
reply travel | 1 | the player wants to travel; they say yes, and that they've packed bars for the trip
reply dungeon | 1 | the player wants to go into a dungeon; they say yes, but no running, mind the stairs
reply help | 1 | the player asks for help; they roll up their sleeves and say let's do it properly
reply grinding | 1 | the player wants to grind for a while; they say it's like mowing, nice and steady, row after row
reply abuse | 1 | the player is rude; they say, kindly, that's no way to talk, and offer them a bar anyway
reply joke | 1 | the player tells a joke; they laugh politely, a little late, then say oh, I get it
reply banter | 1 | the player asks if they ever do anything fun; they say last week they reorganised the tool shed
reply farewell | 1 | the player is leaving; they say drive safe, out of habit, and then laugh because there's no driving
opener xp | 1 | they say they don't rush levels, a good foundation takes time; no {0}
opener zones | 1 | they say {zone} is real nice, but they'd want to check the drainage first; use {zone} exactly; no {0}
opener rvr | 1 | they say they fight in the frontier, but they keep thinking about whether they left the stove on; no {0}
opener trading | 1 | they say they only buy things built to last, like a good porch; no {0}
opener gripes | 1 | they complain, very politely, that the neighbours' dog dug up the flower bed; no {0}
opener questions | 1 | they ask if anyone remembers whose birthday is this week, because they've a feeling it's someone's; no {0}
opener life | 1 | they say they're trying to eat better, and list one healthy thing and then three hotdishes; no {0}
opener life | 1 | they say one day there'll be songs about them, or at least a nice plaque; no {0}
opener fun | 1 | they say they'd like a quiet barbecue with everyone, and nobody dying; no {0}
opener dungeons | 1 | they say they'd never live in a dungeon, the damp would ruin the floors; no {0}
ding | 1 | they levelled up; they're quietly proud, like finishing a deck
grats | 1 | someone levelled up; they say grats and that it deserves a bar or two
own_death | 1 | they died; they say, calmly, that's a heck of a mess, and they'll tidy up after
own_death | 1 | they died; they say at least they didn't die in the house, the floors are new
other_death | 1 | {0} fell; they say ope, and that {0} should've worn gloves, gently
other_death | 1 | {0} fell; they say {0} can't stay down there, the floor's cold, up you get
drop | 1 | they found {item}; they say it's well made, built to last; use {item} exactly
drop | 1 | they found {item}; they say it'd make a nice housewarming gift for someone; use {item} exactly
opener life | 1 | OFF-GAME: the person playing them is up very late again, and the cat is staring at them; the whole line is off-game; no {0}
reply farewell | 1 | the player leaves after helping with something; they say plainly that their dad wanted them to run the shop, and they think the house turned out all right, then a small joke | long
