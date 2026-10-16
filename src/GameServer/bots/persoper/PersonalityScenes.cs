using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS;

/// <summary>Where a scene is said.</summary>
public enum eSceneChannel
{
    Faction,
    Nearby,
    Group,
}

/// <summary>Who takes part in a topic scene: name, type key and what its lines can be filled with.</summary>
public sealed record SceneVoice(string Name, string TypeKey, LineSlots Slots);

/// <summary>
/// Back-and-forth scenes between personality-type bots (Step B2). Topic scenes carry on a
/// conversation a typed bot opened (reply, comebacks by name, a closer); pair scenes are
/// short scripts between rival types (PairScenes). Sizes: Short (up to 4 lines, 70%),
/// Medium (up to 8, 25%), Large (up to 12, 5%; scripted only). Rules are pure and unit
/// tested; the running-server part plays the lines one by one through the chat coordinator
/// and stops the scene when a speaker can't talk any more.
/// </summary>
public static class PersonalityScenes
{
    public const int MaxSceneLines = 12;
    public const double ShortShare = 0.70;
    public const double MediumShare = 0.25;
    /// <summary>Chance a typed bot whose turn it is plays a pair scene, when a rival can join.</summary>
    public const double PairSceneChance = 0.35;
    /// <summary>Chance a Short topic scene ends with a closer.</summary>
    public const double CloserChance = 0.5;
    /// <summary>Chance a closer is cheeky instead (only with tone-down off and a type that has some).</summary>
    public const double CheekyCloserChance = 0.2;

    /// <summary>Most lines a scene of this size may have, its opener included.</summary>
    public static int MaxLines(eSceneSize size) => size switch
    {
        eSceneSize.Short => 4,
        eSceneSize.Medium => 8,
        _ => 12,
    };

    /// <summary>Fewest lines a scripted scene of this size should have.</summary>
    public static int MinLines(eSceneSize size) => size switch
    {
        eSceneSize.Short => 2,
        eSceneSize.Medium => 5,
        _ => 9,
    };

    /// <summary>The size for a roll in 0-1: 70% Short, 25% Medium, 5% Large.</summary>
    public static eSceneSize SizeFor(double roll) =>
        roll < ShortShare ? eSceneSize.Short : roll < ShortShare + MediumShare ? eSceneSize.Medium : eSceneSize.Large;

    public static eSceneSize PickSize(Random random) => SizeFor((random ?? Random.Shared).NextDouble());

    /// <summary>
    /// Chance (0-1) a topic opener turns into a scene when a typed bot answered it: more
    /// for chatty pairs, scaled by persoper_chatter_percent. 0 when the volume is 0.
    /// </summary>
    public static double ContinueChance(int starterChattiness, int partnerChattiness, int volume)
    {
        volume = Math.Clamp(volume, 0, 100);
        double talk = (Math.Clamp(starterChattiness, 1, 100) + Math.Clamp(partnerChattiness, 1, 100)) / 200.0;
        return Math.Clamp((0.3 + 0.4 * talk) * volume / 100.0, 0, 1);
    }

    /// <summary>Wait before a line is said: longer lines take longer to "type". 2.5-7 s plus up to 0.8 s.</summary>
    public static int LineDelayMs(string line, Random random) => BaseDelayMs(line) + (random ?? Random.Shared).Next(MaxDelayJitterMs);

    private const int MaxDelayJitterMs = 800;

    private static int BaseDelayMs(string line) => Math.Clamp(2_200 + (line?.Length ?? 0) * 40, 2_500, 7_000);

    /// <summary>
    /// A script of the wanted size (or the next smaller one when there is none) that the
    /// speakers on hand can play: no third speaker needed unless there is one, no cheeky
    /// script with tone-down on. Null when none fits.
    /// </summary>
    public static SceneScript PickScript(IReadOnlyList<SceneScript> scripts, eSceneSize wanted, bool hasThird, bool toneDown, Random random)
    {
        if (scripts == null || scripts.Count == 0)
            return null;
        random ??= Random.Shared;
        for (int size = (int)wanted; size >= (int)eSceneSize.Short; size--)
        {
            SceneScript[] fits = scripts.Where(script => script != null && (int)script.Size == size &&
                (hasThird || script.Speakers <= 2) && (!toneDown || !script.Cheeky)).ToArray();
            if (fits.Length > 0)
                return fits[random.Next(fits.Length)];
        }
        return null;
    }

