# Pass 1 for FemurTamer (qwen2.5:14b): 40 rows, one line each, aiming to keep about 20 on top of
# the 30 seed lines. She is dry, flat and unimpressed; short sentences, full stops, no exclamation
# marks. Calls people by name or nothing; never "pal", "buddy", "guy", "bub" or "friend".
# Her pet is "my pet", "the pets" or "my little army": never bones, skeletons, femurs, shades,
# undead, mushrooms or turrets (realm sections only, even if an example line shows them).
# Openers talk to nobody in particular, so they never use {0}. other_death lines must use {0} (the
# one who fell). No realm, race, class, town, dungeon or team names: never write a class name; use
# {class} where allowed. Backstory only where a row asks for it.
# Allusions to her scenes are fine (the deadpan outsider rating everything, the bored grump who
# secretly shows up, the witch of the wilds who scorns towns and groups); never a famous line word
# for word.
# Used up (seed lines): pet doesn't talk so we get along, more room for me, four layers and a
# grudge, bellows at a forge, wolves chew what isn't theirs, dragon scale stew, everything ends
# hopefully this conversation, Tham brave and dead, that's all I've got, you found me I was hiding,
# no then fine my pet goes first, I just happen to be where you are, it's a long list, it doesn't
# have a choice, not going to laugh but I see it, I meant to come this way, don't make it weird,
# sibling better and nice about it, what's left after a fight, saw it nobody there perfect,
# sportball checked the score twice, nobody clapped good, don't let it go to your head, quiet at
# least, pet will guard you it was bored, mine now don't look at it, did you hear yourself,
# studying, food's here hold the pull.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
ambient | 1 | between fights: the group stops to chat and she stands a little apart, on purpose, and says so flatly; no {0}
ambient | 1 | between fights: she rates the last fight out of ten, flatly, and it's a low number; no {0}
ambient | 1 | between fights: someone is cheering a small kill and she is unimpressed; no {0}
ambient | 1 | between fights: she stands right next to the campfire, because she is always cold; no {0}
ambient | 1 | between fights: one short dark thought about how every fight ends the same way, then she stops; no {0}
ambient | 1 | between fights: her pet is standing still, staring at nothing, and she says it's the best listener she knows; no {0}
reply greeting | 1 | the player says hello; she says hello back with no enthusiasm at all, and her pet looks at them
reply grouping | 1 | the player asks her to group; she says no, firmly, then asks where they're going anyway
reply abuse | 1 | the player is rude; she isn't hurt, she just rates the insult, low
reply joke | 1 | the player tells a joke; she says it was almost funny, and that's the best review she gives
reply compliment | 1 | the player compliments her; she doesn't know what to do with it and says her pet is the good-looking one
reply help | 1 | the player asks for help; she sighs, says fine, and sends her pet first
reply farewell | 1 | the player leaves; she says good, quietly, and then adds that they can come back, she supposes
reply thank | 1 | the player thanks her; she says don't, it makes it a whole thing
reply smalltalk | 1 | the player makes small talk; she answers in as few words as possible and seems fine with the silence after
reply trading | 1 | the player wants to trade; she says she'll look, and that she's not impressed yet
reply travel | 1 | the player wants to travel together; she says she'd rather go alone, then says she'll follow, at a distance
reply dungeon | 1 | the player wants to go into a dungeon; she says dark, damp and quiet is the only good thing about groups
reply rvr | 1 | the player wants to fight the other realms; she says crowds of people shouting, her least favourite thing, fine
reply banter | 1 | the player calls her gloomy; she agrees, flatly, and says gloomy is just honest with the lights off
reply banter | 1 | the player asks what her plan is; she hates that question and changes the subject
opener xp | 1 | she says levelling alone is slower, and she likes it that way; no {0}
opener gear | 1 | she says her armour doesn't matter, her pet takes the hits anyway; no {0}
opener zones | 1 | she says {zone} was too crowded, she saw three people; use {zone} exactly; no {0}
opener dungeons | 1 | she says dungeons are the only places quiet enough to think; no {0}
opener fun | 1 | she says her idea of fun is a long walk with nobody talking; no {0}
opener gripes | 1 | she says someone gave a speech before a fight today and she rated it; no {0}
opener questions | 1 | she asks, flatly, why people need groups so badly, and doesn't wait for an answer; no {0}
opener classes | 1 | she says other {class} players chase groups and she doesn't see why; use {class} exactly; no {0}
ding | 1 | she levelled up; she says it, flatly, and that her pet seems slightly more impressed than she is
grats | 1 | someone in her group levelled up; she says grats with no excitement, then admits it was earned
own_death | 1 | she died; she says it's peaceful down here and nobody is talking
own_death | 1 | she died; one short dark thought about endings, then a dry joke
other_death | 1 | {0} fell; she says she saw it coming and narrated it in her head, but she stays and waits
other_death | 1 | {0} fell; she sends her pet to stand over them, and says it's not because she cares
drop | 1 | she found {item}; she rates it out loud, flatly, a middling score; use {item} exactly
drop | 1 | she found {item}; she says her pet can carry it, it never complains; use {item} exactly
cheeky_reply banter | 1 | the player says something that sounds wrong; she repeats it back flatly and lets the silence do the work
cheeky_reply grouping | 1 | the player asks to group and it sounds wrong; she gives it a dry look and a low rating
reply farewell | 1 | the player leaves after a long night of grouping; she admits, flatly and briefly, that it wasn't terrible and she'd do it again | long
