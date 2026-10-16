using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.PacketHandler;

namespace DOL.GS;

/// <summary>
/// Step C: small things the personality types do, not just say. Leeroy's battle cry and his
/// "full Leeroy" charge, the Loner turning invites down, the Casual's short breaks, the
/// Hardcore's gripes about sitting, the Min/maxer's build advice, the Joker's emotes, and every
/// bot answering emotes and reacting to deaths with one. Each has a chance and a cooldown.
/// Rules are pure and unit tested; the running-server part below only finds who and when.
/// </summary>
public static class PersonalityBehaviours
{
    public const int BattleCryCooldownMs = 90_000;
    public const int FullLeeroyRollMs = 60_000;
    public const int FullLeeroyRange = 1_500;
    /// <summary>He's reckless, not suicidal: nothing more than this many levels above him.</summary>
    public const int FullLeeroyMaxLevelsAbove = 3;
    public const int MaxNpcsLooked = 20;
    public const int EmoteRange = 512;
    public const int EmoteReplyCooldownMs = 6_000;
    public const int DeathEmoteRegionCooldownMs = 6_000;
    public const int MaxDeathEmoters = 2;
    public const int SitComplaintAfterMs = 45_000;
    public const int InspectRange = 350;
    public const int InspectMemoryMs = 30 * 60_000;
    /// <summary>A Min/maxer gives build advice at most once every 30-45 minutes.</summary>
    public const int InspectCooldownMinMinutes = 30;
    public const int InspectCooldownMaxMinutes = 45;
    public const int MaxInspectMemory = 20;
    /// <summary>Most behaviours started per tick across the whole server.</summary>
    public const int MaxActionsPerTick = 6;

    // --- Rules (pure) ---

    /// <summary>True when a percentage roll (0-100) succeeds.</summary>
    public static bool Rolls(int percent, Random random) =>
        percent > 0 && (random ?? Random.Shared).Next(100) < Math.Clamp(percent, 0, 100);

    /// <summary>Chance (0-1) a Loner turns an invite down: less for someone it knows or likes.</summary>
    public static double LonerDeclineChance(int meetCount, PersonalityMemory.eMood mood) =>
        mood == PersonalityMemory.eMood.Warm ? 0.2 : meetCount >= 3 ? 0.4 : 0.7;

    /// <summary>How likely (0-100) a bot answers an emote aimed at it, by type (null = plain or named).</summary>
    public static int EmoteReplyPercent(string typeKey, bool named) => typeKey switch
    {
        "joker" => 90,
        "leeroy" => 75,
        "casual" => 65,
        "hardcore" or "minmaxer" => 50,
        "loner" => 30,
        _ => named ? 70 : 50,
    };

    private static readonly Dictionary<eEmote, eEmote[]> Answers = new()
    {
        [eEmote.Wave] = [eEmote.Wave],
        [eEmote.Bow] = [eEmote.Bow, eEmote.Curtsey],
        [eEmote.Curtsey] = [eEmote.Bow, eEmote.Curtsey],
        [eEmote.Hug] = [eEmote.Hug, eEmote.Blush],
        [eEmote.BlowKiss] = [eEmote.Blush, eEmote.Laugh],
        [eEmote.Rude] = [eEmote.Rude, eEmote.Laugh, eEmote.Shrug],
        [eEmote.Slap] = [eEmote.Angry, eEmote.Slap],
        [eEmote.Taunt] = [eEmote.Taunt, eEmote.Laugh, eEmote.Brandish],
        [eEmote.Cheer] = [eEmote.Cheer],
        [eEmote.Clap] = [eEmote.Bow],
        [eEmote.Dance] = [eEmote.Dance, eEmote.Clap],
        [eEmote.Laugh] = [eEmote.Laugh, eEmote.Shrug],
        [eEmote.Rofl] = [eEmote.Laugh],
        [eEmote.Point] = [eEmote.Shrug, eEmote.Point],
        [eEmote.Flex] = [eEmote.Flex, eEmote.Laugh],
        [eEmote.Salute] = [eEmote.Salute],
        [eEmote.Military] = [eEmote.Salute],
        [eEmote.Beckon] = [eEmote.Yes, eEmote.Shrug],
        [eEmote.Beg] = [eEmote.No, eEmote.Shrug],
        [eEmote.Cry] = [eEmote.Hug],
        [eEmote.Smile] = [eEmote.Smile],
        [eEmote.Angry] = [eEmote.Cower, eEmote.Shrug],
        [eEmote.Victory] = [eEmote.Cheer, eEmote.Clap],
        [eEmote.Yes] = [eEmote.Yes],
        [eEmote.No] = [eEmote.Shrug],
        [eEmote.Shrug] = [eEmote.Shrug],
        [eEmote.Present] = [eEmote.Bow],
        [eEmote.Ponder] = [eEmote.Ponder],
        [eEmote.Drink] = [eEmote.Drink, eEmote.Cheer],
        [eEmote.Doh] = [eEmote.Laugh],
        [eEmote.Yawn] = [eEmote.Yawn],
        [eEmote.Cower] = [eEmote.Laugh, eEmote.Point],
        [eEmote.Surrender] = [eEmote.Laugh, eEmote.Victory],
        [eEmote.Slit] = [eEmote.Angry, eEmote.Brandish],
    };