    /// <summary>
    /// A script's lines with names ({a}, {b}, {c}) and A's slots filled. Null when a slot is
    /// not known, a speaker is missing, or the script breaks the size cap.
    /// </summary>
    public static List<SceneLine> FillScript(SceneScript script, IReadOnlyList<string> names, LineSlots slots)
    {
        if (script == null || names == null || script.Lines.Count == 0 || script.Lines.Count > MaxLines(script.Size))
            return null;
        var filled = new List<SceneLine>(script.Lines.Count);
        foreach (SceneLine line in script.Lines)
        {
            if (line.Speaker < 0 || line.Speaker >= names.Count || string.IsNullOrWhiteSpace(names[line.Speaker]))
                return null;
            string text = PersonalityReactions.Fill(line.Text, slots);
            if (text == null)
                return null;
            for (int speaker = 0; speaker < 3; speaker++)
            {
                string token = "{" + (char)('a' + speaker) + "}";
                if (!text.Contains(token, StringComparison.Ordinal))
                    continue;
                if (speaker >= names.Count || string.IsNullOrWhiteSpace(names[speaker]))
                    return null;
                text = text.Replace(token, names[speaker].Trim(), StringComparison.Ordinal);
            }
            filled.Add(new SceneLine(line.Speaker, text));
        }
        return filled;
    }

    /// <summary>
    /// The lines after a topic opener said by voices[0] (A): B's reply, then comebacks by
    /// name, sometimes C (voices[2], optional) joining in, and closers. Short gives up to 3
    /// lines, Medium and Large up to 7 (topic scenes don't go Large). The scene ends early
    /// when someone has nothing fresh to say. Speaker indexes point into voices.
    /// </summary>
    public static List<SceneLine> PlanTopicScene(eSceneSize size, eChatTopic topic, IReadOnlyList<SceneVoice> voices, bool toneDown, Random random)
    {
        var lines = new List<SceneLine>();
        if (voices == null || voices.Count < 2 || voices[0] == null || voices[1] == null)
            return lines;
        random ??= Random.Shared;
        bool hasThird = voices.Count > 2 && voices[2] != null;
        // Who speaks, to whom, and from which pool. Short: B, A, (B). Medium: B, A, B, (C, A), B, A.
        (int Speaker, int To, eStep Step)[] steps = size == eSceneSize.Short
            ? ShortSteps
            : hasThird ? MediumStepsWithThird : MediumSteps;
        int cap = Math.Min(MaxLines(size == eSceneSize.Short ? eSceneSize.Short : eSceneSize.Medium), MaxSceneLines) - 1;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach ((int speaker, int to, eStep step) in steps)
        {
            if (lines.Count >= cap)
                break;
            if (step == eStep.Closer && size == eSceneSize.Short && random.NextDouble() >= CloserChance)
                break;
            SceneVoice voice = voices[speaker];
            string other = voices[to].Name;
            string line = step switch
            {
                eStep.Reply => PickFresh(PersonalityLines.All.GetValueOrDefault(voice.TypeKey ?? string.Empty)?.TopicReplies.GetValueOrDefault(topic),
                    voice.Slots, other, used, random),
                eStep.Comeback => PickFresh(SceneLines.All.GetValueOrDefault(voice.TypeKey ?? string.Empty)?.Comebacks.GetValueOrDefault(topic),
                    voice.Slots, other, used, random),
                _ => PickFresh(CloserPool(voice.TypeKey, toneDown, random), voice.Slots, other, used, random),
            };
            // Nothing fresh to say: the conversation has run its course.
            if (line == null)
                break;
            lines.Add(new SceneLine(speaker, line));
        }
        return lines;
    }

    private enum eStep
    {
        Reply,
        Comeback,
        Closer,
    }

    private static readonly (int Speaker, int To, eStep Step)[] ShortSteps =
        [(1, 0, eStep.Reply), (0, 1, eStep.Comeback), (1, 0, eStep.Closer)];

    private static readonly (int Speaker, int To, eStep Step)[] MediumSteps =
        [(1, 0, eStep.Reply), (0, 1, eStep.Comeback), (1, 0, eStep.Comeback), (0, 1, eStep.Comeback), (1, 0, eStep.Closer), (0, 1, eStep.Closer)];

    private static readonly (int Speaker, int To, eStep Step)[] MediumStepsWithThird =
        [(1, 0, eStep.Reply), (0, 1, eStep.Comeback), (1, 0, eStep.Comeback), (2, 0, eStep.Reply),
         (0, 2, eStep.Comeback), (1, 0, eStep.Closer), (0, 1, eStep.Closer)];

    private static string[] CloserPool(string typeKey, bool toneDown, Random random)
    {
        if (SceneLines.All.GetValueOrDefault(typeKey ?? string.Empty) is not { } lines)
            return null;
        return !toneDown && lines.CheekyClosers is { Length: > 0 } cheeky && random.NextDouble() < CheekyCloserChance ? cheeky : lines.Closers;
    }

