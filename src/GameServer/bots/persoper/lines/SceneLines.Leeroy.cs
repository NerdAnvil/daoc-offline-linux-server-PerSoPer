using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class SceneLines
{
    // --- Leeroy: loud, reckless, charges in; Cheeky closers only when tone-down is off ---
    private static SceneTypeLines Leeroy() => new()
    {
        Comebacks = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "{0}, XP comes faster if you pull EVERYTHING!",
                "Less counting, {0}! More charging!",
                "{0}, I level by running into the biggest thing I see!",
                "Rest? {0}, I rest when I'm dead! Which is often!",
            ],
            [T.Gear] =
            [
                "{0}, gear is for denting! Let's dent it!",
                "It looks great when it's on fire, {0}!",
                "{0}, who needs armour when you've got SPEED?!",
                "Does it come in LOUDER, {0}?",
            ],
            [T.Zones] =
            [
                "{0}, what's the BIGGEST thing there? I'm going!",
                "Race you there, {0}! GO!",
                "{0}, I've died in every zone! It's my map!",
            ],
            [T.Classes] =
            [
                "{0}, can it CHARGE? Then it's the best!",
                "Every class is great if you run in first, {0}!",
                "{0}, my class has one button! It's called GO!",
            ],
            [T.Dungeons] =
            [
                "{0}, I'll pull the whole room! You're welcome!",
                "Plan? {0}, the plan is DOWNSTAIRS!",
                "{0}, I know a shortcut! It's straight through the boss!",
            ],
            [T.RvR] =
            [
                "{0}, I'm taking the keep! ALONE IF I HAVE TO!",
                "FRONTIER! {0}, are you coming or not?!",
                "{0}, more enemies means more fun!",
            ],
            [T.Trading] =
            [
                "{0}, I'll buy anything that hits HARD!",
                "Trading is slow, {0}! I'll just loot more!",
                "{0}, give me your biggest weapon! I'll bring it back! Probably!",
            ],
            [T.Realm] =
            [
                "{0}, the best part of town is the gate OUT!",
                "Lore? {0}, I MAKE the legends!",
                "{0}, the guards know my name! Mostly from complaints!",
            ],
            [T.Gripes] =
            [
                "{0}, stop moaning and CHARGE!",
                "Bad camp? {0}, find a bigger one!",
                "{0}, problems are just mobs you haven't hit yet!",
            ],
            [T.Fun] =
            [
                "HA! {0}, that's GREAT!",
                "{0}, you know what's fun? CHARGING!",
                "{0}, I laughed so hard I almost pulled something!",
            ],
            [T.Life] =
            [
                "{0}, I eat on the run! Literally!",
                "Sleep is for people without a target, {0}!",
                "{0}, weather?! Who cares! CHARGE through it!",
            ],
            [T.Questions] =
            [
                "{0}, the answer is always ATTACK!",
                "Don't think, {0}! DO!",
                "{0}, I'll find out by running in! Brb!",
            ],
        },
        Closers =
        [
            "ENOUGH TALK! CHARGE!",
            "I'm going in, {0}!",
            "Right! To battle!",
            "{0}, follow me! Or don't! GO!",
            "Talking is over! FIGHTING time!",
            "See you at the corpse run, {0}!",
            "Here I GOOO!",
            "{0}, cover me! I'm doing something brave!",
        ],
        CheekyClosers =
        [
            "{0}, if I die, tell my repair bill I loved it.",
            "{0}, hold my drink. And my pride. I'm going in!",
            "Last one in buys the ale, {0}!",
            "{0}, if I'm not back in five minutes, I'm dead. Obviously!",
        ],
    };
}