    /// <summary>Each type's own favourites, used now and then instead of the usual answer.</summary>
    private static readonly Dictionary<string, eEmote[]> Favourites = new()
    {
        ["joker"] = [eEmote.Rofl, eEmote.Dance, eEmote.Point],
        ["loner"] = [eEmote.Shrug, eEmote.No],
        ["hardcore"] = [eEmote.Brandish, eEmote.Military],
        ["leeroy"] = [eEmote.Roar, eEmote.Cheer, eEmote.Flex],
        ["minmaxer"] = [eEmote.Ponder, eEmote.Doh],
        ["casual"] = [eEmote.Smile, eEmote.Wave],
    };

    /// <summary>The emote a bot answers with (by its type, if any).</summary>
    public static eEmote EmoteReply(eEmote incoming, string typeKey, Random random)
    {
        random ??= Random.Shared;
        if (typeKey != null && Favourites.TryGetValue(typeKey, out eEmote[] own) && random.NextDouble() < 0.3)
            return own[random.Next(own.Length)];
        eEmote[] pool = Answers.GetValueOrDefault(incoming) ?? [eEmote.Shrug, eEmote.Smile, eEmote.Ponder];
        return pool[random.Next(pool.Length)];
    }

    /// <summary>The emote a bot makes when someone near it dies: grief for friends, mockery for enemies.</summary>
    public static eEmote DeathEmote(string typeKey, bool enemy, Random random)
    {
        random ??= Random.Shared;
        eEmote[] pool = (typeKey, enemy) switch
        {
            ("joker", true) => [eEmote.Rofl, eEmote.Point, eEmote.Dance],
            ("joker", false) => [eEmote.Cry, eEmote.Point, eEmote.Laugh],
            ("hardcore", true) => [eEmote.Victory, eEmote.Brandish, eEmote.Military],
            ("hardcore", false) => [eEmote.Doh, eEmote.Shrug],
            ("leeroy", true) => [eEmote.Roar, eEmote.Cheer, eEmote.Victory],
            ("leeroy", false) => [eEmote.Cry, eEmote.Roar],
            ("loner", _) => [eEmote.Shrug, eEmote.Ponder],
            ("minmaxer", true) => [eEmote.Point, eEmote.Laugh],
            ("minmaxer", false) => [eEmote.Doh, eEmote.Ponder],
            ("casual", true) => [eEmote.Laugh, eEmote.Cheer],
            ("casual", false) => [eEmote.Cry],
            (_, true) => [eEmote.Victory, eEmote.Laugh, eEmote.Cheer],
            _ => [eEmote.Cry, eEmote.Shrug, eEmote.Doh],
        };
        return pool[random.Next(pool.Length)];
    }

    /// <summary>A real build for a class from the bots' own spec templates, e.g. "47 Darkness / 26 Summoning / 5 Suppression".</summary>
    public static string SpecSummary(eCharacterClass characterClass)
    {
        BotSpec spec;
        try
        {
            spec = BotSpec.GetSpec(characterClass);
        }
        catch (Exception)
        {
            return null;
        }
        var lines = spec?.SpecLines?.Where(line => line.SpecCap > 1 && !string.IsNullOrWhiteSpace(line.Spec))
            .OrderByDescending(line => line.SpecCap).Take(4).Select(line => $"{line.SpecCap} {line.Spec}").ToList();
        return lines is { Count: > 0 } ? string.Join(" / ", lines) : null;
    }

