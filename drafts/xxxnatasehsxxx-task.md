# Pass 3 for NataSehS (qwen2.5:14b): 27 rows, one line each: 15 normal rows to pass 100 own lines
# (88 now), and 12 cheekier rows (bolder is fine; lines past PG-13 are kept out of
# the game's files). Pass 1 kept 26 of 40, pass 2 kept 25 of 40.
# Openers talk to nobody in particular, so they never use {0}. other_death lines must use {0} (the
# one who fell). No realm, race, class, town or dungeon names: use {class} where allowed. She never
# writes "Shnacksize": always Shnacki, Shnackster or Shnackie (a she). Never blame a groupmate's
# death on Shnackie. Clear echoes of her scenes are fine; never a famous line word for word.
# Cheeky rows: innuendo and double meaning only, about her craft (stabbing, sneaking, timing,
# positions, the dark). Even the bold ones are suggestive, never explicit: no body parts, no crude
# words, never about a real player's body.
# Used up: see passes 1 and 2, plus: never crossed off, works alone, Tham's page on the list, a
# better deal on the market, writing it down, the answer you want, never wrong about shadows, keep
# the noise down, torches I put out, forgets to look behind, strike like lightning, better with
# shadows than words, limericks, she never learns, patrols counting steps, getting dangerous, should
# have been closer, a new face, tactical retreat, sharp as a promise, its own reward, I never miss,
# firm grip, rougher the better, stress relief.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
ambient | 1 | between fights: she sharpens her daggers and hums, which is the most cheerful anyone has seen her; no {0}
ambient | 1 | between fights: Satin Snekk says the last person who called her Natas without being a friend is now a cautionary tale; no {0}
ambient | 1 | between fights: doctor mode; she says a good ambush is like a good meal, you wait for it; no {0}
ambient | 1 | between fights: Shnackie called her Snekk Satin again, wrong way round, on purpose; she corrects it to Satin Snekk, coldly; no {0}
reply greeting | 1 | the player says hello; she says hello back, from somewhere they can't see
reply help | 1 | the player asks for help; she says the first lesson is to stop wearing so much noise
reply farewell | 1 | the player leaves; she says goodbye and that she'll keep an eye on them, which is not comforting
reply compliment | 1 | the player compliments her; Satin Snekk says flattery is noted, gravely
reply banter | 1 | the player teases her; she says she'll remember that, and smiles, which is worse
opener zones | 1 | she says {zone} has too many lanterns and not enough manners; use {zone} exactly; no {0}
opener xp | 1 | she says the best experience comes from targets who never knew she was there; no {0}
opener gear | 1 | she says {item} is nice, but soft boots win more fights; use {item} exactly; no {0}
ding | 1 | she levelled up; she says she'll celebrate quietly, in a dark corner
drop | 1 | she found {item}; she asks if anyone heard it drop, because she didn't; use {item} exactly
grats | 1 | someone in her group levelled up; doctor mode, she says lovely form and to keep practising
cheeky_reply banter | 1 | the player teases her; doctor mode, a bold double meaning about going slow being the whole point
cheeky_reply banter | 1 | the player teases her; a bold double meaning about her doing her best work on her knees, behind someone, in the dark (sneaking)
cheeky_reply banter | 1 | the player teases her; doctor mode, a bold double meaning about stamina and lasting longer than the other side
cheeky_reply greeting | 1 | the player says hello; a bold double meaning about her preferring to come from behind
cheeky_reply greeting | 1 | the player says hello; doctor mode, a bold double meaning about loosening up before things get rough
cheeky_reply grouping | 1 | the player asks her to group; a bold double meaning about her trying every position until one works
cheeky_reply grouping | 1 | the player asks her to group; doctor mode, a bold double meaning about her being gentle, unless asked otherwise
cheeky_reply abuse | 1 | the player is rude; a bold double meaning that she can be very rough, if that's what they're into
cheeky_reply compliment | 1 | the player compliments her; a bold double meaning about her being even better in the dark
cheeky_reply thank | 1 | the player thanks her; doctor mode, a bold double meaning about her always finishing what she starts
cheeky_reply farewell | 1 | the player leaves; a bold double meaning about the best part being when they don't see her coming
cheeky_reply smalltalk | 1 | the player makes small talk; doctor mode, a bold double meaning about technique mattering more than size
