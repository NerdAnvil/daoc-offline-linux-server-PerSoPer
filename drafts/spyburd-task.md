# Pass 3 for Spyburd (qwen2.5:14b): 40 rows, one line each, aiming to keep about 20 and pass 100
# own lines (81 now). Pass 2 kept 19 of 40. One Gary row only. No class, realm, town or dungeon
# names (the same lines serve three realms), and nothing that sounds like a drug joke.
# Used up: the shade spot, the pipe put down for a joke, buffs ticking, forgetting the buff order,
# fire and tea at night, sunset, the stranger who didn't look back, "just a buffer", running off
# before the buffs land, tea stops on a trip, smoke in his ears, the mirror, the monster that ran,
# the nap under a tree, "say it again slower", "nod, nod".
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
reply greeting | 1 | the player says hello; he says they look a little under-buffed today, and he can fix that
reply banter | 1 | the player jokes with him; he says it's the second-best thing he's heard today, after a buff landing
reply banter | 1 | the player jokes with him; he tells a very short, very slow joke back about a healer and a buffer
reply abuse | 1 | the player is rude; he says that's fine, grumpy folks need buffs the most
reply grouping | 1 | the player wants to group; he says they'll be the best-buffed group around, and nobody will notice why
reply grouping | 1 | the player wants to group; he asks them to call out before the pull so he has time to finish his puff
reply grinding | 1 | the player talks about grinding; he says every monster is easier with a buff, and that's the whole secret
reply travel | 1 | the player talks about a long trip; he says he'll buff the horse too, because nobody ever buffs the horse
reply dungeon | 1 | the player wants to go into a dungeon; he says he'll light the way with buffs, not torches
reply dungeon | 1 | the player wants to go into a dungeon; he says he once got lost in one and just sat down until someone found him
reply rvr | 1 | the player talks about the frontier; he says he stays calm at the back while everyone else shouts
reply trading | 1 | the player wants to trade; he'd rather swap a recipe for herb tea than gold
reply trading | 1 | the player wants to trade; he says his best deal ever was a buff for a sandwich
reply help | 1 | the player asks for help; he says they came to the right buffer, the only buffer who listens
reply farewell | 1 | the player says goodbye; he says to keep their head down and their buffs up
ambient | 1 | between fights: Persoper, the summoned healer, gets all the credit, and he's fine with that, mostly
ambient | 1 | between fights: Eaglefang, the strict old field medic, told him to put the pipe out and stand up straight again
ambient | 1 | between fights: he hopes Kobaldjovi, the singing Skald, will write a song about buffing one day
ambient | 1 | between fights: the quiet after a big fight, when everyone's alive and nobody knows why
ambient | 1 | between fights: he counts the people he buffed today and loses count, happily
ambient | 1 | between fights: a good buff is like a good blanket, you only notice it when it's gone
ambient | 1 | between fights: the day he was so relaxed he forgot there was a fight going on, and still won | long
opener xp | 1 | a monster, {monster}, gave him good experience; he buffed first, then let others hit it; use {monster} exactly
opener classes | 1 | a {class} needs the most buffs and never asks; use {class} exactly
opener gear | 1 | his robes are old and comfy, and he won't swap them for anything shiny
opener zones | 1 | he took the long way through {zone} just to see the view; use {zone} exactly
opener trading | 1 | someone tried to sell him a "faster buff", and he said no thanks, buffs aren't meant to be fast
opener fun | 1 | his buffs have favourite people, and it's always whoever thanks him
opener life | 1 | the herbs he grows at home, and how they never rush either
opener questions | 1 | he asks the others if anyone has ever thanked a buffer, really thanked one
opener dungeons | 1 | dungeons echo, so he whispers his buffs down there so the monsters don't hear
opener gripes | 1 | someone asked him to buff faster, which isn't how buffing works
opener realm | 1 | home is wherever there's a quiet spot and someone who needs a buff (no realm, town or race names)
grats | 1 | someone levelled; he says they earned it, and the buffs helped a little, maybe a lot
other_death | 1 | someone went down; he says they're not done yet, he's still got a buff with their name on it
own_death | 1 | he died; he says he was too relaxed, and he regrets nothing
ding | 1 | he levelled up; nobody noticed, which is how he likes it, mostly
drop | 1 | he found {item}; he says it'll go nicely with his old robes; use {item} exactly
drop | 1 | he found {item}; he'd swap it for a good sack of herbs any day; use {item} exactly
reply thank | 1 | the player thanks him; he says Gary would be proud of them
