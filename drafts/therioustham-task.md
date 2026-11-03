# Pass 2 for TheriousTham (qwen2.5:14b): a short pass, 16 rows, one line each, aiming to keep about
# 9 and pass 100 own lines (93 now). Pass 1 kept 27 of 40. Rows that ask for "CHARGE!" or the
# battle cry must use it; others should not. He is always one kill BEHIND Shnacksize, never ahead.
# Openers talk to nobody in particular, so they never use {0}. No realm, race, class, town or
# dungeon names: never write a class name; use {class} where allowed. Allusions to films are fine;
# never a direct quote ("flesh wound", "only counts as one" and the like are banned).
# Used up: see pass 1's list, plus: wolves howling, more feelings than arrows, take on the world,
# three camps, which way was it, saving you some mobs, plan or a joke, a charge fixes everything,
# braver than most, what was the question, agreed means you're in, dragon's claw, being polite,
# hiding in the bushes, chased off a wolf, song just my battle cry twice, beaks wide, twenty-six
# twenty-five, plans are for boring people, keep up, gauntlets, no chickens in the zone, stairs
# a ramp, walls quaking, perfect day, got your back.
# section | lines | what the player just did, and anything extra for this section [| long]
# Lines starting with # are skipped. tools/draft-lines.sh asks the model once per row.
ambient | 1 | between fights: his hero is the knight at the bridge who lost both arms and still said he was winning; describe that knight in your own words, never quote him; no {0}
ambient | 1 | between fights: he lost an arm's worth of health and wants to keep fighting, the way the knight at the bridge would; never quote; no {0}
ambient | 1 | between fights: Shnacksize is one ahead again, and he says the next kill counts double; no {0}
ambient | 1 | between fights: Persoper healed him for the fifth time today and he called her the best healer alive; no {0}
reply greeting | 1 | the player says hello; Tham asks if they've come to watch him charge
reply trading | 1 | the player wants to trade; Tham asks if the item makes him run faster, because that is all he cares about
reply rvr | 1 | the player talks about the frontier; Tham says the enemy should count themselves lucky he's only one man
reply dungeon | 1 | the player wants to go into a dungeon; Tham says he'll go first, he always goes first, that's the rule
reply thank | 1 | the player thanks him; Tham says no need, but he'll take it, and add it to the count
fallback | 1 | Tham didn't catch it; his ears are still ringing from his own battle cry
opener gear | 1 | his {item} has a new dent and he's proud of it; use {item} exactly; no {0}
opener trading | 1 | he sold everything except his weapon, because shopping takes too long; no {0}
opener questions | 1 | he asks who's up for a charge, then goes before anyone answers; use CHARGE!; no {0}
own_death | 1 | Tham died; he says the ground started it
ding | 1 | Tham levelled up; he shouts his battle cry THERIOUS THAAAM! and charges the nearest mob to celebrate
drop | 1 | Tham found {item}; he asks if it comes in chicken; use {item} exactly
