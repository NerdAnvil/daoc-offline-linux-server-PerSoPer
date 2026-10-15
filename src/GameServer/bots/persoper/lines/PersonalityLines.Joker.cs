using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class PersonalityLines
{
    // --- Joker: puns, teasing; Cheeky lines only when tone-down is off ---
    private static TypeLines Joker() => new()
    {
        Openers = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "My XP bar and I have a lot in common. We both fill up slowly.",
                "I'd tell you my XP per hour, but it's a little embarrassing. Mostly the hour part.",
                "Leveling is easy. You just need a lot of patience and very few friends.",
                "Almost {level}! Or as I call it, nearly an adult.",
                "I asked {monster} for XP. It gave me a death instead. Generous.",
                "Grinding is like baking. Too slow and you go stale.",
                "They say XP is earned. I prefer borrowing it from my group.",
                "My bubbles pop slower than a sleepy frog.",
                "I'm on a strict leveling diet. One mob at a time.",
                "Why level fast? The mobs aren't going anywhere. Mostly because they're dead.",
            ],
            [T.Gear] =
            [
                "Got new boots. They're a step up.",
                "My shield and I broke up. It said I never protected it.",
                "This armour is so heavy, even my excuses can't carry it.",
                "Found {item}. Fashion first, survival second.",
                "My helmet is so shiny, mobs use it as a mirror.",
                "I bought a cursed ring. Now everything I say is a pun. Wait, that was before.",
                "My sword's a bit dull. Like my last joke.",
                "Armour tip: the gaps are for ventilation.",
                "I wear plaid under my chainmail. For morale.",
                "My gloves are leather, my jokes are cheesy. Balanced outfit.",
            ],
            [T.Zones] =
            [
                "{zone} is lovely this time of year. Every time of year, actually.",
                "This zone is so quiet I can hear my jokes not landing.",
                "Got lost in {zone}. Found myself. Wasn't impressed.",
                "These hills have their ups and downs.",
                "The river here is a bit shallow. Like my plot armour.",
                "This forest is a bit of a stump-er.",
                "Somebody should build a tavern here. I'd raise the bar.",
                "Too many spiders in this zone. It's a web of lies.",
                "The swamp here smells like my last group's strategy.",
                "This zone has great views. Mostly of my corpse.",
            ],
            [T.Classes] =
            [
                "Healers keep me alive. Mostly so I can tell more jokes. Their loss.",
                "Tanks are just walls with opinions.",
                "A {class} told me a joke once. It was a support line.",
                "Stealthers are great. You never see them coming. Or at all, really.",
                "Pet classes never eat alone. Lucky.",
                "Casters are sparkly. Like me, but with mana.",
                "I picked my class for the jokes. Nobody told me there'd be fighting.",
                "Hybrids are jacks of all trades. Masters of puns, in my case.",
                "Archers always get to the point.",
                "Bards are great. Someone else does the singing, I do the talking.",
            ],
            [T.Dungeons] =
            [
                "Dungeons are just basements with ambition.",
                "Went into Darkness Falls. It lived up to the name.",
                "Dungeon tip: the floor is cold, so don't die on it.",
                "This dungeon has great acoustics for screaming.",
                "{monster} down there told me to get lost. So I did. Took an hour.",
                "Skeletons never fight each other. They don't have the guts.",
                "Dungeon bosses are just regular mobs with a promotion.",
                "I went into a crypt for some peace. Grave mistake.",
                "Dungeons are great for team building. Mostly the building of corpses.",
                "Dungeon loot is like jokes. You keep the good ones and sell the rest.",
            ],
            [T.RvR] =
            [
                "Went to the frontier. The frontier won.",
                "Keep walls are tall. My ladder jokes fall flat.",
                "I tried to siege a keep with puns. The door groaned.",
                "Battlegrounds are like family dinners. Loud and someone always dies.",
                "Relics are just old rocks that everyone wants. Like me, someday.",
                "Charged into RvR. The charge was refunded.",
                "Guards at keeps never laugh at my jokes. Tough crowd, literally.",
                "Ram the door, they said. I misheard and brought a sheep.",
                "I don't fear the other realms. I fear their healers.",
                "RvR tip: run in a zig-zag. It confuses enemies and friends.",
            ],
            [T.Trading] =
            [
                "Selling jokes. First one's free. Second one's also free. Nobody's buying.",
                "I traded my horse for a potion. Now I'm a bit unstable.",
                "Merchants love me. I'm always good for a laugh and two copper.",
                "Got {item} for sale. Comes with a free pun.",
                "The Realm Exchange is like a joke. Timing is everything.",
                "I sold my map. Now I don't know where I'm going, but I'm rich.",
                "Coins in my pouch jingle like a terrible song.",
                "Bought bread. It was a crumby deal.",
                "Haggling with a merchant is the only fight I win.",
                "I'd lend you money, but then you'd have to laugh at my jokes.",
            ],
            [T.Realm] =
            [
                "The trainer said I need more practice. I said, at jokes? He left.",
                "Town guards never smile. I've tried for weeks.",
                "Stablemasters have the most stable job in the realm.",
                "The bank is so secure, even my jokes can't get in.",
                "Read the lore today. Turns out the realm was founded on puns. I made that up.",
                "Every town has a merchant who sighs when I walk in.",
                "The healer in town says I'm a pain. A pun-in-the-neck.",
                "Our capital is great. I've been lost in every part of it.",
                "Talked to a quest NPC. He had a problem. Now I do too.",
                "The tavern keeper cut me off. Not from drinks. From jokes.",
            ],
            [T.Gripes] =
            [
                "Nobody laughed at my last joke. I'm filing a complaint with myself.",
                "Lagged mid-pun. Ruined the timing.",
                "A {monster} interrupted my best joke. It paid for it.",
                "My group left me. I hope it wasn't the jokes. It was the jokes.",
                "Died while telling a joke. Never got to the punchline.",
                "Bags full of junk. Like my joke book.",
                "Ran out of mana and puns at the same time. Worst day.",
                "Someone called my joke old. It's vintage.",
                "The respawn here is faster than my wit. Rude.",
                "Fell off a cliff. Bad call, gravity. Bad call.",
            ],
            [T.Fun] =
            [
                "What's a kobold's favourite game? Hide and seek. They're great at hiding behind short things.",
                "Why don't trolls tell secrets? Too many rocks in their heads to keep them.",
                "I asked a ghost to join our group. It said it would pass through later.",
                "My horse tells better jokes than me. Mostly neigh-sayers though.",
                "Why did the healer cross the road? Someone on the other side needed a heal.",
                "I tried to scare a skeleton. It was too bony to care.",
                "What do you call a wizard with no mana? A very angry librarian.",
                "Rate my last joke out of ten. Wait, don't.",
                "I have a joke about keeps. It's a bit of a siege.",
                "My spellbook is just jokes. That's why nothing happens.",
            ],
            [T.Life] =
            [
                "Had soup for lunch. It was souper.",
                "It's raining. Even the clouds are crying at my jokes.",
                "Sleep is just a long AFK.",
                "I went for a walk. Nothing attacked me. Boring.",
                "Coffee and puns. My two food groups.",
                "My cat judges my jokes. Harshly.",
                "Snowing out. The perfect time to stay in and be cool.",
                "Travel tip: always pack a joke. They weigh nothing.",
                "I tried to bake a pie. It's now a crust-acean.",
                "Need a nap. Wake me when someone laughs.",
            ],
            [T.Questions] =
            [
                "Who wants to hear a joke? Too late, it's coming.",
                "Anyone know a good place to camp? Not the tent kind. Okay, also the tent kind.",
                "What's everyone's favourite pun? Mine is all of them.",
                "Is {zone} safe? Asking for my dignity.",
                "Who here has the worst gear? I want to feel better.",
                "Anyone know what {monster} drops? Besides my morale.",
                "Can anyone teach me a new joke? I've used all mine. Twice.",
                "Who's grouping tonight? I bring snacks and suffering.",
                "Does anyone actually read the merchant's sign? Asking for a friend.",
                "What's the funniest way you've died?",
            ],
        },
        TopicReplies = new Dictionary<T, string[]>
        {
            [T.Xp] = ["XP talk, {0}? That's experience-ive.", "{0}, I level by jokes. Very slowly.", "Nice pace, {0}. I'll catch up. Probably next year.", "Don't count your bubbles before they pop, {0}."],
            [T.Gear] = ["Shiny, {0}! Does it come with a mirror?", "{0}, that gear really suits you. Not literally.", "Nice, {0}. It's armour-zing.", "Looking sharp, {0}. Like a sword."],
            [T.Zones] = ["I got lost there, {0}. Twice. On purpose.", "{0}, that zone has great views. Of the ground, when you die.", "Sounds lovely, {0}. Bring a map. And a joke.", "Been there, {0}. The mobs didn't laugh."],
            [T.Classes] = ["{0}, I picked my class for the hat.", "That class is great, {0}. Almost as great as my jokes.", "Ah, {0}, a classy topic.", "{0}, every class is fine. Mine is just funnier."],
            [T.Dungeons] = ["Dungeons, {0}? I'll bring the light-hearted jokes.", "Count me in, {0}. I'm dying to go.", "{0}, I'll come. Someone has to scream first.", "A dungeon run? Sounds grave, {0}."],
            [T.RvR] = ["The frontier, {0}? I'll bring jokes and run away.", "{0}, I'll guard the rear. Of the group. Behind it. Far behind.", "Keep fights? I'll storm in with puns, {0}.", "RvR, {0}? Realm vs. Ridiculous, in my case."],
            [T.Trading] = ["I'll trade you a joke for that, {0}.", "{0}, how much? I'll pay in laughs.", "That's a steal, {0}. Wait, did you steal it?", "Price check, {0}: priceless. Like my humour."],
            [T.Realm] = ["{0}, that NPC still owes me a laugh.", "Ah, the lore, {0}. Mostly made up, like my stories.", "I love that town, {0}. The guards hate me there.", "{0}, the stablemaster's a friend. Very stable guy."],
            [T.Gripes] = ["Cheer up, {0}. Want a joke? Too bad, here's one anyway.", "That's rough, {0}. Rougher than my armour.", "{0}, look on the bright side. There isn't one, but look anyway.", "I'd complain too, {0}, but I'm too busy being charming."],
            [T.Fun] = ["Ha! {0}, you're almost as funny as me.", "Good one, {0}. I'm stealing it.", "{0}, that's going in my act.", "Okay {0}, that was funnier than mine. Don't tell anyone."],
            [T.Life] = ["{0}, that sounds like a good time. Pun not intended.", "Same, {0}. Life's a joke and I'm the punchline.", "Nice, {0}. Save me a snack.", "{0}, sounds relaxing. Mine involves screaming at mobs."],
            [T.Questions] = ["Good question, {0}. Bad answer coming.", "{0}, I'd answer, but I'd rather joke.", "Ask me later, {0}. I'll still not know.", "Hmm, {0}. The answer is probably a pun."],
        },
        Ding =
        [
            "Ding {level}! I'm a whole level of funny now.",
            "{level}! Another year older, none the wiser.",
            "Level {level}. Hold your applause. Actually, don't.",
            "Ding! {level}. My jokes got an upgrade too.",
            "{level}! Somebody throw me a party. A small one.",
            "Reached {level}. Still can't reach the top shelf.",
        ],
        Grats =
        [
            "Grats {0}! Now you're legally funny.",
            "Ding-a-ling, {0}! Grats!",
            "Grats {0}! Level up, jokes up.",
            "Nice one {0}! Have a pun on me.",
            "Gz {0}! Getting old, aren't we?",
            "Grats, {0}! I'd hug you, but armour.",
        ],
        OwnDeath =
        [
            "Well, that was a dead end.",
            "I died laughing. Mostly dying.",
            "Floor tastes like regret.",
            "That mob had no sense of humour.",
            "Playing dead. Very convincingly. Too convincingly.",
            "Note to self: don't heckle things with claws.",
        ],
        OtherDeath =
        [
            "{0} took the express route to the bind stone.",
            "Oops, {0}. That's a grave situation.",
            "{0}, nice nap. Very convincing.",
            "Rest in pieces, {0}. Kidding, get up.",
            "{0}, the floor was not your friend.",
            "{0} died. I'll write a joke about it later.",
        ],
        Drop =
        [
            "{item}! Loot-iful.",
            "Ooh, {item}. The mob had taste.",
            "Got {item}. My bag says thank you.",
            "{item}! Finally, something better than my jokes.",
            "A drop! Shame it's not a punchline.",
            "{item}. Fancy. I'll wear it ironically.",
        ],
        Cheeky =
        [
            "Is it hot in here, or is that just my fire resist failing?",
            "I'm not saying I'm the best-looking bot here. I'm just not denying it.",
            "Healers, if you're free tonight, my HP bar is always open.",
            "Nice armour, everyone. Almost as nice as my smile.",
            "I'd flirt with the guards, but they never break character.",
            "My charm stat is capped. My charm items are not.",
            "Somebody pinch me. Gently. I bruise like fruit.",
            "Tanks, you can hold my aggro anytime.",
            "Let's be honest, half of you only group with me for my looks.",
            "New cloak? Bold choice. I respect bold choices. And cloaks.",
            "I'd buy you a drink, but my coin pouch is shy.",
            "Bards, sing me something nice. Or just stand there. That works too.",
        ],
    };
}
