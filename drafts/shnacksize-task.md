# Pass 3 for Shnacksize (qwen2.5:14b): 40 rows, one line each, aiming to keep about 20 and reach
# about 100 own lines (77 now). Pass 2 kept 21 of 40 plus 3 night lines (held aside in
# drafts/shnacksize-night.md). Dad jokes need a real pun in the answer (a word with two meanings);
# a plain "why did X cross the road" with no pun is wrong. Night lines: the dark makes her spooked
# and jumpy despite her fun nature; they never use {0}. Openers talk to nobody in particular, so
# they never use {0}. No realm, race, class, town or dungeon names: never write a class name such
# as Warrior; use {class} instead. Swissland, Glopeldink and the cows only in the rows that ask.
# Used up: ducking behind the tank, testing the ground, Operation Sparkle, "somebody talk",
# travel size, six "go away"s, cheese too loud, mobs not laughing, "are we there yet", training
# dummy, louder plan, nearly as flashy, somebody clap, tiny legend, useless shiny loot, stitched
# flag, adorable, crowd goes wild, ten out of eleven, Glopeldink-hater, cow society, spice in the
# grind, charmingly tiny, capital of Swissland, insulting the ogre, Captain/Admiral/Queen, sitting
# still, warm-up act, party of one, looks better on me, shouting at a storm, echoes, paid to leave,
# Swissland staircase, Sparkles and Dainty, mobs' feelings, crickets, parade, "definitely the
# plan", grand entrance/exit, stealing my job, book of grumpy people, blunt end of the sword,
# waving until out of arms, joke as payment, seal of approval, behind a big shield, juggling
# cows, on fire today, bravest behind a wall, Glopeldinkinator, keep from the bottom of the hill,
# jumped at my shadow, the dark is scared of me. Dad jokes used: skeleton bass, horse stable,
# bard notes, cow crossing the road, cow legen-dairy.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
opener fun | 1 | a dad joke with a real pun about a dragon's hoard or treasure; setup question and punchline; no {0}
opener fun | 1 | a dad joke with a real pun about armour or a shield; setup question and punchline; no {0}
opener fun | 1 | a dad joke with a real pun about a wizard or a spell; setup question and punchline; no {0}
opener fun | 1 | a dad joke with a real pun about a bear; setup question and punchline; no {0}
opener fun | 1 | a dad joke with a real pun about bread or baking; setup question and punchline; no {0}
opener fun | 1 | a dad joke about taking an arrow to the knee, with a pun on knee or arrow; only those few words as a wink, never the full famous line about being an adventurer; no {0}
opener fun | 1 | a joke about taking an arrow to the knee, told as her own story: it was the plan all along; only those few words as a wink, never the full famous line; no {0}
ambient | 1 | at night: an owl hooted and she hid behind the nearest person, then says she was checking their armour; no {0}
ambient | 1 | at night: she whistles a happy tune in the dark, badly, to prove she's brave; no {0}
ambient | 1 | at night: she counts the stars out loud so she doesn't have to look at the shadows; no {0}
ambient | 1 | at night: a twig snapped and she drew both weapons at a bush; no {0}
ambient | 1 | at night: she says she isn't sleeping with the light out, she is resting with her eyes open; no {0}
ambient | 1 | at night: she asks if anyone else heard that, and nobody heard anything; no {0}
ambient | 1 | between fights: she wrote a list of her best jokes and lost it, which means the list was a joke too; no {0}
reply rvr | 1 | the player talks about the frontier; Shnacksize offers to be the distraction, mostly by talking
reply rvr | 1 | the player talks about the frontier; Shnacksize wants a flag of her own to wave from the keep
reply dungeon | 1 | the player wants to go into a dungeon; Shnacksize asks who goes first, and says not her, she's travel size
reply dungeon | 1 | the player wants to go into a dungeon; Shnacksize brings jokes in case the monsters get bored
reply quote | 1 | the player says a famous line; Shnacksize claims it's an old Swissland saying and gets it wrong
reply thank | 1 | the player thanks her; Shnacksize asks them to say it again, louder, for the people at the back
reply travel | 1 | the player talks about a long trip; Shnacksize packs three jokes and no food
reply grinding | 1 | the player talks about grinding; Shnacksize gives every mob a name before it dies
reply question | 1 | the player asks a question; Shnacksize answers a different question, very confidently
reply smalltalk | 1 | the player chats; Shnacksize says that reminds her of a story, then forgets the story halfway
reply agree | 1 | the player agrees; Shnacksize asks them to write it down so she can prove it later
reply grumble | 1 | the player complains; Shnacksize says complaining is fine, but it needs more drama
reply compliment | 1 | the player compliments her; Shnacksize says they have excellent taste
reply grouping | 1 | the player wants to group; Shnacksize says yes, but she's in charge of morale and snacks
fallback | 1 | Shnacksize didn't catch it; she was busy arguing with a squirrel and losing
opener classes | 1 | a {class} walked past looking very serious, and she waved anyway; use {class} exactly, never a class name; no {0}
opener classes | 1 | she'd like to try being a {class} for a day, mostly for the outfit; use {class} exactly, never a class name; no {0}
opener zones | 1 | everything in {zone} looks bigger than her, which is everything; use {zone} exactly; no {0}
opener dungeons | 1 | dungeons are dark, and she is fine with that, totally, completely fine; no {0}
opener gear | 1 | her new {item} is shinier than her old one, so it must be better; use {item} exactly; no {0}
opener trading | 1 | a merchant asked her to stop telling jokes so he could work; no {0}
opener gripes | 1 | someone said she talks too much, and she had a lot to say about that; no {0}
grats | 1 | someone levelled; Shnacksize says they're nearly as impressive as her now
own_death | 1 | Shnacksize died; she says that was a tactical lie-down and the plan is still on
reply dungeon | 1 | the player wants to go into a dungeon; Shnacksize says she's in, then asks if there are bats | long
ambient | 1 | at night: she admits, quietly, that the dark scares her, and asks someone to stay close till morning; no {0} | long
