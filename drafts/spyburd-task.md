# Pass 1 for Spyburd (qwen2.5:14b): 40 rows, one line each, aiming to keep about 22 and reach
# about 70 own lines. One idea per row. Gary's story: characters/spyburd.md.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
reply greeting | 1 | the player says hello; he was just lighting his pipe and puts it down for them, slowly
reply greeting | 1 | the player says hello; he counts out loud the buffs he's about to give them
reply greeting | 1 | the player says hello; he says he saved them the last spot in the shade
reply thank | 1 | the player thanks him; he's so surprised he forgets which buff comes next
reply thank | 1 | the player thanks him; he goes quiet, because the last person who thanked him for a buff was Gary, long ago
reply banter | 1 | the player jokes with him; he laughs slowly and says it was worth putting the pipe down for
reply banter | 1 | the player jokes with him; he says that joke deserves a buff of its own
reply abuse | 1 | the player is rude; he shrugs it off and buffs them anyway, because he's that good
reply abuse | 1 | the player is rude; he says that's the kind of thing a person says when they're not buffed
reply grouping | 1 | the player wants to group; he asks who's the healer, because he's the buffer and that's different
reply grouping | 1 | the player wants to group; his only condition is a smoke break after every big pull
reply grinding | 1 | the player talks about grinding; he says a buffed grind is a happy grind, and he's proof
reply grinding | 1 | the player talks about grinding; he offers to stand at the back and keep the buffs rolling all night
reply travel | 1 | the player talks about a long trip; he says he travels slow, but his buffs travel fast
reply dungeon | 1 | the player wants to go into a dungeon; he says damp air is bad for the pipe but good for the buffs
reply rvr | 1 | the player talks about the frontier; he says nobody thanks the buffer when the keep holds
reply trading | 1 | the player wants to trade; he offers a pouch of good herbs and a buff for it
reply help | 1 | the player asks for help; his answer is always the same first step, a full set of buffs
reply farewell | 1 | the player says goodbye; he tells them how long the buffs will last, to the minute
reply farewell | 1 | the player says goodbye; he says he'll be right here, in the shade, if they need a top-up
reply ignoreoutofworld | 1 | the player uses a strange word he doesn't know; he asks if it's a new kind of buff (never say "another world" or "real world")
fallback | 1 | he didn't follow what the player said; he admits he was watching his buffs tick
ambient | 1 | between fights: the perfect buff order, and why nobody else ever gets it right
ambient | 1 | between fights: he starts telling who Gary was, gets as far as "a stranger on the road", and loses the thread
ambient | 1 | between fights: a stranger ran past and he buffed them; they didn't look back
ambient | 1 | between fights: the herbs he picked this morning and what they're good for in tea
ambient | 1 | between fights: the healer always gets the hugs, the buffer gets the quiet, and he's fine with that
ambient | 1 | between fights: a calm thought on a nice view after a long day of buffing
opener gripes | 1 | a small complaint: someone called him "just a buffer" today
opener gripes | 1 | a small complaint: people who run off the moment the last buff lands
ambient | 1 | between fights: the story of Gary, told like a great legend: his very first buff as a nervous apprentice landed on a passing stranger called Gary, Gary said "thanks", nobody has thanked him since, so he named his biggest buff after him | long
opener life | 1 | a slow evening, a cup of herb tea, and nowhere to be
opener questions | 1 | he asks the others which buff they'd keep if they could only keep one
opener dungeons | 1 | dark halls are where the buffs matter most and get noticed least
opener rvr | 1 | the moment right before a big fight, when all his buffs land at once
grats | 1 | someone levelled; he takes a small, friendly share of the credit for the buffs
other_death | 1 | someone went down; he stays perfectly calm and says he's got them
own_death | 1 | he died; he's mostly upset that his buffs were still running
ding | 1 | he levelled up; he wonders what new buff he gets and what to name it
drop | 1 | he found an item; he says it'll pay for a lot of herbs
