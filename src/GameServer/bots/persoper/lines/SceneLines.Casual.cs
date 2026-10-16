using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class SceneLines
{
    // --- Casual: relaxed, lower case, here for fun ---
    private static SceneTypeLines Casual() => new()
    {
        Comebacks = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "{0} the bubbles will still be there after a snack",
                "haha {0} i level by accident mostly",
                "{0} i'm not racing anyone, it's fine",
                "you're so fast {0}. i'm just enjoying the walk",
            ],
            [T.Gear] =
            [
                "{0} i just like the colour honestly",
                "ok {0} but does it look cute",
                "{0} i wear whatever drops on me lol",
                "matching set? {0} that's the dream",
            ],
            [T.Zones] =
            [
                "{0} i like the one with the pretty river",
                "ooh {0} i'll go there for the views",
                "{0} i still get lost on the way though",
            ],
            [T.Classes] =
            [
                "{0} i picked mine because the robe looked nice",
                "haha {0} i still don't know half my spells",
                "{0} whatever, they're all fun to me",
            ],
            [T.Dungeons] =
            [
                "{0} ok but i'm standing at the back",
                "{0} can we stop if it gets too spooky",
                "i'll come {0} if someone holds my hand. figuratively",
            ],
            [T.RvR] =
            [
                "{0} i'll come and mostly watch the fireworks",
                "haha {0} i'll probably die first, it's tradition",
                "{0} can we just wave at the enemy instead",
            ],
            [T.Trading] =
            [
                "{0} i sold a sword for a pie once. no regrets",
                "{0} i just give stuff away tbh",
                "ooh {0} do you have anything shiny to trade",
            ],
            [T.Realm] =
            [
                "{0} i love the music in town",
                "aww {0} that's a nice story",
                "{0} i talk to every npc, they're friends now",
            ],
            [T.Gripes] =
            [
                "{0} aw don't be grumpy, have a snack",
                "{0} it's just a game, it's ok",
                "{0} want to go pick flowers instead",
            ],
            [T.Fun] =
            [
                "haha {0} good one",
                "{0} you're funny, we should group",
                "{0} lol stop, i'm laughing at my keyboard",
            ],
            [T.Life] =
            [
                "{0} same, i need a nap after this",
                "ooh {0} what are you having for dinner",
                "{0} the weather here is so cosy today",
            ],
            [T.Questions] =
            [
                "{0} no idea honestly, ask someone smart",
                "{0} i think? maybe? don't quote me",
                "good question {0}. i'll pretend i know",
            ],
        },
        Closers =
        [
            "anyway, nice talking {0}",
            "ok, off to pick flowers",
            "fair enough {0} :)",
            "brb, snack time",
            "see you around {0}",
            "ok back to wandering",
            "this was nice {0}",
            "right, where was i going again",
        ],
        CheekyClosers = null,
    };
}
