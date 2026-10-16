using System.Collections.Generic;
using static DOL.GS.eSceneSize;

namespace DOL.GS;

public static partial class PairScenes
{
    // --- Hardcore (A) vs Casual (B): speed against fun. C = any other bot who joins in. ---
    private static List<SceneScript> HardcoreCasual() =>
    [
        SceneScript.Of(Short,
            "A: {b}, still in {zone}? I left there five levels ago.",
            "B: {a}, some of us stop to look at the trees.",
            "A: Trees don't give XP, {b}."),

        SceneScript.Of(Short,
            "A: {b}, why are you sitting?",
            "B: i'm having a little picnic {a}",
            "A: In the middle of a camp?",
            "B: best view of the fighting tbh"),

        SceneScript.Of(Short,
            "A: {b}, what level are you now?",
            "B: um... a good one? {a}",
            "A: That's not a number, {b}."),

        SceneScript.Of(Short,
            "A: {b}, where are you off to? The camp is that way.",
            "B: there's a waterfall i want to look at {a}",
            "A: A waterfall. During prime hunting hours?",
            "B: it's a really nice waterfall"),

        SceneScript.Of(Medium,
            "A: {b}, how long have you been on that level?",
            "B: um. a while? i stopped counting {a}",
            "A: You stopped counting. That's the problem.",
            "B: i had a nice chat with a merchant though",
            "A: A merchant. Who gave you no XP.",
            "B: he gave me a biscuit",
            "C: Honestly, {a}, the biscuit sounds better."),

        SceneScript.Of(Medium,
            "A: {b}, I saw you dancing at the bind stone. For ten minutes.",
            "B: it was a good song {a}",
            "A: There's no song. It's a stone.",
            "B: you have to listen with your heart",
            "A: I listen with my XP bar, {b}.",
            "B: that sounds lonely {a}"),

        SceneScript.Of(Medium,
            "A: {b}, you're pulling {monster} one at a time?",
            "B: yeah, they look friendly when they're alone {a}",
            "A: Two at a time. Minimum.",
            "B: but then they argue over who gets to bite me",
            "A: Then let them argue faster, {b}.",
            "B: ok coach"),

        SceneScript.Of(Large,
            "A: {b}. Group with me. One hour. Real pulling.",
            "B: one hour? like, a whole hour {a}?",
            "A: Sixty minutes. No sitting, no shopping, no sightseeing.",
            "B: can i at least wave at people",
            "A: You can wave while you run.",
            "B: what if i get tired",
            "A: Then you get tired at a higher level.",
            "B: ok but i'm bringing snacks",
            "A: Fine. Eat while I pull.",
            "B: deal. wait, which way is {zone}",
            "A: ...Follow me, {b}. Closely.",
            "B: yay, a field trip"),

        SceneScript.Of(Large,
            "A: {b}, I have a schedule for you.",
            "B: ooh a schedule? like a timetable {a}",
            "A: Wake up. Pull. Rest only when the healer says so. Pull again.",
            "B: where's lunch on it",
            "A: Lunch is when the respawn is slow.",
            "B: and naps?",
            "A: There are no naps, {b}.",
            "C: I've seen this schedule. Nobody survives it.",
            "A: I survive it.",
            "B: you're not really a person though {a}, you're a spreadsheet with a sword",
            "A: ...That's the nicest thing anyone has said to me.",
            "B: aww. ok, i'll try your schedule. tomorrow"),
    ];
}
