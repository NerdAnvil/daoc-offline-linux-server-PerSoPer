using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class SceneLines
{
    // --- Joker: puns, teasing; Cheeky closers only when tone-down is off ---
    private static SceneTypeLines Joker() => new()
    {
        Comebacks = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "{0}, my XP bar is shy. It only fills when nobody looks.",
                "Slow and steady, {0}. Mostly slow.",
                "{0}, I don't grind. I sort of... grate.",
                "Leveling is a journey, {0}. Mine has lots of rest stops.",
            ],
            [T.Gear] =
            [
                "{0}, I dress for the job I want. Court jester.",
                "Shiny is a stat, {0}. Look it up.",
                "{0}, my armour has holes. It's breathable.",
                "Nice, {0}. Mine came with a free dent.",
            ],
            [T.Zones] =
            [
                "{0}, I've seen that zone. Mostly face down.",
                "Great place, {0}. The ground there is very soft. I checked.",
                "{0}, I'd go, but I'm map-sick.",
            ],
            [T.Classes] =
            [
                "{0}, every class has a weakness. Mine is jokes.",
                "Ah, {0}, but can it juggle?",
                "{0}, my class's best skill is running away. With style.",
            ],
            [T.Dungeons] =
            [
                "{0}, I love dungeons. Great acoustics for screaming.",
                "Count me in, {0}. I'll carry the torch. And the puns.",
                "{0}, in a dungeon nobody can hear you joke. I've tried.",
            ],
            [T.RvR] =
            [
                "{0}, I'll storm the keep. Lightly. With a drizzle.",
                "The frontier, {0}? I'll bring a white flag. Fashion choice.",
                "{0}, my RvR plan is confusing them with puns.",
            ],
            [T.Trading] =
            [
                "{0}, I'll trade you a joke. Very rare. Barely used.",
                "Price check, {0}: my sense of humour. Priceless. Also worthless.",
                "{0}, I'm a terrible haggler. I just laugh at the price.",
            ],
            [T.Realm] =
            [
                "{0}, I asked a guard for directions. He pointed at the exit.",
                "Ah, the lore, {0}. Old stories, older jokes.",
                "{0}, I know every tavern. For research.",
            ],
            [T.Gripes] =
            [
                "{0}, look on the bright side. There's a respawn.",
                "Cheer up, {0}. At least you're not my XP bar.",
                "{0}, I'd complain too, but I'm too busy being charming.",
            ],
            [T.Fun] =
            [
                "{0}, finally, someone who gets me.",
                "Ha! {0}, I'm stealing that one.",
                "{0}, good joke. Second best here today.",
            ],
            [T.Life] =
            [
                "{0}, I had soup for lunch. It was a bowl-d choice.",
                "Sleep, {0}? I tried it once. Woke up dead.",
                "{0}, the weather's so nice even the mobs are smiling.",
            ],
            [T.Questions] =
            [
                "{0}, great question. Wrong person, but great question.",
                "The answer is a bigger sword, {0}. It usually is.",
                "{0}, I'd tell you, but then I'd have to stop joking.",
            ],
        },
        Closers =
        [
            "I'll be here all week, {0}.",
            "Thank you, thank you. No applause.",
            "Right, back to work. Funny work.",
            "Tip your jester, {0}.",
            "And that's my set. Goodnight!",
            "{0}, same time tomorrow? I'll bring new material.",
            "Exit, stage left.",
            "I'd stay, but my jokes need walking.",
        ],
        CheekyClosers =
        [
            "{0}, if you laughed, that counts as a group buff.",
            "{0}, you're my favourite audience. Don't tell the others.",
            "I'd wink at you, {0}, but my helmet's in the way.",
            "{0}, that's the most fun I've had without dying.",
        ],
    };
}
