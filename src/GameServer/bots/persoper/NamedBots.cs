using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.Database;
using DOL.Database.Attributes;
using DOL.GS.Admin;
using DOL.Logging;

namespace DOL.GS;

/// <summary>
/// One row per named bot in <see cref="NamedBotLineup"/>: which roster bot it is and whether it's
/// on. A missing row means "never created yet", the only case where the server creates it.
/// </summary>
[DataTable(TableName = "persoper_named_bot")]
public sealed class NamedBotRecord : DataObject
{
    /// <summary>The lineup key, "joker-mid".</summary>
    [PrimaryKey] public string NamedKey { get; set; } = string.Empty;
    /// <summary>The roster bot (offline_world_bots); 0 when it was never created.</summary>
    [DataElement(AllowDbNull = false)] public long BotId { get; set; }
    [DataElement(AllowDbNull = false)] public string State { get; set; } = NamedBots.On;
}

/// <summary>
/// The 33 named personality bots in the running server (docs/NAMED-BOTS-DESIGN.md): created once
/// on server start, matched by roster id, and logged in before other bots.
/// </summary>
public static class NamedBots
{
    public const string On = "on";
    public const string Off = "off";
    public const string Deleted = "deleted";

    private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

    public enum StartAction { Keep, Create, RenameThenCreate, SkipPlayerHasName }

    /// <summary>What server start does for one lineup entry. <see cref="ClashBotId"/> is the generated bot to rename.</summary>
    public sealed record StartStep(NamedBotLineup.Entry Entry, StartAction Action, long ClashBotId = 0);

    /// <summary>
    /// The start-up plan, one step per lineup entry (33, bounded). An entry with a row is left
    /// alone. Without one: a player character with its name wins (the bot is skipped), a roster
    /// bot with its name is renamed first, otherwise it's simply created. Names match in any case.
    /// </summary>
    public static IReadOnlyList<StartStep> PlanStart(
        ISet<string> keysWithRow,
        IReadOnlyDictionary<string, long> rosterIdsByName,
        ISet<string> playerNames)
    {
        var steps = new List<StartStep>(NamedBotLineup.All.Count);
        foreach (NamedBotLineup.Entry entry in NamedBotLineup.All)
        {
            if (keysWithRow.Contains(entry.Key))
                steps.Add(new StartStep(entry, StartAction.Keep));
            else if (playerNames.Contains(entry.Name))
                steps.Add(new StartStep(entry, StartAction.SkipPlayerHasName));
            else if (rosterIdsByName.TryGetValue(entry.Name, out long clash))
                steps.Add(new StartStep(entry, StartAction.RenameThenCreate, clash));
            else
                steps.Add(new StartStep(entry, StartAction.Create));
        }
        return steps;
    }

    /// <summary>The bot to create for an entry; a "Random" gender is rolled here and then saved with the bot.</summary>
    public static AutonomousBotIdentityGenerator.Identity IdentityFor(NamedBotLineup.Entry entry, Random random)
    {
        random ??= Random.Shared;
        eGender gender = entry.Gender ?? (random.Next(2) == 0 ? eGender.Male : eGender.Female);
        return new AutonomousBotIdentityGenerator.Identity(entry.Name, entry.Realm, gender, entry.CharacterClass, entry.Race);
    }

