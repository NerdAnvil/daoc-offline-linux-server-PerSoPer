using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class SceneLines
{
    // --- Loner: few words, prefers solo ---
    private static SceneTypeLines Loner() => new()
    {
        Comebacks = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "Slower alone, {0}. Still mine.",
                "Hm. Fine, {0}.",
                "{0}. I don't count.",
                "No rush, {0}.",
            ],
            [T.Gear] =
            [
                "Works, {0}. That's enough.",
                "Not looking, {0}.",
                "Quiet armour, {0}. Best kind.",
                "Mine's fine, {0}.",
            ],
            [T.Zones] =
            [
                "Too many people there, {0}.",
                "I know a quieter spot, {0}. Not telling.",
                "Hm. Empty is better, {0}.",
            ],
            [T.Classes] =
            [
                "Mine solos, {0}. That's all I need.",
                "Sure, {0}.",
                "Whatever keeps you alive, {0}.",
            ],
            [T.Dungeons] =
            [
                "Groups are loud, {0}.",
                "I'll go after you leave, {0}.",
                "Pass, {0}.",
            ],
            [T.RvR] =
            [
                "I'll watch from the trees, {0}.",
                "Crowds. No, {0}.",
                "Maybe. Alone, {0}.",
            ],
            [T.Trading] =
            [
                "I make my own, {0}.",
                "Merchant's quieter, {0}.",
                "Keep it, {0}.",
            ],
            [T.Realm] =
            [
                "Towns are noisy, {0}.",
                "I know the old roads, {0}.",
                "Hm. Old story, {0}.",
            ],
            [T.Gripes] =
            [
                "Go solo, {0}. Fewer problems.",
                "Mm. Happens, {0}.",
                "Don't need to hear it, {0}.",
            ],
            [T.Fun] =
            [
                "...Heh. {0}.",
                "Not bad, {0}.",
                "Stop, {0}.",
            ],
            [T.Life] =
            [
                "Quiet campfire, {0}. That's life.",
                "Rain's fine, {0}.",
                "Eat, sleep, hunt, {0}.",
            ],
            [T.Questions] =
            [
                "Figure it out, {0}. I did.",
                "Don't know, {0}.",
                "Ask someone louder, {0}.",
            ],
        },
        Closers =
        [
            "Done talking.",
            "Later, {0}.",
            "Mm.",
            "Going now.",
            "Enough, {0}.",
            "...",
            "Back to the quiet.",
            "Bye, {0}.",
        ],
        CheekyClosers = null,
    };
}
