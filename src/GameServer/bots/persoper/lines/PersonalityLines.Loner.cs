using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class PersonalityLines
{
    // --- Loner: very short, dry, wants to be left alone ---
    private static TypeLines Loner() => new()
    {
        Openers = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "Slow XP. Quiet XP. Fine.",
                "Solo pace suits me.",
                "Bar's moving.",
                "No group bonus. No group. Good trade.",
                "Nearly {level}.",
                "{monster}. Again. Fine.",
                "Leveling. Don't watch.",
                "Half a bubble. Nobody helped.",
                "XP's the same alone.",
                "One more level. Then quiet.",
            ],
            [T.Gear] =
            [
                "Got {item}. Keeping it.",
                "Gear's fine.",
                "Repaired. Back out.",
                "Don't need shiny.",
                "Dull armour. Less attention.",
                "My blade works. That's enough.",
                "Bought a cloak. Dark one.",
                "No upgrades needed.",
                "Same boots for ten levels.",
                "Gear talk. Pass.",
            ],
            [T.Zones] =
            [
                "{zone}. Empty. Perfect.",
                "Too busy here.",
                "Found a quiet hill.",
                "Moving on. Crowd's coming.",
                "Nobody comes this far out. Good.",
                "This zone's loud.",
                "Edge of the map. My spot.",
                "Fog here. I like fog.",
                "Leaving {zone}. Too many people.",
                "Quiet valley. Not telling where.",
            ],
            [T.Classes] =
            [
                "Solo class. On purpose.",
                "Healers talk too much.",
                "A {class} followed me. Lost it.",
                "Stealth is underrated.",
                "Pets don't talk. Good classes.",
                "Tanks need groups. Pass.",
                "My class works alone.",
                "Never needed a buffer.",
                "Classes. Whatever works.",
                "Archers keep their distance. Smart.",
            ],
            [T.Dungeons] =
            [
                "Dungeons alone. Always.",
                "Darkness Falls is quiet below.",
                "Cleared a crypt. Nobody saw.",
                "Dark. Cold. Fine.",
                "{monster} down there. Handled it.",
                "Groups are loud in dungeons.",
                "Took the back way.",
                "Dungeon's empty tonight. Good.",
                "Went deep. Came back.",
                "No raid. Just me.",
            ],
            [T.RvR] =
            [
                "Scouting the frontier. Alone.",
                "Zergs. No.",
                "Watched a keep fall. From far away.",
                "RvR's crowded.",
                "Quiet night out there.",
                "Took a tower. Nobody noticed.",
                "Hid in the trees. Saw everything.",
                "Battlegrounds. Too many people.",
                "Enemy scouts. Gone now.",
                "Frontier's best at night.",
            ],
            [T.Trading] =
            [
                "Sold everything. Didn't talk.",
                "Merchants are fine. They don't chat.",
                "Exchange works. No haggling.",
                "Don't need much.",
                "Selling {item}. Don't ask questions.",
                "Bought arrows. Enough.",
                "Coin's fine.",
                "Not trading. Not buying.",
                "Fixed price. Take it or not.",
                "Vendor's quicker than people.",
            ],
            [T.Realm] =
            [
                "Town's too loud.",
                "Bank. Then gone.",
                "Trainer talks too much.",
                "Stablemaster doesn't chat. Respect.",
                "Read a lore book. Alone. Nice.",
                "Guards don't bother me.",
                "Capital's crowded.",
                "Small villages. Better.",
                "Old ruins out east. Quiet.",
                "Bound somewhere nobody goes.",
            ],
            [T.Gripes] =
            [
                "Someone followed me.",
                "Too much chat.",
                "Group invites. Stop.",
                "Crowded camp.",
                "{monster} won't stay dead.",
                "People shouting in town.",
                "Train came through. Annoying.",
                "Someone took my spot.",
                "Too many questions today.",
                "Noise.",
            ],
            [T.Fun] =
            [
                "Fun. Sure.",
                "Watched the rain.",
                "Sat on a cliff for an hour.",
                "Quiet is fun.",
                "Funny. I guess.",
                "Counted stars. Lost count.",
                "Found a cave. Kept it.",
                "Silence. Best part of the day.",
                "Not laughing. Inside, maybe.",
                "Saw a fox. It left me alone.",
            ],
            [T.Life] =
            [
                "Ate. Alone. Good.",
                "Rain. Fine.",
                "Didn't sleep.",
                "Long walk. Nobody around.",
                "Tea.",
                "Cold out. Stayed in.",
                "Quiet night.",
                "Walked instead of riding.",
                "Up early. Empty roads.",
                "Snow. Silence.",
            ],
            [T.Questions] =
            [
                "Anyone near {zone}? Asking to avoid it.",
                "Which camp's empty?",
                "Quiet zones? Anyone?",
                "Where's nobody?",
                "Who's leaving soon?",
                "Anyone seen my horse?",
                "Respawn on {monster}?",
                "Dungeon empty tonight?",
                "Why so many people here?",
                "Done talking yet?",
            ],
        },
        TopicReplies = new Dictionary<T, string[]>
        {
            [T.Xp] = ["Good for you, {0}.", "Mm. {0}.", "Slower alone, {0}. Better.", "Sure, {0}."],
            [T.Gear] = ["Nice, {0}.", "Gear's gear, {0}.", "Don't need it, {0}.", "Shiny. Loud. Like you, {0}."],
            [T.Zones] = ["Crowded, {0}.", "Not going, {0}.", "Been there, {0}.", "Go then, {0}."],
            [T.Classes] = ["Whatever works, {0}.", "Mine works alone, {0}.", "Hm. Your call, {0}.", "Fine class, {0}."],
            [T.Dungeons] = ["Alone, {0}.", "Not with a group, {0}.", "Been, {0}. Quiet.", "Go ahead, {0}."],
            [T.RvR] = ["I'll scout, {0}. Alone.", "No, {0}.", "Too loud, {0}.", "Later, {0}. Maybe."],
            [T.Trading] = ["Not buying, {0}.", "Not selling either, {0}.", "Vendor, {0}.", "Keep it, {0}."],
            [T.Realm] = ["Too loud there, {0}.", "Mm, {0}.", "Never go there, {0}.", "Read it, {0}. Alone."],
            [T.Gripes] = ["Then leave, {0}.", "Same, {0}.", "Happens, {0}.", "Talk less, {0}."],
            [T.Fun] = ["...{0}.", "Right, {0}.", "Ha. {0}.", "If you say so, {0}."],
            [T.Life] = ["Mm. Nice, {0}.", "Fine, {0}.", "Same, {0}. Quieter.", "Good, {0}."],
            [T.Questions] = ["Figure it out, {0}.", "Trainer, {0}.", "Don't know, {0}.", "Ask someone else, {0}."],
        },
        Ding =
        [
            "{level}.",
            "Ding. {level}.",
            "Level {level}. Quietly.",
            "{level}. Moving on.",
            "Hit {level}. Don't make it a thing.",
            "{level}. Alone. Good.",
        ],
        Grats =
        [
            "Grats, {0}.",
            "Gz {0}.",
            "{0}. Grats.",
            "Not bad, {0}. Grats.",
            "Grats {0}. Quietly.",
            "Hm. Grats, {0}.",
        ],
        OwnDeath =
        [
            "Dead.",
            "Hm. Died.",
            "My fault.",
            "Releasing.",
            "Long walk back.",
            "Nobody saw. Good.",
        ],
        OtherDeath =
        [
            "{0}'s down.",
            "Hm. {0}.",
            "Get up, {0}.",
            "{0} died. Not my problem.",
            "Should've gone alone, {0}.",
            "{0}. Careless.",
        ],
        Drop =
        [
            "{item}. Good.",
            "Got {item}.",
            "Hm. {item}.",
            "{item}. Keeping it.",
            "Good drop.",
            "{item}. Nobody needs to know.",
        ],
        Cheeky = null,
    };
}
