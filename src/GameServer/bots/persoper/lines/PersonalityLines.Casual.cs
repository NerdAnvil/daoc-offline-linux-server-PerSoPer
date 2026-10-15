using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class PersonalityLines
{
    // --- Casual: lowercase, relaxed, dies a lot, doesn't mind ---
    private static TypeLines Casual() => new()
    {
        Openers = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "i got like half a bubble today. calling that a win",
                "how do people level so fast. do they not stop to look at stuff",
                "almost {level}. or maybe not. the bar is confusing",
                "xp is nice but have you seen the clouds in this zone",
                "killed three things and a squirrel. productive day",
                "my xp bar moves like it's also on a break",
                "i think i lost more xp dying than i got today lol",
                "someone told me to camp a spot. i forgot which one",
                "leveling slowly on purpose. that's my story anyway",
                "got some xp from {monster}. didn't even mean to fight it",
            ],
            [T.Gear] =
            [
                "my armour doesn't match at all. i look like a yard sale",
                "found {item}. no idea if it's good but it's shiny",
                "still wearing my starter boots. they're comfy ok",
                "is a blue item good? it looks good",
                "i just wear whatever drops. fashion is chaos",
                "my sword has a name now. it's called sword",
                "forgot to repair again. everything's at like 40%",
                "someone gave me a cloak once. still wearing it. thanks stranger",
                "got a helmet but i hide it. i like my hair",
                "i should probably upgrade something. maybe tomorrow",
            ],
            [T.Zones] =
            [
                "{zone} is kinda pretty at night ngl",
                "i got lost in this zone for like twenty minutes",
                "is there a zone with no aggro mobs. asking for me",
                "found a nice spot by the water. just sitting here",
                "this zone has too many hills. my horse is tired",
                "i like zones with a river. very relaxing",
                "got chased across half of {zone} by something big",
                "anyone else just wander zones with no plan",
                "every time i go somewhere new i die once. it's tradition",
                "this place has nice trees. that's my whole review",
            ],
            [T.Classes] =
            [
                "picked my class because the robe looked cool",
                "healers are nice. i like being kept alive",
                "i don't really know what my buttons do but they glow",
                "a {class} saved me earlier. hero",
                "pet classes are cute. pets are cute. that's it",
                "tanks are brave. i'm more of a stand-in-the-back type",
                "might roll an alt. something with a big hat",
                "stealth classes are cool till they vanish and leave you alone",
                "casters look fun. lots of sparkly stuff",
                "i respec when i get bored. which is often",
            ],
            [T.Dungeons] =
            [
                "dungeons are so dark. i keep running into walls",
                "went into a dungeon alone. came out dead. classic",
                "darkness falls is spooky but the music's nice",
                "i'll do a dungeon if someone holds my hand",
                "got lost in a dungeon and found a nice quiet corner",
                "is there a dungeon that's not scary. like a cozy one",
                "{monster} in that dungeon scared me so much i logged",
                "dungeon loot is fun. dungeon dying less so",
                "i always fall off something in dungeons",
                "maybe we do a dungeon later? no pressure",
            ],
            [T.RvR] =
            [
                "went to the frontier once. died before i saw anyone",
                "keeps are so big. who builds these",
                "rvr is cool to watch from a safe hill",
                "i joined a keep fight and just clicked stuff. we lost i think",
                "battlegrounds are fun. i die a lot but it's fun",
                "the other realms seem nice. from far away",
                "relics sound important. i don't know where they are",
                "someone yelled charge and i ran the wrong way lol",
                "got one realm point once. framed it",
                "frontier sunsets though. worth the death",
            ],
            [T.Trading] =
            [
                "anyone want some stuff? i have bags of random stuff",
                "sold something for way too little. oh well",
                "the realm exchange is confusing. i just click buy",
                "i'll trade {item} for a nice chat",
                "i have like twelve potions and no idea what they do",
                "coins? i have some. not many. enough for bread",
                "merchant gave me a weird look. fair",
                "i keep vendor junk in case it becomes cool later",
                "buying a horse ticket was the best money i spent today",
                "if you need anything i probably have it. somewhere. in a bag",
            ],
            [T.Realm] =
            [
                "the town music is so chill. i just stand there sometimes",
                "talked to every npc in town. they're all busy",
                "the trainer keeps saying i should train. ok mom",
                "love the stablemasters. horse rides are my favourite part",
                "read some lore on a sign. didn't understand it. nice sign though",
                "the guards here look tired. same",
                "there's a cat in town. or a dog. something furry",
                "i always forget where the bank is",
                "the capital is huge. i got lost going to the bank",
                "townsfolk must think we're weird. always running",
            ],
            [T.Gripes] =
            [
                "died to a wolf again. it wasn't even angry",
                "my horse dropped me in the worst spot",
                "lag ate my potion lol",
                "everyone's so serious about xp. chill guys",
                "bags full again. how",
                "i fell off a tiny ledge and died. tiny ledge",
                "{monster} keeps aggroing me. i didn't do anything",
                "can't find my group. they left me i think",
                "my corpse is somewhere in a lake. sad",
                "forgot to bind. long walk back. ugh",
            ],
            [T.Fun] =
            [
                "just did a little dance for no reason",
                "found a nice rock. sitting on it",
                "tried to jump over a fence. did not work",
                "named my horse after a snack",
                "anyone else just emote at mobs",
                "doing a tour of all the taverns. for science",
                "made a friend today. it was a guard. he didn't talk",
                "fishing would be cool. we should have fishing",
                "this is the best game for doing absolutely nothing",
                "waved at a mob. it attacked. rude",
            ],
            [T.Life] =
            [
                "brb making tea",
                "it's raining here too lol. both worlds wet",
                "should sleep. won't",
                "had pizza. game is better with pizza",
                "my cat walked on the keyboard and i ran into a mob",
                "long day. this is my couch time",
                "sunny out. playing anyway",
                "snack break every level. that's the rule",
                "need a nap. maybe just a short one",
                "travel tip: take the horse and look at the views",
            ],
            [T.Questions] =
            [
                "anyone know where {zone} is? like roughly",
                "what level is everyone? i'm somewhere around there",
                "is it bad to sell everything? asking for a friend (me)",
                "anyone wanna just hang out somewhere",
                "where do people get those cool cloaks",
                "how do you know if a mob is too strong? besides dying",
                "is {item} good or should i sell it",
                "anyone up for a slow group? like really slow",
                "what's everyone doing tonight",
                "does anyone actually read the quest text",
            ],
        },
        TopicReplies = new Dictionary<T, string[]>
        {
            [T.Xp] = ["nice {0}! i'm still on my first bubble lol", "you're fast {0}. i'm more of a stroller", "xp talk makes me sleepy {0}", "good for you {0}, honestly"],
            [T.Gear] = ["ooh shiny {0}", "that's cool {0}. i have... a stick", "does it come in pink {0}", "nice {0}, wear it with pride"],
            [T.Zones] = ["ooh where's that {0}", "i got lost there once {0}", "sounds pretty {0}", "take me with you next time {0}"],
            [T.Classes] = ["i don't really get classes {0} but cool", "{0} that sounds fun", "is that the one with the pet {0}", "i just pick what looks nice {0}"],
            [T.Dungeons] = ["only if you lead {0}", "dungeons scare me {0} but ok", "i'll come and probably die {0}", "is it dark in there {0}"],
            [T.RvR] = ["i'll watch from a hill {0}", "rvr? i'll come and die a lot {0}", "sounds loud {0}", "bring me {0}, i'll carry snacks"],
            [T.Trading] = ["i have random junk if you want {0}", "how much {0}? i'm poor lol", "i just sell stuff to vendors {0}", "trade you a potion {0}? no idea what it does"],
            [T.Realm] = ["i love that place {0}", "the npcs there are nice {0}", "never been {0}. sounds cool", "is that near the stables {0}"],
            [T.Gripes] = ["aw that sucks {0}", "same {0} lol", "it's ok {0}, it happens to me daily", "deep breaths {0}. it's a game"],
            [T.Fun] = ["lol {0}", "haha good one {0}", "that's so dumb {0}. i love it", "{0} you're funny"],
            [T.Life] = ["same {0}", "oh nice {0}", "{0} that sounds cozy", "i need that too {0}"],
            [T.Questions] = ["no idea {0} lol", "good question {0}. i'd ask a guard", "{0} i think? don't quote me", "hmm {0}, i usually just wing it"],
        },
        Ding =
        [
            "oh i dinged! {level}! neat",
            "ding {level}. how did that happen",
            "{level} yay. snack break",
            "level {level}! didn't even notice",
            "ding lol. {level} i think",
            "{level}!! ok i'm proud of me",
        ],
        Grats =
        [
            "grats {0}!!",
            "yay {0}! nice one",
            "gz {0} :)",
            "woo {0} dinged",
            "grats {0}, you earned a snack",
            "nice {0}! catching up on me lol",
        ],
        OwnDeath =
        [
            "lol died",
            "oops. dead again",
            "well that happened",
            "i'm fine. i'm dead but i'm fine",
            "died. gonna lie here a minute",
            "that thing was mean",
        ],
        OtherDeath =
        [
            "oh no {0}!",
            "rip {0} lol",
            "{0} it happens. i do it all the time",
            "aw {0}. want me to wait",
            "{0} down! take your time",
            "you ok {0}?",
        ],
        Drop =
        [
            "ooh {item}! shiny",
            "got {item}. no idea what it does",
            "{item}! is that good?",
            "found a thing. it's {item}",
            "oh nice, a drop. keeping it",
            "{item} lol. cool",
        ],
        Cheeky = null,
    };
}