    /// <summary>A Min/maxer's advice line, or null when the class has no known build.</summary>
    public static string InspectLine(string name, string className, string spec, Random random)
    {
        if (string.IsNullOrWhiteSpace(spec) || string.IsNullOrWhiteSpace(className))
            return null;
        random ??= Random.Shared;
        string line = BehaviourLines.MinMaxerInspects[random.Next(BehaviourLines.MinMaxerInspects.Length)]
            .Replace("{spec}", spec, StringComparison.Ordinal);
        return PersonalityReactions.Fill(line, new LineSlots(null, null, 0, className, null), name);
    }

    // What others read when a bot emotes: (verb, word before the target; empty = target follows the verb).
    private static readonly Dictionary<eEmote, (string Verb, string Prep)> Texts = new()
    {
        [eEmote.Wave] = ("waves", "at"), [eEmote.Bow] = ("bows", "to"), [eEmote.Curtsey] = ("curtseys", "to"),
        [eEmote.Hug] = ("hugs", ""), [eEmote.Blush] = ("blushes", "at"), [eEmote.Laugh] = ("laughs", "at"),
        [eEmote.Rofl] = ("rolls on the floor laughing", "at"), [eEmote.Point] = ("points", "at"),
        [eEmote.Shrug] = ("shrugs", "at"), [eEmote.Cheer] = ("cheers", "for"), [eEmote.Clap] = ("claps", "for"),
        [eEmote.Dance] = ("dances", "with"), [eEmote.Flex] = ("flexes", "at"), [eEmote.Salute] = ("salutes", ""),
        [eEmote.Military] = ("snaps a salute", "to"), [eEmote.Smile] = ("smiles", "at"), [eEmote.Angry] = ("glares", "at"),
        [eEmote.Slap] = ("slaps", ""), [eEmote.Taunt] = ("taunts", ""), [eEmote.Brandish] = ("brandishes a weapon", "at"),
        [eEmote.Cry] = ("cries", "over"), [eEmote.Ponder] = ("looks", "over"), [eEmote.Doh] = ("facepalms", "at"),
        [eEmote.Yes] = ("nods", "at"), [eEmote.No] = ("shakes their head", "at"), [eEmote.Cower] = ("cowers", "before"),
        [eEmote.Victory] = ("celebrates", "over"), [eEmote.Drink] = ("raises a drink", "to"), [eEmote.Yawn] = ("yawns", "at"),
        [eEmote.Rude] = ("makes a rude gesture", "at"), [eEmote.Roar] = ("roars", "at"), [eEmote.Loco] = ("goes loco", "at"),
    };

    /// <summary>What others read: "Bob waves at you." / "Bob waves at Ann." / "Bob waves."</summary>
    public static string EmoteText(string botName, eEmote emote, string targetName)
    {
        (string verb, string prep) = Texts.GetValueOrDefault(emote, ("gestures", "at"));
        if (string.IsNullOrWhiteSpace(targetName))
            return $"{botName} {verb}.";
        return prep.Length == 0 ? $"{botName} {verb} {targetName}." : $"{botName} {verb} {prep} {targetName}.";
    }

    // --- In the running server ---

    private const int TickMilliseconds = 5_000;
    private const string LastFightKey = "persoper.lastFightTarget";
    private const string NextBattleCryKey = "persoper.nextBattleCry";
    private const string NextLeeroyRollKey = "persoper.nextFullLeeroy";
    private const string NextEmoteReplyKey = "persoper.nextEmoteReply";
    private const string NextJokerEmoteKey = "persoper.nextJokerEmote";
    private const string BreakUntilKey = "persoper.breakUntil";
    private const string NextBreakKey = "persoper.nextBreak";
    private const string SitSinceKey = "persoper.sitSince";
    private const string NextGripeKey = "persoper.nextGripe";
    private const string NextInspectKey = "persoper.nextInspect";
    private const string InspectedKey = "persoper.inspected";

