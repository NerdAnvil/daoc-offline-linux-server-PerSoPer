# Model test B2 (2026-10-30): the same 100 lines as A and B, with setup B's rules plus longer and
# tagged example lines in the style file and a situation named in each row. Raw answers are
# compared; nothing goes into the game.
# section | lines | what the player just did, and anything extra for this section [| long]
reply greeting | 10 | the player says hello; she reacts to how the player looks today or to what just happened to them (some just died, some are badly hurt, some just found loot or went up a level, some it's late at night)
reply help | 10 | the player asks for help; she helps, but makes a big show of it (some mid-fight, some with the player nearly dead, some calm)
reply thank | 8 | the player thanks her; she accepts it like an award at a ceremony (some from a player she likes a lot, some from one she doesn't)
reply thank | 5 | the player thanks her after a close call where they nearly died | long
reply grumble | 10 | the player complains; she sympathises badly, or tops their complaint with a worse one of her own (some after the player died several times today, some in the morning)
reply banter | 10 | the player teases her; she fires right back (some from a player she likes, some from one she finds cool, some mid-fight)
reply grouping | 9 | the player talks about grouping; she sizes up the group like a sports coach (some for a small group, some for a big one of five or more)
reply dungeon | 9 | the player talks about a dungeon; she has strong opinions about dungeon decor (some said at night, one she only says the first time)
reply farewell | 9 | the player says goodbye; she makes their leaving dramatic (some at night, some right after a fight, some from a player she likes a lot)
reply compliment | 10 | the player compliments her; she pretends to be modest and fails (some right after she saved them, some from a player she likes)
fallback | 10 | she didn't understand the player; she covers it with total confidence (some mid-fight, some while the player is dead)
