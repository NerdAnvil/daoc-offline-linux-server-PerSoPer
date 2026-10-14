using System;

namespace DOL.GS;

/// <summary>
/// Kobaldjovi, the Singer: the second PerSoPer personality. Unlike Persoper he
/// is not summoned; he lives in the world as an ordinary autonomous gamebot
/// (a Midgard Kobold Skald) and mostly sings when it is his turn to chat.
/// Pure logic only, so it can be unit tested without a running world.
/// </summary>
public static class KobaldjoviIdentity
{
    public const string Name = "Kobaldjovi";
    public const eRealm Realm = eRealm.Midgard;
    public const eGender Gender = eGender.Male;
    public const eCharacterClass CharacterClass = eCharacterClass.Skald;
    public const eRace Race = eRace.Kobold;

    /// <summary>Chance that his ambient chat line is a song rather than ordinary talk.</summary>
    public const double SingChance = 0.8;

    // Original lines only: no real lyrics. He names artists and genres, never quotes them.
    private static readonly string[] Songs =
    [
        "~ Oh Jordheim, my frozen love, your snow falls soft as power ballads from above ~",
        "~ I'm a Kobold on a long road, wanted dead or rezzed ~ ...that one needs work.",
        "~ Hair this big takes courage, and courage takes hairspray ~",
        "~ Spin the lute, drop the beat, the troll in the back has got two left feet ~",
        "~ RAAAARGH, THE FROST GIANT HUNGERS ~ ...sorry, death metal phase. It comes and goes.",
        "~ Whoop whoop, the juggalo of Jotunheim ~ ...the faygo is mead, it counts.",
        "~ Hmmmmmmmmm-ooooo ~ That was throat singing. You could feel it in your boots, admit it.",
        "~ Glitter on my beard and sequins on my axe ~ Madonna would approve.",
        "~ My horse left me at the stable, my mead is warm, my boots are sad ~ Country is hard.",
        "~ Om... the wind... the pine... the gentle respawn timer ~ New age. Very calming.",
        "~ Da da da DUM ~ Beethoven, but on a lute. Close enough.",
        "~ Two hearts, one group, zero mana potions ~ A romance. Tragic, really.",
        "~ Chorus! Everybody now! ~ ...anybody? Fine, I'll do the harmony too.",
        "~ They say a Skald can't rap, but a Skald just did ~ Mic drop. Lute drop. Careful, that's my lute.",
        "~ Comic books and candlelight, I'll read until the end of night ~",
        "~ Pages turning, dragons burning, every chapter, I keep learning ~",
    ];

    private static readonly string[] Asides =
    [
        "Anyone up for a sing-along? No? I'll start, you join in.",
        "Picked up a new comic in Jordheim. The hero is a Kobold. Obviously.",
        "Reading tip: never lend a book to a Troll. Ask me how I know.",
        "Best decade for music? Trick question. It's the one with the big hair.",
        "If you hear singing in the hills, that's me. If it's bad singing, also me.",
        "I take requests. I ignore most of them, but I take them.",
    ];

    /// <summary>True when a character or bot name would impersonate Kobaldjovi.</summary>
    public static bool IsReservedName(string name) =>
        !string.IsNullOrEmpty(name) && name.Contains(Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>True for the one roster bot that is Kobaldjovi.</summary>
    public static bool Is(GameBot bot) =>
        bot != null && string.Equals(bot.Name, Name, StringComparison.OrdinalIgnoreCase);

    public static AutonomousBotIdentityGenerator.Identity CreateIdentity() =>
        new(Name, Realm, Gender, CharacterClass, Race);

    /// <summary>Picks his ambient line: usually a song, sometimes an aside.</summary>
    public static string AmbientLine(Random random, out bool isSong)
    {
        random ??= Random.Shared;
        isSong = random.NextDouble() < SingChance;
        string[] lines = isSong ? Songs : Asides;
        return lines[random.Next(lines.Length)];
    }
}