    private static readonly eEmote[] JokerEmotes = [eEmote.Dance, eEmote.Wave, eEmote.Point, eEmote.Laugh, eEmote.Flex, eEmote.Rofl, eEmote.Loco];
    private static readonly object Sync = new();
    private static readonly Dictionary<ushort, long> NextDeathEmote = new();
    private static Timer _timer;

    [GameServerStartedEvent]
    public static void OnServerStarted(DOLEvent e, object sender, EventArgs args)
    {
        _timer?.Dispose();
        // The timer only hands the work to the game loop; everything happens there.
        _timer = new Timer(_ => NpcService.Instance.Post<object>(_ => Tick(), null), null, TickMilliseconds, TickMilliseconds);
        GameEventMgr.AddHandlerUnique(GameLivingEvent.Dying, OnDying);
    }

    [GameServerStoppedEvent]
    public static void OnServerStopped(DOLEvent e, object sender, EventArgs args)
    {
        _timer?.Dispose();
        _timer = null;
        GameEventMgr.RemoveHandler(GameLivingEvent.Dying, OnDying);
    }

    private static bool Talks => PersoperProperties.CHATTER_PERCENT > 0;

    private static bool Idle(GameBot bot) => bot.IsAlive && bot.ObjectState == GameObject.eObjectState.Active &&
        !bot.InCombat && !bot.IsCasting && !bot.IsOnStableMasterRoute && !bot.IsOnHorse;

    private static void Tick()
    {
        long now = GameLoop.GameLoopTime;
        int actions = 0;
        foreach ((GameBot bot, BotPersonality personality) in PersonalityTypes.Online())
        {
            if (actions >= MaxActionsPerTick)
                break;
            if (!Idle(bot))
                continue;
            bool acted = personality.TypeKey switch
            {
                "leeroy" => FullLeeroy(bot, now),
                "joker" => JokerEmote(bot, now),
                "casual" => CasualBreak(bot, now),
                "hardcore" => HardcoreGripe(bot, now),
                "minmaxer" => MinMaxerInspect(bot, now),
                _ => false,
            };
            if (acted)
                actions++;
        }
    }

    // Leeroy

    /// <summary>Called by GameBot when it starts an attack: a Leeroy may yell, once per new fight.</summary>
    public static void OnStartAttack(GameBot bot, GameObject target)
    {
        if (target is not GameLiving || bot?.IsAutonomousWorldBot != true || !Talks)
            return;
        if (bot.TempProperties.GetProperty<int>(LastFightKey) == target.ObjectID)
            return;
        bot.TempProperties.SetProperty(LastFightKey, target.ObjectID);
        if (PersonalityTypes.InstanceFor(bot)?.TypeKey != "leeroy")
            return;
        long now = GameLoop.GameLoopTime;
        if (bot.TempProperties.GetProperty<long>(NextBattleCryKey) > now ||
            !Rolls(PersoperProperties.LEEROY_BATTLE_CRY_PERCENT, Random.Shared))
            return;
        bot.TempProperties.SetProperty(NextBattleCryKey, now + BattleCryCooldownMs);
        string cry = BehaviourLines.LeeroyBattleCries[Random.Shared.Next(BehaviourLines.LeeroyBattleCries.Length)];
        Later(bot, 300, () => AutonomousBotChatCoordinator.SpeakReaction(bot, cry));
    }

    /// <summary>In a group led by a player, with nothing happening, he may run at something on his own.</summary>
    private static bool FullLeeroy(GameBot bot, long now)
    {
        if (!PersoperProperties.FULL_LEEROY || !bot.IsPlayerLedGroup || bot.Group == null || bot.Brain is not BotBrain brain ||
            bot.TempProperties.GetProperty<long>(NextLeeroyRollKey) > now)
            return false;
        bot.TempProperties.SetProperty(NextLeeroyRollKey, now + FullLeeroyRollMs);
        if (bot.Group.GetMembersInTheGroup().Any(member => member.InCombat) ||
            !Rolls(PersoperProperties.FULL_LEEROY_PERCENT, Random.Shared))
            return false;
        GameNPC target = bot.GetNPCsInRadius(FullLeeroyRange).Take(MaxNpcsLooked)
            .Where(npc => npc is not GameBot && npc.IsAlive && npc.Level <= bot.Level + FullLeeroyMaxLevelsAbove && brain.CanAggroTarget(npc))
            .OrderBy(npc => bot.GetDistanceTo(npc))
            .FirstOrDefault();
        if (target == null)
            return false;
        // His own cry, so the battle cry on the attack itself stays quiet.
        bot.TempProperties.SetProperty(NextBattleCryKey, now + BattleCryCooldownMs);
        bot.TempProperties.SetProperty(LastFightKey, target.ObjectID);
        if (Talks && PersonalityReactions.PickFilled(BehaviourLines.FullLeeroyCries,
                new LineSlots(null, target.Name, 0, null, null), null, Random.Shared) is { } cry)
            AutonomousBotChatCoordinator.SpeakReaction(bot, cry);
        bot.WakeRecoveryRest();
        brain.OrderPull(target);
        return true;
    }

