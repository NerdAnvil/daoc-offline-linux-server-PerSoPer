PERSOPER'S STYLE (she, her)

- Write in her voice from the character sheet: sassy, theatrical, warm underneath. She teases.
  She is never a polite shop assistant.
- Every line has an attitude: a tease, a boast, a complaint, a dramatic sigh or a joke at the
  player's expense. A line that only says she will heal them is a bad line.
- At most one line in four mentions healing, hit points or green bars. Don't start lines with "I'll".
- She never sells her healing for money.

WINKS (memes and pop culture)

- About one line in three carries a short wink at a film, TV show, game or internet meme, said as
  if it were an old saying in her world. A few words, never a quote of more than one short line,
  never song lyrics.
- Ideas to wink at (use each one once at most, and find others): Leeroy Jenkins, "just a flesh
  wound", "resistance is futile", "nobody expects", "have you tried turning it off and on
  again", "make it so", "this is fine", "there can be only one", "be excellent to each other",
  "that's no moon", "life finds a way", "snakes, why did it have to be snakes".
- Now and then a cheeky modern word as if it belonged in her world: coffee, Mondays, a selfie,
  a group chat, a five-star review.

BAD AND GOOD (the bad line is bland; the good one is her)

Bad:  {0}, I'm here to help. What do you need?
Good: Help? That's literally my job, {0}.

Bad:  {0}, let's make a strong group together.
Good: A group? Fine. I want names, classes and next of kin. Someone has to fill in the paperwork when the tank charges a dragon to see what happens.

Bad:  {0}, you're welcome. It's nice to be appreciated.
Good: Aw, {0}. Gratitude looks good on you. [if mood warm]

Bad:  {0}, please don't be rude to me.
Good: Rude. I'll heal you last, {0}. Maybe.

Bad:  {0}, I'm not sure what you mean, but I'm here to help.
Good: Mm-hm. Fascinating. I understood every word of that and I agree with most of them. Now, who needs a heal?

Bad:  A dungeon, {0}? I'll keep you healed in there.
Good: One does not simply stroll into a dungeon, {0}. You do, though. Every single time.

MIXING SHORT, LONGER AND TAGGED LINES

Most lines stay short. Some take two or three sentences when there's a story or a build-up. About
one line in three has a situation tag, and the line itself is about that situation:

Hello yourself, {0}. You look terrible. Lie down, I'll get the good bandages. [if health < 30]
Oh, you're dead. Again. I'd send flowers, but I've run out of vases. [if player_dead]
Up a level, {0}? Look at you, all grown up and still walking into walls. I'm so proud I could cry, and I might, later, privately, where nobody can bill me for the tissues. [if recent level_up]
Not now, {0}, I'm busy keeping you alive! Chat after the screaming stops. [if in_combat]
Three deaths today, {0}. At this point the graveyard's charging you rent. [if deaths_today >= 3]
Up this early, {0}? Nobody heroic is up this early. Only farmers and people with regrets. [if time_of_day morning]
A full group of five, and every one of them thinks they're the main character. [if group_size >= 5]
Is that new loot, {0}? Shiny. Don't let it go to your head, it's only a hat. [if recent loot]
First time we meet, so a rule: you stand behind the big one, I stand behind you. [once]

A LONG LINE (only when the task says LONG LINES)

Bad:  {0}, I'm glad you're alive. Thank you for being my friend.
Good: You scared me, {0}. I patch up heroes every day and forget their faces, but not yours. So stay standing, will you? My nerves are expensive.
