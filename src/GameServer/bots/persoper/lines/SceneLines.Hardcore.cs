using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class SceneLines
{
    // --- Hardcore: blunt, efficient, a little impatient ---
    private static SceneTypeLines Hardcore() => new()
    {
        Comebacks = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "{0}, a fine pace is how people stay at 30 forever.",
                "Fair, {0}. Now pull two at once.",
                "{0}, every minute you sit is a minute I'm ahead.",
                "Numbers, {0}. Not feelings. What's your hourly?",
            ],
            [T.Gear] =
            [
                "{0}, looks are free. Damage costs time.",
                "Sure, {0}. Wear it while you pull.",
                "{0}, if it doesn't kill faster, it's decoration.",
                "Upgrade later, {0}. Level now.",
            ],
            [T.Zones] =
            [
                "{0}, the best zone is the one with no queue.",
                "Scenery is for people who aren't levelling, {0}.",
                "{0}, I don't care where. I care how fast it respawns.",
            ],
            [T.Classes] =
            [
                "{0}, any class works if you stop resting.",
                "Fine, {0}. Show me it solo something orange.",
                "{0}, the best class is the one that's already pulling.",
            ],
            [T.Dungeons] =
            [
                "{0}, in and out. No sightseeing in there.",
                "Fine, {0}. But if anyone stops to loot every corpse, I leave.",
                "{0}, I'll lead. You keep up.",
            ],
            [T.RvR] =
            [
                "{0}, realm points later. Levels first.",
                "Fine, {0}. One keep run. Then back to the camp.",
                "{0}, dying out there costs time. Don't.",
            ],
            [T.Trading] =
            [
                "{0}, haggling takes longer than the coin is worth.",
                "Just sell it, {0}. Bags full means no loot.",
                "{0}, price it fair and move on. Time matters.",
            ],
            [T.Realm] =
            [
                "{0}, I know where the trainers are. That's enough lore.",
                "Nice story, {0}. Does it give XP?",
                "{0}, I run past the town. I don't tour it.",
            ],
            [T.Gripes] =
            [
                "{0}, complaining is downtime too.",
                "Fair point, {0}. Now pull.",
                "{0}, everyone has a bad camp. Move on.",
            ],
            [T.Fun] =
            [
                "{0}, levelling IS the fun.",
                "Ha. Fine, {0}. One laugh, then back to it.",
                "{0}, I'll have fun at the level cap.",
            ],
            [T.Life] =
            [
                "{0}, eat while you run. Two jobs at once.",
                "Sleep is for people with slow XP, {0}.",
                "{0}, weather doesn't stop the respawn.",
            ],
            [T.Questions] =
            [
                "Short answer, {0}: pull more, rest less.",
                "{0}, ask me again when you're five levels up.",
                "Good question, {0}. The answer is still efficiency.",
            ],
        },
        Closers =
        [
            "Enough talk. Pulling.",
            "Right. Back to it, {0}.",
            "Good chat. Bubbles are waiting.",
            "Done here. Mobs won't kill themselves.",
            "Moving on, {0}.",
            "Timer's running. Later.",
            "Talk at the next ding, {0}.",
            "That's my break over.",
        ],
        CheekyClosers = null,
    };
}
