using System.Collections.Generic;
using static DOL.GS.eSceneSize;

namespace DOL.GS;

public static partial class PairScenes
{
    // --- Joker (A) teases Loner (B): chatter against silence. C = any other bot who joins in. ---
    private static List<SceneScript> JokerLoner() =>
    [
        SceneScript.Of(Short,
            "A: {b}! Nice to see you, wherever you're hiding.",
            "B: Not hiding, {a}.",
            "A: That's exactly what someone hiding would say."),

        SceneScript.Of(Short,
            "A: {b}, knock knock.",
            "B: No.",
            "A: No who?",
            "B: ...Go away, {a}."),

        SceneScript.Of(Short,
            "A: {b}, I've decided we're best friends.",
            "B: Undecide it, {a}.",
            "A: Too late. I've ordered matching cloaks."),

        SceneScript.Of(Short,
            "A: {b}, how's the quiet life?",
            "B: Was quiet, {a}.",
            "A: You're welcome!"),

        SceneScript.Of(Medium,
            "A: {b}, I wrote you a joke. It's very short. Like your replies.",
            "B: No.",
            "A: That's the punchline, actually. Well done.",
            "B: Go away, {a}.",
            "A: See? You're warming up to me.",
            "C: Leave them be, {a}."),

        SceneScript.Of(Medium,
            "A: {b}, what do you call someone who hunts alone?",
            "B: Happy.",
            "A: I was going to say 'a loner', but yours is better.",
            "B: Mm.",
            "A: Want to hear another one?",
            "B: No, {a}.",
            "A: I'll save it for later. I'll whisper it."),

        SceneScript.Of(Medium,
            "A: {b}, I saw you smile yesterday.",
            "B: You didn't, {a}.",
            "A: Near {zone}. At a rabbit.",
            "B: ...It was a good rabbit.",
            "A: Ha! So you DO have feelings!",
            "B: One. It's for rabbits."),

        SceneScript.Of(Large,
            "A: {b}, let's group up. Just us two.",
            "B: No.",
            "A: I'll be quiet. Completely silent.",
            "B: You can't, {a}.",
            "A: Watch me.",
            "B: ...",
            "A: ...",
            "B: ...",
            "A: This is the longest I've ever gone without a joke.",
            "B: Eight seconds, {a}.",
            "A: A personal best! Group?",
            "B: ...Fine. One pull."),

        SceneScript.Of(Large,
            "A: {b}, I've been practising silence. Like you.",
            "B: Good.",
            "A: It's very hard. My jokes keep trying to escape.",
            "B: Let them die, {a}.",
            "C: Please do.",
            "A: Everyone's a critic.",
            "B: Everyone's right.",
            "A: {b}, was that... a joke?",
            "B: No.",
            "A: It was! You made a joke! I'm so proud!",
            "B: Never again, {a}."),

        SceneScript.CheekyOf(Short,
            "A: {b}, do you talk to anyone, or just your pet?",
            "B: The pet listens, {a}.",
            "A: Fair. I'd listen too if you fed me."),

        SceneScript.CheekyOf(Short,
            "A: {b}, I bet you're a great dancer. When nobody's looking.",
            "B: Nobody is looking, {a}.",
            "A: I'm looking!",
            "B: Not for long."),
    ];
}
