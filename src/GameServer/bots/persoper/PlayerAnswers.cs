using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DOL.Database;
using DOL.Database.Attributes;
using DOL.Logging;

namespace DOL.GS;

/// <summary>
/// What one player answered to one of the bots' questions ("fav_music" -> "metal"). One row per
/// player and question; every bot that asks the same question shares the answer.
/// </summary>
[DataTable(TableName = "persoper_player_answer")]
public sealed class PlayerAnswerRecord : DataObject
{
    /// <summary>"{character id}|{question}".</summary>
    [PrimaryKey] public string AnswerKey { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string PlayerId { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string Question { get; set; } = string.Empty;
    /// <summary>The answer key from the question's "-> key: reply" row; "other" when none matched.</summary>
    [DataElement(AllowDbNull = false)] public string Answer { get; set; } = string.Empty;
    [DataElement(AllowDbNull = false)] public string AnsweredUtc { get; set; } = string.Empty;

    public static string KeyFor(string playerId, string question) => $"{playerId}|{question}";
}

/// <summary>
/// The saved answers of the players the bots met, kept in memory so rules can check them for every
/// line without a database read. Loading and saving are handed in, so it is unit tested without a
/// database. Safe to call from several threads. It runs in chat, so a failed load or save (a busy
/// database) is logged, never thrown and never kept: the next look reads the database again.
/// </summary>
public sealed partial class PlayerAnswerCache(Func<string, IReadOnlyDictionary<string, string>> load, Action<string, string, string> save)
{
    private static readonly Logger Log = LoggerManager.Create(typeof(PlayerAnswerCache));

    /// <summary>Players kept in memory at most; past that the cache starts over and reloads from the database.</summary>
    public const int MaxPlayers = 500;

    /// <summary>Questions saved per player at most.</summary>
    public const int MaxAnswersPerPlayer = 64;

    private readonly Dictionary<string, Dictionary<string, string>> _players = new(StringComparer.Ordinal);
    private readonly object _lock = new();

    /// <summary>A player's saved answers by question; empty for a player who has none or an empty id.</summary>
    public IReadOnlyDictionary<string, string> For(string playerId)
    {
        if (string.IsNullOrEmpty(playerId))
            return new Dictionary<string, string>();
        lock (_lock)
            return new Dictionary<string, string>(Entry(playerId));
    }

    /// <summary>The player's answer to one question, or null when they haven't answered it.</summary>
    public string Get(string playerId, string question)
    {
        if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(question))
            return null;
        lock (_lock)
            return Entry(playerId).GetValueOrDefault(question);
    }

    /// <summary>
    /// Saves an answer (a new one replaces the old). False when an id or word is bad, the player is at
    /// the cap, or the save failed (then it is still remembered until the server restarts).
    /// </summary>
    public bool Set(string playerId, string question, string answer)
    {
        if (string.IsNullOrEmpty(playerId) || !Word().IsMatch(question ?? string.Empty) || !Word().IsMatch(answer ?? string.Empty))
            return false;
        lock (_lock)
        {
            Dictionary<string, string> answers = Entry(playerId);
            if (!answers.ContainsKey(question) && answers.Count >= MaxAnswersPerPlayer)
                return false;
            answers[question] = answer;
            try
            {
                save(playerId, question, answer);
                return true;
            }
            catch (Exception exception)
            {
                if (Log.IsErrorEnabled)
                    Log.Error($"PLAYER_ANSWER_SAVE_FAILED player={playerId} question={question}", exception);
                return false;
            }
        }
    }

    private Dictionary<string, string> Entry(string playerId)
    {
        if (_players.TryGetValue(playerId, out Dictionary<string, string> answers))
            return answers;
        IReadOnlyDictionary<string, string> loaded;
        try
        {
            loaded = load(playerId);
        }
        catch (Exception exception)
        {
            // Not stored: an empty entry would read as "answered nothing" until the cache starts over.
            if (Log.IsErrorEnabled)
                Log.Error($"PLAYER_ANSWER_LOAD_FAILED player={playerId}", exception);
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
        if (_players.Count >= MaxPlayers)
            _players.Clear();
        return _players[playerId] = loaded == null ? [] : new Dictionary<string, string>(loaded.Take(MaxAnswersPerPlayer), StringComparer.Ordinal);
    }

    [GeneratedRegex(@"^[a-z0-9_-]{1,32}$")]
    private static partial Regex Word();
}

/// <summary>The bots' shared saved answers, backed by the persoper_player_answer table.</summary>
public static class PlayerAnswers
{
    public static readonly PlayerAnswerCache Cache = new(LoadFromDatabase, SaveToDatabase);

    private static IReadOnlyDictionary<string, string> LoadFromDatabase(string playerId) =>
        GameServer.Database.SelectObjects<PlayerAnswerRecord>(DB.Column("PlayerId").IsEqualTo(playerId))
            .GroupBy(record => record.Question)
            .ToDictionary(group => group.Key, group => group.First().Answer);

    private static void SaveToDatabase(string playerId, string question, string answer)
    {
        string key = PlayerAnswerRecord.KeyFor(playerId, question);
        PlayerAnswerRecord record = GameServer.Database.FindObjectByKey<PlayerAnswerRecord>(key) ?? new PlayerAnswerRecord
        {
            AnswerKey = key,
            PlayerId = playerId,
            Question = question,
        };
        record.Answer = answer;
        record.AnsweredUtc = DateTime.UtcNow.ToString("O");
        record.Dirty = true;
        if (record.IsPersisted)
            GameServer.Database.SaveObject(record);
        else
            GameServer.Database.AddObject(record);
    }
}
