# Pass 12 for Persoper (qwen2.5:14b): a small pass, about 33 lines, to reach 503. More of the rows
# that worked in pass 11 (bossy roles, terrible advice, drama queen), a few new angles, and no
# cheeky grouping (worn out).
# section | lines | what the player just did, and anything extra for this section [| long]
# "long" asks for long heartfelt lines (20 to 35 words); a section may have several rows.
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
reply grouping | 3 | the player talks about forming a group; she hands out roles like a bossy film director, to classes she hasn't teased yet: the bard or skald, the pet class, the other healer
reply help | 3 | the player asks for help with something other than healing; she gives terrible advice with total confidence
reply abuse | 2 | the player is rude; she plays the drama queen: clutches her chest, demands an apology in writing
song_reaction | 4 | someone just sang a song to the group (usually Kobaldjovi); she scores it like a talent show judge, or asks for an encore against her better judgement
reply greeting | 3 | the player says hello; something she cooked at camp is still smoking behind her
reply rvr | 3 | the player talks about the frontier; she rates the keeps like someone house hunting
reply travel | 3 | the player talks about going somewhere; she complains about the horse ride from the stablemaster: saddle sore, a horse with an attitude
reply thank | 3 | the player thanks her; she asks to be paid in compliments, not coin
reply dungeon | 2 | the player talks about a dungeon; she counts the exits out loud like a nervous theatre-goer
cheeky_reply greeting | 3 | the player says hello; she flirts a little about their new gear (mild double meanings only; never "hands-on" or "topped up")
cheeky_reply banter | 2 | the player jokes with her; she flirts a little about dancing with them by the campfire (mild double meanings only; never "hands-on" or "topped up")
fallback | 2 | she didn't understand the player; she claims the wind stole their words, very theatrically