    /// <summary>A filled line from the pool that hasn't been said in this scene yet, or null.</summary>
    private static string PickFresh(IReadOnlyList<string> pool, LineSlots slots, string otherName, HashSet<string> used, Random random)
    {
        if (pool == null || pool.Count == 0)
            return null;
        for (int attempt = 0; attempt < PersonalityReactions.MaxFillTries; attempt++)
        {
            if (PersonalityReactions.Fill(pool[random.Next(pool.Count)], slots, otherName) is { } line && used.Add(line))
                return line;
        }
        return null;
    }

    // --- In the running server ---

    /// <summary>Most typed bots looked at when finding a scene partner or a third speaker.</summary>
    private const int MaxCandidates = 24;
    /// <summary>Most responders looked at for a typed scene partner (the coordinator's best-first order).</summary>
    private const int MaxPartnerLook = 3;
    private const int MaxHeldEntries = 256;

    private static readonly object Sync = new();
    /// <summary>Channels a scene is playing in (a realm, a region or a group), until when.</summary>
    private static readonly Dictionary<object, long> HeldUntil = new();

    public static object FactionKey(eRealm realm) => ("faction", realm);

    public static object NearbyKey(ushort regionId) => ("nearby", regionId);

    /// <summary>How long a scene still holds this channel (0 = free). Other talk waits for it.</summary>
    public static int HeldMs(object key)
    {
        if (key == null)
            return 0;
        long now = GameLoop.GameLoopTime;
        lock (Sync)
            return HeldUntil.TryGetValue(key, out long until) && until > now ? (int)Math.Min(int.MaxValue, until - now) : 0;
    }

    /// <summary>
    /// Called by the coordinator when a typed bot's topic opener is about to get answers:
    /// with the scene chance, the first typed answerer becomes its scene partner and the
    /// scene plays instead of the usual answers. True when a scene took over.
    /// </summary>
    public static bool TryTopicScene(GameBot starter, IReadOnlyList<GameBot> responders, eChatTopic? topic, eSceneChannel channel, Group group)
    {
        int volume = PersoperProperties.CHATTER_PERCENT;
        if (topic is not { } opened || volume <= 0 || responders == null ||
            PersonalityTypes.InstanceFor(starter) is not { TypeKey: { } starterType } starterPersonality)
            return false;
        var typed = responders.Take(MaxCandidates)
            .Select(bot => (Bot: bot, Personality: bot == starter ? null : PersonalityTypes.InstanceFor(bot)))
            .Where(pair => pair.Personality?.TypeKey != null)
            .ToList();
        // The partner must be among the first few, so the coordinator's best-fit order still counts.
        int partnerIndex = typed.FindIndex(pair => responders.Take(MaxPartnerLook).Contains(pair.Bot));
        if (partnerIndex < 0)
            return false;
        var (partner, partnerPersonality) = typed[partnerIndex];
        if (Random.Shared.NextDouble() >= ContinueChance(starterPersonality.Chattiness, partnerPersonality.Chattiness, volume))
            return false;
        var bots = new List<GameBot> { starter, partner };
        var voices = new List<SceneVoice> { Voice(starter, starterType), Voice(partner, partnerPersonality.TypeKey) };
        int thirdIndex = typed.FindIndex(pair => pair.Bot != partner);
        if (thirdIndex >= 0)
        {
            bots.Add(typed[thirdIndex].Bot);
            voices.Add(Voice(typed[thirdIndex].Bot, typed[thirdIndex].Personality.TypeKey));
        }
        List<SceneLine> lines = PlanTopicScene(PickSize(Random.Shared), opened, voices, PersoperProperties.TONE_DOWN, Random.Shared);
        if (lines.Count == 0)
            return false;
        return Play(lines.Select(line => (bots[line.Speaker], line.Text)).ToList(), channel, group, firstDelayMs: 1_700 + Random.Shared.Next(1_300));
    }

