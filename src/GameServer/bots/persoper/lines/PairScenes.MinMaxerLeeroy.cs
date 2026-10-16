using System.Collections.Generic;
using static DOL.GS.eSceneSize;

namespace DOL.GS;

public static partial class PairScenes
{
    // --- Min/maxer (A) lectures Leeroy (B): plans against charging in. C = any other bot who joins in. ---
    private static List<SceneScript> MinMaxerLeeroy() =>
    [
        SceneScript.Of(Short,
            "A: {b}, your death count this week is higher than your level.",
            "B: That's a LOT of fighting, {a}!",
            "A: That's not the lesson, {b}."),

        SceneScript.Of(Short,
            "A: {b}, did you check your resists before that pull?",
            "B: I checked if it was BIG, {a}!",
            "A: That's not a stat.",
            "B: It is to ME!"),

        SceneScript.Of(Short,
            "A: {b}, your template has three points in nothing useful.",
            "B: They're in COURAGE, {a}!",
            "A: There is no courage skill line, {b}."),

        SceneScript.Of(Short,
            "A: {b}, wait for the healer's power to refill.",
            "B: Power is a state of MIND, {a}!",
            "A: It's a blue bar, {b}. It's empty."),

        SceneScript.Of(Medium,
            "A: {b}, before you pull {monster}, let's go over the plan.",
            "B: The plan is I RUN IN!",
            "A: Step one: check the adds. Step two: wait for the healer.",
            "B: Step three: I RUN IN!",
            "A: There is no step three, {b}.",
            "B: There is NOW!",
            "C: I'll get the rez ready."),

        SceneScript.Of(Medium,
            "A: {b}, I made you a chart.",
            "B: A CHART? Does it explode?",
            "A: It shows your deaths per hour. The line only goes up.",
            "B: UP is GOOD, {a}!",
            "A: Not for this chart, {b}.",
            "B: Then make a chart where it's good!"),

        SceneScript.Of(Medium,
            "A: {b}, at your level you should be pulling one blue at a time.",
            "B: One?! What do I do with my OTHER hand, {a}?",
            "A: Hold a potion. That's the point.",
            "B: Potions are for after!",
            "A: After what?",
            "B: After I'm DEAD, obviously!",
            "C: ...I'm starting to see the problem."),

        SceneScript.Of(Large,
            "A: {b}, I ran the numbers on your last dungeon run.",
            "B: Did I WIN, {a}?",
            "A: You pulled four rooms at once.",
            "B: That's FOUR TIMES the fun!",
            "A: The group died in eleven seconds.",
            "B: Eleven seconds of GLORY!",
            "A: Our repair bill was higher than the loot.",
            "B: Glory isn't cheap, {a}!",
            "C: For the record, I was in that group.",
            "A: Next time, {b}, we follow the pull order.",
            "B: Fine! I'll pull in ORDER! All of them! At once!",
            "A: ...I'll make another chart."),

        SceneScript.Of(Large,
            "A: {b}, I've optimised your spec.",
            "B: Does it make me CHARGE faster?",
            "A: It makes you live longer. About 40% longer.",
            "B: Longer than WHAT, {a}?",
            "A: Than the six seconds you usually last.",
            "B: Six seconds is plenty! I do a LOT in six seconds!",
            "A: You run in, shout, and fall over.",
            "B: In THAT ORDER! Every time! That's consistency!",
            "A: ...That's actually a fair point about consistency.",
            "B: SEE? I'm OPTIMAL!",
            "A: No, {b}. You're predictable. That's different.",
            "B: PREDICTABLY AWESOME! CHARGE!"),
    ];
}
