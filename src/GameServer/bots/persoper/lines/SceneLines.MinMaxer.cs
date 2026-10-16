using System.Collections.Generic;
using T = DOL.GS.eChatTopic;

namespace DOL.GS;

public static partial class SceneLines
{
    // --- Min/maxer: numbers, caps, optimal choices ---
    private static SceneTypeLines MinMaxer() => new()
    {
        Comebacks = new Dictionary<T, string[]>
        {
            [T.Xp] =
            [
                "{0}, rest time counts. Your real rate is lower.",
                "Measure it, {0}. Feelings are not data.",
                "{0}, XP per kill is a trap. Use XP per minute.",
                "Close, {0}. Pull one level higher and it's 20% better.",
            ],
            [T.Gear] =
            [
                "{0}, check the bonuses before you get attached.",
                "That only helps if you're not capped, {0}.",
                "{0}, I'd trade looks for two points of a capped stat. Every time.",
                "Overcapped stats are wasted stats, {0}.",
            ],
            [T.Zones] =
            [
                "{0}, mob density beats scenery. Always.",
                "I timed the respawns there, {0}. Average at best.",
                "{0}, the travel time cancels out the better camp.",
            ],
            [T.Classes] =
            [
                "{0}, that depends entirely on the spec.",
                "Respec and you'd be 15% stronger, {0}.",
                "{0}, a class is only as good as its template.",
            ],
            [T.Dungeons] =
            [
                "{0}, I have a pull order. We follow it.",
                "Only if the drop is worth the time, {0}.",
                "{0}, healer, tank, crowd control. Then we talk.",
            ],
            [T.RvR] =
            [
                "{0}, cap your resists first. Then we talk.",
                "Defending gives better points per hour, {0}.",
                "{0}, bring numbers. The frontier rewards math.",
            ],
            [T.Trading] =
            [
                "{0}, I checked the prices. You're overpaying.",
                "Sell it to the merchant, {0}. The difference is 3%.",
                "{0}, buy low, sell at the right hour. Simple.",
            ],
            [T.Realm] =
            [
                "{0}, the only lore I need is the trainer's list.",
                "Interesting, {0}. Is there a stat bonus involved?",
                "{0}, I know which merchant has the cheapest arrows.",
            ],
            [T.Gripes] =
            [
                "{0}, that's a known problem. I have a workaround.",
                "Complain with data, {0}. It's more convincing.",
                "{0}, your issue is the template, not the game.",
            ],
            [T.Fun] =
            [
                "{0}, efficiency is fun. You'll see.",
                "Funny, {0}. Statistically.",
                "{0}, I laughed. About 0.4 seconds, but I laughed.",
            ],
            [T.Life] =
            [
                "{0}, I eat during the respawn timer. Optimal.",
                "Sleep is important, {0}. Eight hours. Measured.",
                "{0}, I plan meals around the long runs.",
            ],
            [T.Questions] =
            [
                "{0}, short answer: it depends on the numbers.",
                "I have a chart for that, {0}.",
                "{0}, test it twice, then ask again.",
            ],
        },
        Closers =
        [
            "I'll run the numbers later.",
            "Noted, {0}. Suboptimal, but noted.",
            "Back to my spreadsheet.",
            "We'll revisit this with data, {0}.",
            "Good talk. Efficient, even.",
            "I have a template to finish.",
            "Fine, {0}. Agree to disagree. I'm right, though.",
            "Logging that for later.",
        ],
        CheekyClosers = null,
    };
}
