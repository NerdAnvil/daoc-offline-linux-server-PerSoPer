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
/// <summary>One speaker of a scene; with a MemoryKey it skips lines it said lately, in earlier scenes too (DialoguePicker).</summary>
public sealed record SceneVoice(string Name, string TypeKey, LineSlots Slots, string MemoryKey = null);

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
    /// <summary>Chance a named bot whose turn it is plays one of its character's own scenes ("shempenstein-any").</summary>
    public const double CharacterSceneChance = 0.05;
    /// <summary>Character scenes remembered as played (character and partner) at most; a full store is emptied.</summary>
    public const int MaxCharacterScenesRemembered = 1000;
    /// <summary>The same partner waits this many times persoper_character_scene_minutes for the next one.</summary>
    public const int PartnerRepeatFactor = 4;
    /// <summary>A crowd: at least this many bots (the character included), at most MaxCrowd.</summary>
    public const int MinCrowd = 4;
    public const int MaxCrowd = 20;
    /// <summary>How long a crowd's pal, buddy, guy chain runs, and its lines at most; the laughs follow.</summary>
    public const int CrowdChainMs = 36_000;
    /// <summary>After this many chain lines, someone asks what is going on (a "~ crowd confused" line).</summary>
    public const int ConfusedAfter = 5;
    public const int MaxCrowdChain = 30;
    /// <summary>The chain: the known bit, word for word (owner's choice). {0} and {1} cycle pal, buddy, guy.</summary>
    public const string CrowdChainLine = "I'm not your {0}, {1}.";
    private static readonly string[] CrowdWords = ["pal", "buddy", "guy"];
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
                    voice.Slots, other, used, random, voice.MemoryKey),
                eStep.Comeback => PickFresh(SceneLines.All.GetValueOrDefault(voice.TypeKey ?? string.Empty)?.Comebacks.GetValueOrDefault(topic),
                    voice.Slots, other, used, random, voice.MemoryKey),
                _ => PickFresh(CloserPool(voice.TypeKey, toneDown, random), voice.Slots, other, used, random, voice.MemoryKey),
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

    /// <summary>
    /// A filled line from the pool that hasn't been said in this scene yet, or null. With a botKey, of those, one the bot
    /// hasn't said lately (so "Exit, stage left." doesn't close two scenes in a row), and it is remembered.
    /// </summary>
    private static string PickFresh(IReadOnlyList<string> pool, LineSlots slots, string otherName, HashSet<string> used, Random random,
        string botKey = null)
    {
        if (pool == null || pool.Count == 0)
            return null;
        if (botKey != null)
        {
            List<string> open = pool.Take(DialoguePicker.MaxPool)
                .Where(template => PersonalityReactions.Fill(template, slots, otherName) is { } filled && !used.Contains(filled)).ToList();
            if (DialoguePicker.Shared.Fresh(open, botKey, random) is not { } picked)
                return null;
            DialoguePicker.Shared.Said(botKey, picked);
            string line = PersonalityReactions.Fill(picked, slots, otherName);
            used.Add(line);
            return line;
        }
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
    /// <summary>The bots in each held channel's scene: everyone else is hushed there until it ends.</summary>
    private static readonly Dictionary<object, HashSet<GameBot>> HeldCast = new();

    /// <summary>True while a scene holds this channel and the bot isn't in it: its other chatter waits.</summary>
    public static bool Hushes(object key, GameBot bot)
    {
        long now = GameLoop.GameLoopTime;
        lock (Sync)
            return HeldUntil.TryGetValue(key, out long until) && until > now &&
                HeldCast.TryGetValue(key, out HashSet<GameBot> cast) && !cast.Contains(bot);
    }

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
        if (TryCharacterScene(bot, channel))
            return true;
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

    /// <summary>When (GameLoopTime) each character last played one of its own scenes, and with which partner.</summary>
    private static readonly Dictionary<string, long> CharacterSceneAt = new();
    private static readonly Dictionary<(string Character, string Partner), long> CharacterPartnerAt = new();

    /// <summary>
    /// Now and then a named bot plays one of its character's own scenes with another bot in the
    /// audience (A the named bot, B the partner, C a third if there is one): at most once per
    /// persoper_character_scene_minutes, and not with the same partner again within four times
    /// that. Sometimes (persoper_crowd_scene_percent) it turns into a crowd.
    /// </summary>
    private static bool TryCharacterScene(GameBot bot, eSceneChannel channel)
    {
        int minutes = PersoperProperties.CHARACTER_SCENE_MINUTES;
        if (minutes <= 0 || PersoperProperties.CHATTER_PERCENT <= 0 || NamedBots.EntryFor(bot.DatabaseID) is not { } entry ||
            PairScenes.ForCharacter(PersonalityLines.CharacterKey(entry)) is not { } pair)
            return false;
        long now = GameLoop.GameLoopTime;
        long gapMs = minutes * 60_000L;
        lock (Sync)
            if (CharacterSceneAt.TryGetValue(pair.Lead, out long last) && now - last < gapMs)
                return false;
        if (Random.Shared.NextDouble() >= CharacterSceneChance)
            return false;
        var audience = PersonalityTypes.Online()
            .Where(other => other.Bot != bot && other.Bot.Realm == bot.Realm && AutonomousBotChatCoordinator.CanSpeakInScene(other.Bot) &&
                InSameAudience(bot, other.Bot, channel))
            .Take(MaxCandidates)
            .OrderBy(_ => Random.Shared.Next())
            .Select(other => other.Bot)
            .ToList();
        GameBot partner;
        lock (Sync)
            partner = audience.FirstOrDefault(other =>
                !CharacterPartnerAt.TryGetValue((pair.Lead, other.InternalID), out long at) || now - at >= PartnerRepeatFactor * gapMs);
        if (partner == null)
            return false;
        audience.Remove(partner);
        audience.Insert(0, partner);
        bool crowd = pair.HasCrowd && audience.Count + 1 >= MinCrowd && Random.Shared.Next(100) < PersoperProperties.CROWD_SCENE_PERCENT;
        if (!PlayCharacterScene(bot, pair, null, audience, channel, crowd))
            return false;
        lock (Sync)
        {
            if (CharacterPartnerAt.Count >= MaxCharacterScenesRemembered)
                CharacterPartnerAt.Clear();
            CharacterSceneAt[pair.Lead] = now;
            CharacterPartnerAt[(pair.Lead, partner.InternalID)] = now;
        }
        return true;
    }

    /// <summary>
    /// Plays a character scene (by name, or a random one that fits) with others[0] as B and
    /// others[1] as C; with crowd, the scene's opener starts a crowd of up to MaxCrowd bots instead.
    /// </summary>
    private static bool PlayCharacterScene(GameBot lead, TypePair pair, SceneScript script, IReadOnlyList<GameBot> others, eSceneChannel channel, bool crowd)
    {
        GameBot partner = others.ElementAtOrDefault(0), third = others.ElementAtOrDefault(1);
        if (partner == null)
            return false;
        script ??= PickScript(pair.Scenes, PickSize(Random.Shared), third != null, PersoperProperties.TONE_DOWN, Random.Shared) ??
            PickScript(pair.Scenes, eSceneSize.Large, third != null, PersoperProperties.TONE_DOWN, Random.Shared);
        GameBot[] bots = [lead, partner, third];
        List<SceneLine> lines = script == null ? null : FillScript(script, bots.Select(member => member?.Name).ToArray(),
            PersonalityReactions.SlotsFor(lead, lead.Level < lead.MaxLevel ? lead.Level + 1 : 0));
        if (lines == null || lines.Count == 0)
            return false;
        if (!crowd)
            return Play(lines.Select(line => (bots[line.Speaker], line.Text)).ToList(), channel, lead.Group, firstDelayMs: 0);
        GameBot[] cast = [lead, .. others.Take(MaxCrowd - 1)];
        List<(int Speaker, string Line, int DelayMs)> plan = PlanCrowd(lines[0].Text, cast.Length, pair.CrowdLaughs, pair.CrowdGrumbles, lead.Name, Random.Shared, pair.CrowdConfused);
        return PlayTimed(plan.Select(step => (cast[step.Speaker], step.Line, step.DelayMs)).ToList(), channel, lead.Group, IsChainLine);
    }

    /// <summary>
    /// A crowd of size bots (0 the character, 1 its partner): the opener, then the pal, buddy, guy
    /// chain for about CrowdChainMs with everyone piling in, then each one laughs, or for a few,
    /// grumbles. Steps are (speaker, line, wait before it); the first wait is 0. About a minute.
    /// </summary>
    public static List<(int Speaker, string Line, int DelayMs)> PlanCrowd(string opener, int size, IReadOnlyList<string> laughs,
        IReadOnlyList<string> grumbles, string leadName, Random random, IReadOnlyList<string> confused = null)
    {
        random ??= Random.Shared;
        size = Math.Clamp(size, 2, MaxCrowd);
        var plan = new List<(int Speaker, string Line, int DelayMs)> { (0, opener, 0) };
        var spoken = new HashSet<int> { 0 };
        int previous = 0, elapsed = 0;
        for (int step = 0; step < MaxCrowdChain && elapsed < CrowdChainMs; step++)
        {
            // B answers the opener and A answers B, as in the bit; then whoever hasn't spoken yet, mostly.
            int speaker = step switch
            {
                0 => 1,
                1 => 0,
                _ => NextCrowdSpeaker(size, previous, spoken, random),
            };
            // A beat before the chain starts, then a line every 2.5 to 3.5 s.
            int delay = step == 0 ? 3_500 + random.Next(1_000) : 2_500 + random.Next(1_000);
            plan.Add((speaker, ChainLine(step), delay));
            spoken.Add(speaker);
            previous = speaker;
            elapsed += delay;
            // After a few chain lines someone who hasn't joined in yet asks what is going on.
            if (step + 1 == ConfusedAfter && confused is { Count: > 0 } &&
                Enumerable.Range(0, size).Where(other => !spoken.Contains(other)).ToArray() is { Length: > 0 } quiet)
            {
                int asker = quiet[random.Next(quiet.Length)];
                int wait = 1_000 + random.Next(1_000);
                plan.Add((asker, confused[random.Next(confused.Count)].Replace("{a}", leadName), wait));
                spoken.Add(asker);
                previous = asker;
                elapsed += wait;
            }
        }
        // Most laugh, a few grumble (one in five, at least one in a crowd of five or more); the character laughs last.
        // Each pool is shuffled and gone through in order, so a line only repeats once all have been said.
        int grumblers = size >= 5 ? Math.Max(1, (size - 1) / 5) : 0;
        int[] enders = Enumerable.Range(1, size - 1).OrderBy(_ => random.Next()).ToArray();
        string[] laughOrder = laughs?.OrderBy(_ => random.Next()).ToArray() ?? [];
        string[] grumbleOrder = grumbles?.OrderBy(_ => random.Next()).ToArray() ?? [];
        int laughed = 0;
        for (int i = 0; i < enders.Length; i++)
        {
            string line = i < grumblers && grumbleOrder.Length > 0 ? grumbleOrder[i % grumbleOrder.Length]
                : laughOrder.Length > 0 ? laughOrder[laughed++ % laughOrder.Length] : null;
            if (line != null)
                plan.Add((enders[i], line.Replace("{a}", leadName), 800 + random.Next(1_200)));
        }
        // The character's own laugh: not one that names it ({a}).
        string[] ownLaughs = laughOrder.Where(line => !line.Contains("{a}")).ToArray();
        if (ownLaughs.Length > 0)
            plan.Add((0, ownLaughs[laughed % ownLaughs.Length], 1_000 + random.Next(1_000)));
        return plan;
    }

    /// <summary>True for a line of the pal, buddy, guy chain (CrowdChainLine with any of the words).</summary>
    public static bool IsChainLine(string line) =>
        CrowdWords.Any(first => CrowdWords.Any(second => line == string.Format(CrowdChainLine, first, second)));

    /// <summary>The chain's line after this many chain lines were said: pal, buddy; buddy, guy; guy, pal; ...</summary>
    public static string ChainLine(int said) => string.Format(CrowdChainLine, CrowdWords[said % 3], CrowdWords[(said + 1) % 3]);

    private static int NextCrowdSpeaker(int size, int previous, HashSet<int> spoken, Random random)
    {
        int[] fresh = Enumerable.Range(0, size).Where(speaker => speaker != previous && !spoken.Contains(speaker)).ToArray();
        if (fresh.Length > 0 && random.Next(4) > 0)
            return fresh[random.Next(fresh.Length)];
        int other = random.Next(size - 1);
        return other >= previous ? other + 1 : other;
    }

    /// <summary>
    /// For "/dialogue scene": plays one pair scene by name now, in the realm's faction chat, with
    /// online bots of that realm (of the pair's types when there are some, else any that can talk).
    /// Returns what happened, for chat.
    /// </summary>
    public static string PlayNamedScene(eRealm realm, string pairName, string sceneName)
    {
        if (PairScenes.Find(pairName) is not { } pair)
            return $"No pair '{pairName}'. Pairs: {string.Join(", ", PairScenes.All.Select(found => found.Name))}.";
        bool crowd = pair.Rival == PairScenes.AnyRival && string.Equals(sceneName, PairScenes.CrowdSection, StringComparison.OrdinalIgnoreCase);
        if (crowd && !pair.HasCrowd)
            return $"{pair.Name} has no crowd lines ('~ crowd laugh' and '~ crowd grumble').";
        SceneScript script = crowd ? PickScript(pair.Scenes, eSceneSize.Large, false, PersoperProperties.TONE_DOWN, Random.Shared)
            : pair.Scenes.FirstOrDefault(scene => string.Equals(scene.Name, sceneName, StringComparison.OrdinalIgnoreCase));
        if (script == null)
            return $"No scene '{sceneName}' in {pair.Name}. Scenes: {string.Join(", ", pair.Scenes.Select(scene => scene.Name))}.";
        if (script.Cheeky && PersoperProperties.TONE_DOWN)
            return $"'{script.Name}' is a cheeky scene; it only plays with persoper_tone_down off.";

        var cast = PersonalityTypes.Online()
            .Where(found => found.Bot.Realm == realm && AutonomousBotChatCoordinator.CanSpeakInScene(found.Bot))
            .Take(MaxCandidates)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        // A character's own scene ("shempenstein-any") needs that character's bot of this realm as A.
        bool character = pair.Rival == PairScenes.AnyRival;
        // Looked for among all online bots, not only the first few that can talk.
        GameBot a = character
            ? PersonalityTypes.Online().Select(found => found.Bot).FirstOrDefault(bot => bot.Realm == realm &&
                NamedBots.EntryFor(bot.DatabaseID) is { } entry && PersonalityLines.CharacterKey(entry) == pair.Lead)
            : cast.FirstOrDefault(found => found.Personality.TypeKey == pair.Lead).Bot ?? cast.FirstOrDefault().Bot;
        if (character && a == null)
            return $"{pair.Lead} of your realm isn't online.";
        if (character && !AutonomousBotChatCoordinator.CanSpeakInScene(a))
            return $"{a.Name} can't talk now ({(a.InCombat ? "fighting" : a.IsCasting ? "casting" : a.IsOnHorse || a.IsOnStableMasterRoute ? "riding" : "busy")}); try again in a moment.";
        if (character)
        {
            // Not counted against the character's timing: a check, not the real thing.
            List<GameBot> others = cast.Where(found => found.Bot != a).Select(found => found.Bot).ToList();
            if (others.Count < (crowd ? MinCrowd - 1 : script.Speakers - 1))
                return "Not enough bots of your realm can talk now.";
            return PlayCharacterScene(a, pair, script, others, eSceneChannel.Faction, crowd)
                ? $"Playing {pair.Name} '{(crowd ? "crowd" : script.Name)}' in faction chat: A = {a.Name}" +
                    (crowd ? $", {Math.Min(others.Count + 1, MaxCrowd)} bots." : $", B = {others[0].Name}.")
                : $"'{script.Name}' couldn't start.";
        }
        GameBot b = cast.FirstOrDefault(found => found.Bot != a && found.Personality.TypeKey == pair.Rival).Bot ??
            cast.FirstOrDefault(found => found.Bot != a).Bot;
        GameBot c = cast.FirstOrDefault(found => found.Bot != a && found.Bot != b).Bot;
        if (a == null || b == null || script.Speakers > 2 && c == null)
            return $"Not enough bots of your realm can talk now ('{script.Name}' needs {script.Speakers}).";

        GameBot[] bots = [a, b, c];
        List<SceneLine> lines = FillScript(script, bots.Select(member => member?.Name).ToArray(),
            PersonalityReactions.SlotsFor(a, a.Level < a.MaxLevel ? a.Level + 1 : 0));
        if (lines == null)
            return $"'{script.Name}' can't be filled in for {a.Name} now (a {{zone}} or other slot isn't known).";
        if (!Play(lines.Select(line => (bots[line.Speaker], line.Text)).ToList(), eSceneChannel.Faction, null, firstDelayMs: 0))
            return $"'{script.Name}' couldn't start.";
        return $"Playing {pair.Name} '{script.Name}' in faction chat: A = {a.Name}, B = {b.Name}" +
            (script.Speakers > 2 ? $", C = {c.Name}." : ".");
    }

    private static SceneVoice Voice(GameBot bot, string typeKey) =>
        new(bot.Name, typeKey, PersonalityReactions.SlotsFor(bot, bot.Level < bot.MaxLevel ? bot.Level + 1 : 0),
            PersonalityReactions.MemoryKey(bot, BotPersonalities.For(bot)));

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
        Hold(key, total, lines.Select(line => line.Speaker));
        if (index < lines.Count)
            Next(lines, index, channel, group, key, index == 0 ? firstDelayMs : LineDelayMs(lines[index].Line, Random.Shared));
        return true;
    }

    /// <summary>
    /// Plays a crowd: each line after its own wait, holding the channel to the end. A bot that
    /// can't talk just misses its line (a chain line goes to up to MaxStandIns others instead), and so
    /// does one whose line would follow its own; the crowd goes on. Lines isChain picks out are worked out
    /// when said (ChainLine), so a missed one doesn't break the pal, buddy, guy order.
    /// False when the first line can't be said.
    /// </summary>
    private static bool PlayTimed(List<(GameBot Speaker, string Line, int DelayMs)> steps, eSceneChannel channel, Group group, Func<string, bool> isChain)
    {
        if (steps.Count == 0 || steps.Count > MaxTimedLines || channel == eSceneChannel.Group && group == null)
            return false;
        if (!AutonomousBotChatCoordinator.SpeakSceneLine(steps[0].Speaker, steps[0].Line, channel, group))
            return false;
        object key = KeyFor(steps[0].Speaker, channel, group);
        Hold(key, steps.Sum(step => step.DelayMs) + 2_000, steps.Select(step => step.Speaker));
        int at = 0;
        int[] chainSaid = [0];
        GameBot[] cast = steps.Select(step => step.Speaker).Where(bot => bot != null).Distinct().ToArray();
        // Who said the last line: a bot whose turn follows its own line (the one between missed) skips it.
        GameBot[] lastSpeaker = [steps[0].Speaker];
        for (int i = 1; i < steps.Count; i++)
        {
            (GameBot speaker, string line, int delay) = steps[i];
            at += delay;
            bool last = i == steps.Count - 1;
            bool chain = isChain(line);
            _ = new ECSGameTimer(speaker, _ =>
            {
                string said = chain ? ChainLine(chainSaid[0]) : line;
                // A busy bot's chain line goes to someone else in the crowd, so the chain doesn't stall.
                IEnumerable<GameBot> tries = chain
                    ? cast.Where(other => other != speaker).OrderBy(_ => Random.Shared.Next()).Take(MaxStandIns).Prepend(speaker)
                    : [speaker];
                foreach (GameBot candidate in tries)
                {
                    if (candidate == lastSpeaker[0] || !AutonomousBotChatCoordinator.SpeakSceneLine(candidate, said, channel, group))
                        continue;
                    lastSpeaker[0] = candidate;
                    if (chain)
                        chainSaid[0]++;
                    break;
                }
                if (last)
                    Release(key);
                return 0;
            }, at);
        }
        return true;
    }

    /// <summary>Others who try a busy bot's chain line in its place, at most.</summary>
    private const int MaxStandIns = 3;

    /// <summary>Lines a timed scene (a crowd) may have: its chain and everyone's ending.</summary>
    private const int MaxTimedLines = 2 + MaxCrowdChain + MaxCrowd;

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

    private static void Hold(object key, int ms, IEnumerable<GameBot> cast)
    {
        var speakers = new HashSet<GameBot>(cast.Where(bot => bot != null).Take(MaxTimedLines));
        long now = GameLoop.GameLoopTime;
        lock (Sync)
        {
            // Old holds are forgotten once they've passed; the table stays small.
            if (HeldUntil.Count >= MaxHeldEntries)
            {
                foreach (object old in HeldUntil.Where(pair => pair.Value <= now).Select(pair => pair.Key).ToArray())
                {
                    HeldUntil.Remove(old);
                    HeldCast.Remove(old);
                }
            }
            HeldUntil[key] = now + ms;
            HeldCast[key] = speakers;
        }
    }

    private static void Release(object key)
    {
        lock (Sync)
        {
            HeldUntil.Remove(key);
            HeldCast.Remove(key);
        }
    }
}