    // Loner

    /// <summary>
    /// A Loner usually turns an invite down (with a line for the player); someone it knows or
    /// likes gets a yes more often, with a grudging line once it has joined.
    /// </summary>
    public static bool LonerDeclines(GamePlayer player, GameBot bot, out string refusal)
    {
        refusal = null;
        if (player == null || PersonalityTypes.InstanceFor(bot) is not { TypeKey: "loner" } personality)
            return false;
        PersonalityMemoryRecord memory = PersonalityMemoryStore.Load(personality, player);
        double chance = LonerDeclineChance(memory?.MeetCount ?? 0, PersonalityMemory.MoodOf(memory));
        if (Random.Shared.NextDouble() < chance)
        {
            string line = BehaviourLines.LonerInviteDeclines[Random.Shared.Next(BehaviourLines.LonerInviteDeclines.Length)];
            refusal = $"{bot.Name} declines your invitation: \"{line.Replace("{0}", player.Name, StringComparison.Ordinal)}\"";
            return true;
        }
        if (Talks)
        {
            string accept = BehaviourLines.LonerInviteAccepts[Random.Shared.Next(BehaviourLines.LonerInviteAccepts.Length)]
                .Replace("{0}", player.Name, StringComparison.Ordinal);
            Later(bot, 1_500, () => AutonomousBotChatCoordinator.SpeakReaction(bot, accept));
        }
        return false;
    }

    // Casual

    private static bool CasualBreak(GameBot bot, long now)
    {
        // Only out on its own: never in a group, so nobody's play is held up.
        if (bot.Group != null || bot.TempProperties.GetProperty<long>(BreakUntilKey) > 0)
            return false;
        long next = bot.TempProperties.GetProperty<long>(NextBreakKey);
        if (next == 0)
        {
            bot.TempProperties.SetProperty(NextBreakKey, now + Random.Shared.Next(10, 26) * 60_000L);
            return false;
        }
        if (next > now)
            return false;
        bot.TempProperties.SetProperty(NextBreakKey, now + Random.Shared.Next(20, 41) * 60_000L);
        bot.TempProperties.SetProperty(BreakUntilKey, now + Random.Shared.Next(45, 91) * 1_000L);
        if (Talks)
            AutonomousBotChatCoordinator.SpeakReaction(bot, BehaviourLines.CasualBreakStarts[Random.Shared.Next(BehaviourLines.CasualBreakStarts.Length)]);
        if (PersoperProperties.EMOTES && Random.Shared.Next(2) == 0)
            Emote(bot, eEmote.Drink, null);
        return true;
    }

    /// <summary>
    /// Asked by the world-bot controller each turn: true while this bot is on a short break
    /// (it then stands and rests). A group or a fight ends the break at once.
    /// </summary>
    public static bool OnBreak(GameBot bot)
    {
        long until = bot?.TempProperties.GetProperty<long>(BreakUntilKey) ?? 0;
        if (until <= 0)
            return false;
        if (until > GameLoop.GameLoopTime && bot.Group == null && !bot.InCombat)
            return true;
        bot.TempProperties.RemoveProperty(BreakUntilKey);
        if (Talks && !bot.InCombat && Random.Shared.NextDouble() < 0.6)
            AutonomousBotChatCoordinator.SpeakReaction(bot, BehaviourLines.CasualBreakEnds[Random.Shared.Next(BehaviourLines.CasualBreakEnds.Length)]);
        return false;
    }