    /// <summary>The entry for what an admin typed: a name ("Shnacksize") or a key ("joker-mid"), any case.</summary>
    public static NamedBotLineup.Entry Find(string typed) =>
        NamedBotLineup.FindByName(typed) ??
        NamedBotLineup.All.FirstOrDefault(entry => string.Equals(entry.Key, typed, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The entries for what an admin typed after "bots named on|off": one name or key, or a type
    /// ("joker", "Herbal Doctor", "min/maxer") for all three realms. Empty when nothing matches.
    /// </summary>
    public static IReadOnlyList<NamedBotLineup.Entry> FindAll(string typed)
    {
        if (Find(typed) is { } one)
            return [one];
        string type = new string((typed ?? string.Empty).Where(char.IsLetter).ToArray()).ToLowerInvariant();
        return type.Length == 0 ? [] : NamedBotLineup.All.Where(entry => entry.TypeKey == type).ToList();
    }

    // --- In the running server ---

    private static readonly object Sync = new();
    private static volatile Dictionary<long, (NamedBotLineup.Entry Entry, string State)> _byBotId;

    private static Dictionary<long, (NamedBotLineup.Entry Entry, string State)> ByBotId => _byBotId ?? Reload();

    private static Dictionary<long, (NamedBotLineup.Entry Entry, string State)> Reload()
    {
        lock (Sync)
        {
            var map = new Dictionary<long, (NamedBotLineup.Entry, string)>();
            foreach (NamedBotRecord row in DOLDB<NamedBotRecord>.SelectAllObjects())
            {
                NamedBotLineup.Entry entry = NamedBotLineup.All.FirstOrDefault(e => e.Key == row.NamedKey);
                if (entry != null && row.BotId > 0)
                    map[row.BotId] = (entry, row.State);
            }
            _byBotId = map;
            return map;
        }
    }

    /// <summary>True for a roster bot that is one of the 33, whatever its state.</summary>
    public static bool IsNamed(long botId) => botId > 0 && ByBotId.ContainsKey(botId);

    /// <summary>The lineup entry of a roster bot that is one of the 33, whatever its state, or null.</summary>
    public static NamedBotLineup.Entry EntryFor(long botId) =>
        botId > 0 && ByBotId.TryGetValue(botId, out var named) ? named.Entry : null;

    /// <summary>True for a named bot that is on: it logs in before other bots.</summary>
    public static bool IsOn(long botId) => botId > 0 && ByBotId.TryGetValue(botId, out var named) && named.State == On;

    /// <summary>True for a named bot that is off: the population controller leaves it out of the world.</summary>
    public static bool IsOff(long botId) => botId > 0 && ByBotId.TryGetValue(botId, out var named) && named.State == Off;

    /// <summary>Marks a named bot deleted (bots delete), so it isn't made again on start. Others are ignored.</summary>
    public static void MarkDeleted(long botId)
    {
        if (botId > 0 && ByBotId.TryGetValue(botId, out var named) && named.State != Deleted)
        {
            SaveRow(named.Entry.Key, botId, Deleted);
            Log.Info($"Named bot {named.Entry.Name} ({named.Entry.Key}) deleted; bots create --personality {named.Entry.Name} makes it again.");
        }
    }

    /// <summary>
    /// Server start: creates every named bot that has no row yet, at level 1 with no armour, and
    /// saves its row as on. Runs before any bot logs in, so a renamed generated bot isn't live.
    /// </summary>
    public static void CreateMissingOnStart()
    {
        try
        {
            lock (AutonomousBotStatusPersistence.DatabaseWriteLock)
            {
                var keysWithRow = new HashSet<string>(DOLDB<NamedBotRecord>.SelectAllObjects().Select(row => row.NamedKey));
                if (NamedBotLineup.All.All(entry => keysWithRow.Contains(entry.Key)))
                    return;

                OfflineWorldBotRecord[] roster = DOLDB<OfflineWorldBotRecord>.SelectAllObjects().ToArray();
                var rosterIdsByName = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
                foreach (OfflineWorldBotRecord record in roster)
                    rosterIdsByName.TryAdd(record.Name, record.BotId);
                var playerNames = new HashSet<string>(DOLDB<DbCoreCharacter>.SelectAllObjects().Select(c => c.Name), StringComparer.OrdinalIgnoreCase);
                // Names a renamed generated bot must not get.
                var taken = new HashSet<string>(rosterIdsByName.Keys.Concat(playerNames), StringComparer.OrdinalIgnoreCase);

                var created = new List<string>();
                foreach (StartStep step in PlanStart(keysWithRow, rosterIdsByName, playerNames))
                {
                    NamedBotLineup.Entry entry = step.Entry;
                    switch (step.Action)
                    {
                        case StartAction.Keep:
                            continue;
                        case StartAction.SkipPlayerHasName:
                            SaveRow(entry.Key, 0, Off);
                            Log.Warn($"Named bot {entry.Name} not created: a player character already has that name. Saved as off.");
                            continue;
                        case StartAction.RenameThenCreate:
                            RenameGenerated(roster.First(record => record.BotId == step.ClashBotId), taken);
                            break;
                    }
                    OfflineWorldBotRecord added = BotRosterAdmin.AddRecord(IdentityFor(entry, Random.Shared), 1);
                    SaveRow(entry.Key, added.BotId, On);
                    created.Add(entry.Name);
                }
                if (created.Count > 0)
                    Log.Info($"Created {created.Count} named bot(s) at level 1: {string.Join(", ", created)}");
            }
        }
        catch (Exception exception)
        {
            Log.Error("Could not create the named bots on start", exception);
        }
        finally
        {
            Reload();
        }
    }

    /// <summary>Saves (adds or replaces) one row and refreshes the lookup.</summary>
    public static void SaveRow(string key, long botId, string state)
    {
        NamedBotRecord row = GameServer.Database.FindObjectByKey<NamedBotRecord>(key) ?? new NamedBotRecord { NamedKey = key };
        row.BotId = botId;
        row.State = state;
        row.Dirty = true;
        if (!(row.IsPersisted ? GameServer.Database.SaveObject(row) : GameServer.Database.AddObject(row)))
            throw new InvalidOperationException($"Could not save named bot row {key}.");
        Reload();
    }

    /// <summary>The saved row for an entry, or null.</summary>
    public static NamedBotRecord RowFor(NamedBotLineup.Entry entry) =>
        GameServer.Database.FindObjectByKey<NamedBotRecord>(entry.Key);

    private static void RenameGenerated(OfflineWorldBotRecord record, ISet<string> taken)
    {
        string old = record.Name;
        eGender gender = record.Gender == (int)eGender.Female ? eGender.Female : eGender.Male;
        record.Name = AutonomousBotIdentityGenerator.GenerateName((eRealm)record.Realm, gender, taken);
        record.Dirty = true;
        GameServer.Database.SaveObject(record);
        Log.Warn($"Renamed generated bot {old} (id {record.BotId}) to {record.Name}: {old} is a named bot.");
    }
}
