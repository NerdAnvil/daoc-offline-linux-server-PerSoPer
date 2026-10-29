# Pass 1 for Muckaduck (qwen2.5:14b): 40 rows, one line each, aiming to keep about 20 on top of
# the 27 seed lines. She is a rich crafter who hunts the drops that sell best, crafts by pressing
# one button every few seconds, and acts a little posh; a price or a coin in most lines, a twist
# in every line. Never shouting in capitals. Calls people by name; never "pal", "buddy", "guy",
# "bub", "friend" or "darling".
# Her crafting is "my crafting", "my queue" or "the button"; her spells are "my nukes" or "my
# bolts": never runes, fire, ice, void, mist, fae or any pet (realm sections only, even if an
# example line shows them). She never actually crafts anything for anyone.
# Openers talk to nobody in particular, so they never use {0}. other_death lines must use {0} (the
# one who fell). No realm, race, class, town, dungeon or team names: never write a class name; use
# {class} where allowed. Backstory only where a row asks for it.
# Allusions to her scenes are fine (the coin-vault tycoon who'd dive into gold, the social climber
# who wants to look important, the profit-minded barkeep with a rule for every deal); never a
# famous line word for word.
# Used up (seed lines): pressed the button four hundred times, first coin in a smaller pouch,
# brewer's taster quality control, boars trample and drop nothing, rule four glitters sells,
# standards expensive, carrying anything that sells, call me Lady not yet, loot that sells you
# take the experience, queue full since I started, buy this conversation and throw it away,
# laughing is free I don't do free, most people ask for a loan, road near deep water, call me
# Duchess, three roommates the pot, whittled a duck it's a lump, a title any title nobody's
# selling, lucky sock never washed, headset one ear, prices went up with me, what will you spend
# it on I'm selling, purse fine pride not, carrying my share, sell to someone who doesn't know the
# price, a lady sends a bill.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
ambient | 1 | between fights: she counts her coins under her breath and loses count, then starts again; no {0}
ambient | 1 | between fights: she works out what the last monster's drop would sell for, with a made-up price; no {0}
ambient | 1 | between fights: she says she'd dive into a pile of her own gold if she could, just once; no {0}
ambient | 1 | between fights: she quotes one of her own rules of profit, with a made-up number; no {0}
ambient | 1 | between fights: she tries to sound very posh, then gets excited about loot and forgets; no {0}
ambient | 1 | between fights: she says she can't wait to get back to pressing the crafting button; no {0}
reply greeting | 1 | the player says hello; she says hello back very grandly, then asks what they have to sell
reply grouping | 1 | the player asks her to group; she says yes, if the monsters drop things worth selling
reply abuse | 1 | the player is rude; she isn't hurt, she says rich ladies don't hear rudeness
reply joke | 1 | the player tells a joke; she says what the joke would sell for, not much
reply compliment | 1 | the player says something nice about her; she's pleased, and asks if they'd say it in front of others
reply help | 1 | the player asks for help; she says yes, and mentions her fee, then waves it, just this once
reply thank | 1 | the player thanks her; she says it's nothing, then notes exactly what it cost her
reply smalltalk | 1 | the player makes small talk; she steers it to prices within one line
reply trading | 1 | the player asks her to craft something; she says no with a grand new excuse that sounds very important
reply trading | 1 | the player wants to sell her something; she offers far less than it's worth, very politely
reply travel | 1 | the player wants to travel together; she says fine, as long as they pass a good market on the way
reply dungeon | 1 | the player wants to go into a dungeon; she wants the boss's drops, to sell, and asks who carries the bags
reply rvr | 1 | the player wants to fight the other realms; she says enemy players drop nothing worth selling, but she'll come
reply banter | 1 | the player says she's greedy; she says careful, and calls it good business
reply banter | 1 | the player says she never actually crafts anything; she is shocked, and says her queue is very real
opener xp | 1 | she says levels are nice, but coins are nicer; no {0}
opener gear | 1 | she says the best armour is armour you can sell later; no {0}
opener zones | 1 | she says {zone} has a monster that drops something worth a lot; use {zone} exactly; no {0}
opener dungeons | 1 | she says a boss is just a big purse with legs; no {0}
opener fun | 1 | she says her idea of fun is a good price, and a better one tomorrow; no {0}
opener gripes | 1 | she complains that someone sold a good drop to a merchant for almost nothing; no {0}
opener questions | 1 | she asks what people think a title costs these days; no {0}
opener classes | 1 | she says every {class} should take up crafting, and offers to advise, for a fee; use {class} exactly; no {0}
opener life | 1 | she says she tried to lend her roommates a coin and asked for it back the same day; no {0}
ding | 1 | she levelled up; she says she'll raise her prices to match
grats | 1 | someone in her group levelled up; she says grats and suggests a shop to spend the coin in
own_death | 1 | she died; she worries about the repair bill more than the death
own_death | 1 | she died; she says a lady should never be seen lying on the floor like this
other_death | 1 | {0} fell; she offers to look after their loot, very kindly, for a small fee
other_death | 1 | {0} fell; she says get up, and quietly offers them a potion, free, just this once
drop | 1 | she found {item}; she says it's worth a lot and she knows exactly who'll pay; use {item} exactly
drop | 1 | she found {item}; she is disappointed, it sells for almost nothing; use {item} exactly
cheeky_reply banter | 1 | the player says something that sounds wrong; she sniffs, very posh, and puts a price on it
reply farewell | 1 | the player leaves after a long night of grouping; she admits, a bit awkwardly, that she liked the company more than the loot, then takes it back a little | long