    // Hardcore

    private static bool HardcoreGripe(GameBot bot, long now)
    {
        if (!AutonomousBotChatCoordinator.IsGroupedWithPlayer(bot))
            return false;
        var members = bot.Group.GetMembersInTheGroup();
        bool sitting = !members.Any(member => member.InCombat) &&
                       (bot.IsRecoveryResting || members.OfType<GamePlayer>().Any(player => player.IsSitting));
        if (!sitting)
        {
            bot.TempProperties.RemoveProperty(SitSinceKey);
            return false;
        }
        long since = bot.TempProperties.GetProperty<long>(SitSinceKey);
        if (since == 0)
        {
            bot.TempProperties.SetProperty(SitSinceKey, now);
            return false;
        }
        if (now - since < SitComplaintAfterMs || bot.TempProperties.GetProperty<long>(NextGripeKey) > now)
            return false;
        bot.TempProperties.SetProperty(NextGripeKey, now + Random.Shared.Next(4, 7) * 60_000L);
        if (!Talks || Random.Shared.NextDouble() >= 0.6)
            return false;
        AutonomousBotChatCoordinator.SpeakReaction(bot, BehaviourLines.HardcoreSitComplaints[Random.Shared.Next(BehaviourLines.HardcoreSitComplaints.Length)]);
        return true;
    }

    // Min/maxer

    private static bool MinMaxerInspect(GameBot bot, long now)
    {
        if (!Talks || bot.TempProperties.GetProperty<long>(NextInspectKey) > now)
            return false;
        // Nobody to advise (or not in the mood): look again in a few minutes.
        bot.TempProperties.SetProperty(NextInspectKey, now + Random.Shared.Next(2, 5) * 60_000L);
        if (Random.Shared.NextDouble() >= 0.4)
            return false;

        var seen = bot.TempProperties.GetProperty<Dictionary<string, long>>(InspectedKey);
        if (seen == null)
        {
            seen = new Dictionary<string, long>();
            bot.TempProperties.SetProperty(InspectedKey, seen);
        }
        foreach (string old in seen.Where(pair => now - pair.Value > InspectMemoryMs).Select(pair => pair.Key).ToList())
            seen.Remove(old);
        if (seen.Count >= MaxInspectMemory)
            return false;

        // A newcomer: a player close by, or a groupmate, it hasn't looked over lately.
        IEnumerable<GameLiving> near = bot.GetPlayersInRadius(InspectRange).Where(player => player.Realm == bot.Realm);
        if (bot.Group != null)
            near = near.Concat(bot.Group.GetMembersInTheGroup().Where(member => member != bot && bot.IsWithinRadius(member, WorldMgr.SAY_DISTANCE)));
        GameLiving target = near.Take(MaxNpcsLooked).FirstOrDefault(living => !seen.ContainsKey(living.Name));
        ICharacterClass characterClass = target switch
        {
            GamePlayer player => player.CharacterClass,
            GameBot other => other.CharacterClass,
            _ => null,
        };
        if (characterClass == null ||
            InspectLine(target.Name, characterClass.Name, SpecSummary((eCharacterClass)characterClass.ID), Random.Shared) is not { } line)
            return false;
        seen[target.Name] = now;
        bot.TempProperties.SetProperty(NextInspectKey,
            now + Random.Shared.Next(InspectCooldownMinMinutes, InspectCooldownMaxMinutes + 1) * 60_000L);
        if (PersoperProperties.EMOTES)
            Emote(bot, eEmote.Ponder, target);
        Later(bot, 1_500, () => AutonomousBotChatCoordinator.SpeakReaction(bot, line));
        return true;
    }

    // Joker

    private static bool JokerEmote(GameBot bot, long now)
    {
        if (!PersoperProperties.EMOTES)
            return false;
        long next = bot.TempProperties.GetProperty<long>(NextJokerEmoteKey);
        if (next > now)
            return false;
        bot.TempProperties.SetProperty(NextJokerEmoteKey, now + Random.Shared.Next(3, 7) * 60_000L);
        if (next == 0 || Random.Shared.Next(2) == 0)
            return false;
        GamePlayer audience = bot.GetPlayersInRadius(400).Take(MaxNpcsLooked).FirstOrDefault(player => player.Realm == bot.Realm);
        if (audience == null)
            return false;
        eEmote emote = JokerEmotes[Random.Shared.Next(JokerEmotes.Length)];
        Emote(bot, emote, emote == eEmote.Dance ? null : audience);
        return true;
    }

