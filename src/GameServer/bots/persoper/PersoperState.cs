using System;
using System.Linq;
using DOL.Database;
using DOL.Database.Attributes;
using DOL.GS.ServerProperties;

namespace DOL.GS;

public static class PersoperProperties
{
    [ServerProperty("persoper", "persoper_tone_down", "Keeps Persoper's surnames and greetings clean (no flirty or raunchy lines).", false)]
    public static bool TONE_DOWN;

    [ServerProperty("persoper", "persoper_surname_change_percent", "Chance (0-100) that Persoper decides on a new surname when summoned.", 5)]
    public static int SURNAME_CHANGE_PERCENT;

    [ServerProperty("persoper", "persoper_annoyance", "Personality bots (Persoper, Kobaldjovi) notice repeat irritations (quick re-calls, rudeness, emote spam, your deaths) and grumble.", true)]
    public static bool ANNOYANCE;

    [ServerProperty("persoper", "persoper_rage_quit", "When annoyed enough, a personality bot quits on you and sulks (Persoper despawns, Kobaldjovi storms out). Off: they only warn.", true)]
    public static bool RAGE_QUIT;

    [ServerProperty("persoper", "persoper_chatter_percent", "How chatty personality-type bots are, 0-100: 0 = silent, 50 = calm, 100 = lively (default).", 100)]
    public static int CHATTER_PERCENT;

    [ServerProperty("persoper", "persoper_sulk_minutes", "Minutes a personality bot refuses you after quitting on you (0 = no sulk).", 10)]
    public static int SULK_MINUTES;

    [ServerProperty("persoper", "persoper_leeroy_battle_cry_percent", "Chance (0-100) a Leeroy-type bot yells a battle cry when a fight starts (with a cooldown).", 33)]
    public static int LEEROY_BATTLE_CRY_PERCENT;

    [ServerProperty("persoper", "persoper_full_leeroy", "Leeroy-type bots in a player's group may charge a nearby mob on their own while the group waits.", true)]
    public static bool FULL_LEEROY;

    [ServerProperty("persoper", "persoper_full_leeroy_percent", "Chance (0-100), checked once a minute while his group is idle, that a Leeroy goes full Leeroy.", 10)]
    public static int FULL_LEEROY_PERCENT;

    [ServerProperty("persoper", "persoper_emotes", "Bots answer emotes aimed at them and emote when someone near them dies; Jokers emote on their own.", true)]
    public static bool EMOTES;
}

/// <summary>
/// Persoper is one person for the whole server, so her lasting identity is a
/// single row. Her body is a normal companion and is not saved; only who she
/// is (surname, look per realm) and when she was last seen are kept.
/// </summary>
[DataTable(TableName = "persoper_state")]
public sealed class PersoperStateRecord : DataObject
{
    public const string SingletonKey = "persoper";

    [PrimaryKey] public string StateKey { get; set; } = SingletonKey;
    [DataElement(AllowDbNull = false)] public string Surname { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string SurnameChosenUtc { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public int SurnameChangeCount { get; set; }
    [DataElement(AllowDbNull = false)] public int RaceAlbion { get; set; }
    [DataElement(AllowDbNull = false)] public int RaceMidgard { get; set; }
    [DataElement(AllowDbNull = false)] public int RaceHibernia { get; set; }
    [DataElement(AllowDbNull = false)] public int SummonCount { get; set; }
    [DataElement(AllowDbNull = false)] public string LastSummonedBy { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string LastSummonedUtc { get; set; } = string.Empty;

    public eRace RaceFor(eRealm realm) => (eRace)(realm switch
    {
        eRealm.Albion => RaceAlbion,
        eRealm.Hibernia => RaceHibernia,
        _ => RaceMidgard,
    });

    public void SetRace(eRealm realm, eRace race)
    {
        switch (realm)
        {
            case eRealm.Albion: RaceAlbion = (int)race; break;
            case eRealm.Hibernia: RaceHibernia = (int)race; break;
            default: RaceMidgard = (int)race; break;
        }
    }
}

public static class PersoperStateStore
{
    public static PersoperStateRecord Load() =>
        GameServer.Database.FindObjectByKey<PersoperStateRecord>(PersoperStateRecord.SingletonKey) ?? new PersoperStateRecord();

    public static bool Save(PersoperStateRecord record)
    {
        record.Dirty = true;
        return record.IsPersisted ? GameServer.Database.SaveObject(record) : GameServer.Database.AddObject(record);
    }

    /// <summary>
    /// Name of an existing player character or autonomous bot that already
    /// uses "Persoper" (created before the name was reserved), or null.
    /// </summary>
    public static string FindNameConflict()
    {
        string pattern = $"%{PersoperIdentity.FirstName}%";
        DbCoreCharacter character = GameServer.Database.SelectObjects<DbCoreCharacter>(DB.Column("Name").IsLike(pattern)).FirstOrDefault();
        if (character != null)
            return character.Name;

        OfflineWorldBotRecord bot = GameServer.Database.SelectObjects<OfflineWorldBotRecord>(DB.Column("Name").IsLike(pattern)).FirstOrDefault();
        return bot?.Name;
    }

    public static string UtcNow() => DateTime.UtcNow.ToString("O");
}