    /// <summary>
    /// Called by PersonalityChatter when it's this typed bot's turn: with the pair-scene
    /// chance, and a rival type that can hear it, a scripted pair scene plays instead of a
    /// single line. True when the scene's first line was said.
    /// </summary>
    public static bool TryPairScene(GameBot bot, BotPersonality personality, eSceneChannel channel)
    {
        if (personality?.TypeKey is not { } type || PersoperProperties.CHATTER_PERCENT <= 0 ||
            PairScenes.RivalsOf(type) is not { Count: > 0 } rivals || Random.Shared.NextDouble() >= PairSceneChance)
            return false;
        var audience = PersonalityTypes.Online()
            .Where(pair => pair.Bot != bot && pair.Bot.Realm == bot.Realm && AutonomousBotChatCoordinator.CanSpeakInScene(pair.Bot) &&
                InSameAudience(bot, pair.Bot, channel))
            .Take(MaxCandidates)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        var rival = audience.FirstOrDefault(pair => rivals.Contains(pair.Personality.TypeKey));
        if (rival.Bot == null || PairScenes.For(type, rival.Personality.TypeKey) is not { } typePair)
            return false;
        GameBot third = audience.FirstOrDefault(other => other.Bot != rival.Bot).Bot;
        SceneScript script = PickScript(typePair.Scenes, PickSize(Random.Shared), third != null, PersoperProperties.TONE_DOWN, Random.Shared);
        bool leads = typePair.Lead == type;
        GameBot[] bots = [leads ? bot : rival.Bot, leads ? rival.Bot : bot, third];
        List<SceneLine> lines = FillScript(script, bots.Select(member => member?.Name).ToArray(),
            PersonalityReactions.SlotsFor(bots[0], bots[0].Level < bots[0].MaxLevel ? bots[0].Level + 1 : 0));
        if (lines == null || lines.Count == 0)
            return false;
        return Play(lines.Select(line => (bots[line.Speaker], line.Text)).ToList(), channel, bot.Group, firstDelayMs: 0);
    }

    private static SceneVoice Voice(GameBot bot, string typeKey) =>
        new(bot.Name, typeKey, PersonalityReactions.SlotsFor(bot, bot.Level < bot.MaxLevel ? bot.Level + 1 : 0));

    private static bool InSameAudience(GameBot bot, GameBot other, eSceneChannel channel) => channel switch
    {
        eSceneChannel.Faction => true,
        eSceneChannel.Group => other.Group != null && other.Group == bot.Group,
        _ => other.CurrentRegion == bot.CurrentRegion && other.IsWithinRadius(bot, WorldMgr.SAY_DISTANCE),
    };

    private static object KeyFor(GameBot bot, eSceneChannel channel, Group group) => channel switch
    {
        eSceneChannel.Faction => FactionKey(bot.Realm),
        eSceneChannel.Group => group,
        _ => NearbyKey(bot.CurrentRegionID),
    };

    /// <summary>
    /// Plays the lines one after another, each after a short typing pause, holding the
    /// channel until the end. The scene stops when a speaker can't talk any more. With
    /// firstDelayMs 0 the first line is said at once; false if it couldn't be.
    /// </summary>
    private static bool Play(List<(GameBot Speaker, string Line)> lines, eSceneChannel channel, Group group, int firstDelayMs)
    {
        if (lines.Count == 0 || lines.Count > MaxSceneLines)
            return false;
        if (channel == eSceneChannel.Group && group == null)
            return false;
        object key = KeyFor(lines[0].Speaker, channel, group);
        // The longest the scene can take (each pause plus its full random part), plus a little.
        int total = firstDelayMs + lines.Skip(1).Sum(line => BaseDelayMs(line.Line) + MaxDelayJitterMs) + 2_000;
        int index = 0;
        if (firstDelayMs <= 0)
        {
            if (!AutonomousBotChatCoordinator.SpeakSceneLine(lines[0].Speaker, lines[0].Line, channel, group))
                return false;
            index = 1;
        }
        Hold(key, total);
        if (index < lines.Count)
            Next(lines, index, channel, group, key, index == 0 ? firstDelayMs : LineDelayMs(lines[index].Line, Random.Shared));
        return true;
    }

    /// <summary>Says line [index] after the delay, then schedules the next one. At most MaxSceneLines steps.</summary>
    private static void Next(List<(GameBot Speaker, string Line)> lines, int index, eSceneChannel channel, Group group, object key, int delayMs)
    {
        GameBot speaker = lines[index].Speaker;
        _ = new ECSGameTimer(speaker, _ =>
        {
            if (!AutonomousBotChatCoordinator.SpeakSceneLine(speaker, lines[index].Line, channel, group))
            {
                Release(key);
                return 0;
            }
            if (index + 1 < lines.Count && index + 1 < MaxSceneLines)
                Next(lines, index + 1, channel, group, key, LineDelayMs(lines[index + 1].Line, Random.Shared));
            else
                Release(key);
            return 0;
        }, delayMs);
    }

    private static void Hold(object key, int ms)
    {
        long now = GameLoop.GameLoopTime;
        lock (Sync)
        {
            // Old holds are forgotten once they've passed; the table stays small.
            if (HeldUntil.Count >= MaxHeldEntries)
            {
                foreach (object old in HeldUntil.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                    HeldUntil.Remove(old);
            }
            HeldUntil[key] = now + ms;
        }
    }

    private static void Release(object key)
    {
        lock (Sync)
            HeldUntil.Remove(key);
    }
}