    // Every bot: emotes

    /// <summary>Called when a player aims an emote at a bot: it may answer with one of its own.</summary>
    public static void OnEmotedAt(GamePlayer player, GameBot bot, eEmote incoming)
    {
        if (!PersoperProperties.EMOTES || player == null || bot == null || !bot.IsAlive ||
            bot.ObjectState != GameObject.eObjectState.Active || bot.InCombat || bot.IsCasting)
            return;
        long now = GameLoop.GameLoopTime;
        if (bot.TempProperties.GetProperty<long>(NextEmoteReplyKey) > now)
            return;
        bot.TempProperties.SetProperty(NextEmoteReplyKey, now + EmoteReplyCooldownMs);
        BotPersonality personality = BotPersonalities.For(bot);
        if (!Rolls(EmoteReplyPercent(personality?.TypeKey, personality != null && personality.TypeKey == null), Random.Shared))
            return;
        eEmote reply = EmoteReply(incoming, personality?.TypeKey, Random.Shared);
        Later(bot, 700 + Random.Shared.Next(800), () => Emote(bot, reply, player));
    }

    /// <summary>Someone died: up to two bots near them grieve, shrug or mock, by type and realm.</summary>
    private static void OnDying(DOLEvent e, object sender, EventArgs args)
    {
        // Every mob's death passes here: leave at once unless a player or a world bot died.
        if (!PersoperProperties.EMOTES || sender is not GamePlayer && sender is not GameBot { IsAutonomousWorldBot: true })
            return;
        var victim = (GameLiving)sender;
        if (victim.CurrentRegion == null || !TakeDeathTurn(victim.CurrentRegionID))
            return;
        var witnesses = victim.GetNPCsInRadius((ushort)WorldMgr.SAY_DISTANCE).Take(MaxNpcsLooked).OfType<GameBot>()
            .Where(bot => bot != victim && bot.IsAlive && !bot.IsCasting)
            .OrderBy(_ => Random.Shared.Next())
            .ToList();
        int emoted = 0;
        foreach (GameBot bot in witnesses)
        {
            if (emoted >= MaxDeathEmoters)
                break;
            if (Random.Shared.Next(2) == 0)
                continue;
            eEmote emote = DeathEmote(BotPersonalities.For(bot)?.TypeKey, bot.Realm != victim.Realm, Random.Shared);
            Later(bot, 1_500 + emoted * 1_200 + Random.Shared.Next(1_000), () =>
            {
                if (!bot.IsAttacking && !bot.IsCasting)
                    Emote(bot, emote, victim);
            });
            emoted++;
        }
    }

    private static bool TakeDeathTurn(ushort regionId)
    {
        long now = GameLoop.GameLoopTime;
        lock (Sync)
        {
            if (NextDeathEmote.TryGetValue(regionId, out long next) && now < next)
                return false;
            NextDeathEmote[regionId] = now + DeathEmoteRegionCooldownMs;
            return true;
        }
    }

    /// <summary>A bot emotes: the animation, and the text for players close enough to see it.</summary>
    public static void Emote(GameBot bot, eEmote emote, GameObject target)
    {
        if (bot?.ObjectState != GameObject.eObjectState.Active)
            return;
        if (target != null && !bot.IsMoving)
            bot.TurnTo(target);
        bot.Emote(emote);
        foreach (GamePlayer player in bot.GetPlayersInRadius(EmoteRange))
        {
            string targetName = target == null ? null : target == player ? "you" : target.GetName(0, false);
            player.Out.SendMessage(EmoteText(bot.Name, emote, targetName), eChatType.CT_Emote, eChatLoc.CL_SystemWindow);
        }
    }

    private static void Later(GameBot bot, int delay, Action action)
    {
        _ = new ECSGameTimer(bot, _ =>
        {
            if (bot.ObjectState == GameObject.eObjectState.Active)
                action();
            return 0;
        }, delay);
    }
}
