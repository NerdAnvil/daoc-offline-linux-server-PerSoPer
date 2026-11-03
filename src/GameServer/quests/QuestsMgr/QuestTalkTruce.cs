using System;
using System.Collections.Generic;
using System.Reflection;
using DOL.Database;
using DOL.Events;

namespace DOL.GS.Quests
{
    /// <summary>
    /// Some quests send the player to talk to, whisper, or hand items to a monster: Tiomoid the Siabra Gurite in Lough
    /// Gur (Lost Townfolk of Bran Llyr), Trefor and Curmie (Occupation of Lough Gur), Shiake (Mother's Children), the
    /// XP-item turn-in NPCs of the Shrouded Isles tribes. Their faction is kill-on-sight by default, or they have no
    /// faction and attack anyone, so the player was attacked on arrival or turned away with a dirty look and the quest
    /// could not go on (quest audit 2026-10-13: 67 such NPCs). While a player is on a step that talks to, whispers to
    /// or hands items to one of them, or can take one of its quests, that NPC does not aggro the player and talks to
    /// them. Everyone else meets the NPC as before, and a player who attacks it gets a fight.
    /// </summary>
    public static class QuestTalkTruce
    {
        private static readonly Logging.Logger log = Logging.LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        // Owner 2026-10-26: the names were a Lazy<T>, which keeps a failed load's exception. One read that met a
        // locked database made every later aggro check throw, and NpcService took each companion that asked out of
        // the world (it looked like an instant death, and /persoper then made a second Persoper). A failed load is
        // now tried again after a gap, and until then the truce simply doesn't hold.
        private const long RetryGapMilliseconds = 30_000;
        private static readonly RetryingLoad<HashSet<string>> _questNpcNames = new(LoadQuestNpcNames, RetryGapMilliseconds);

        /// <summary>Loads the names at start-up, while the database is quiet, rather than in the first aggro check.</summary>
        [GameServerStartedEvent]
        public static void OnServerStarted(DOLEvent e, object sender, EventArgs args) => QuestNpcNames();

        /// <summary>Called from mob aggro checks: cheap for every NPC that is not a data quest's talk target.</summary>
        public static bool Holds(GameNPC npc, GamePlayer player)
        {
            if (npc == null || player == null || npc.Realm != eRealm.None || QuestNpcNames()?.Contains(npc.Name) != true)
                return false;

            foreach (var pair in player.QuestList) // not .Keys: that copies the keys on every aggro check
            {
                if (pair.Key is DataQuest dataQuest && TalksTo(dataQuest.StepType) &&
                    dataQuest.TargetName == npc.Name && (dataQuest.TargetRegion == 0 || dataQuest.TargetRegion == npc.CurrentRegionID))
                    return true;
            }

            // A quest it gives. Level range only: the full qualification check reads the database.
            List<DataQuest> given = npc.DataQuestList;
            for (int i = 0; i < given.Count; i++)
            {
                DataQuest quest = i < given.Count ? given[i] : null;
                if (quest?.DBDataQuest == null || quest.StartType == DataQuest.eStartType.KillComplete)
                    continue;
                if (player.Level >= quest.DBDataQuest.MinLevel && player.Level <= quest.DBDataQuest.MaxLevel)
                    return true;
            }

            return false;
        }

        public static bool TalksTo(DataQuest.eStepType step) => step is DataQuest.eStepType.Deliver or DataQuest.eStepType.DeliverFinish or
            DataQuest.eStepType.Interact or DataQuest.eStepType.InteractFinish or DataQuest.eStepType.Whisper or
            DataQuest.eStepType.WhisperFinish or DataQuest.eStepType.Collect or DataQuest.eStepType.CollectFinish;

        /// <summary>The names, or null while they can't be read (logged once per failure streak).</summary>
        private static HashSet<string> QuestNpcNames()
        {
            HashSet<string> names = _questNpcNames.TryGet(Environment.TickCount64, out Exception error, out bool recovered);
            if (error != null)
                log.Error($"QUEST_TALK_TRUCE_LOAD_FAILED retry_in_ms={RetryGapMilliseconds}", error);
            else if (recovered)
                log.Info("QUEST_TALK_TRUCE_LOADED after earlier failures");
            return names;
        }

        /// <summary>
        /// A value loaded on first use that, unlike Lazy&lt;T&gt;, doesn't keep a failure: after a failed load it
        /// returns null until the gap has passed, then tries once more. Only the first failure of a streak is
        /// reported, so a broken database logs once rather than on every aggro check.
        /// </summary>
        public sealed class RetryingLoad<T> where T : class
        {
            private readonly Func<T> _load;
            private readonly long _retryGapMilliseconds;
            private readonly object _lock = new();
            private T _value;
            private long _nextAttempt = long.MinValue;
            private bool _failing;

            public RetryingLoad(Func<T> load, long retryGapMilliseconds)
            {
                _load = load;
                _retryGapMilliseconds = retryGapMilliseconds;
            }

            /// <summary>
            /// The value, or null while a retry gap runs. error: set on the first failure of a streak.
            /// recovered: true on the load that succeeds after a failure.
            /// </summary>
            public T TryGet(long nowMilliseconds, out Exception error, out bool recovered)
            {
                error = null;
                recovered = false;
                T value = System.Threading.Volatile.Read(ref _value);
                if (value != null)
                    return value;

                lock (_lock)
                {
                    if (_value != null)
                        return _value;
                    if (nowMilliseconds < _nextAttempt)
                        return null;

                    try
                    {
                        value = _load() ?? throw new InvalidOperationException("The load returned nothing.");
                    }
                    catch (Exception exception)
                    {
                        _nextAttempt = nowMilliseconds + _retryGapMilliseconds;
                        if (!_failing)
                            error = exception;
                        _failing = true;
                        return null;
                    }

                    recovered = _failing;
                    _failing = false;
                    System.Threading.Volatile.Write(ref _value, value);
                    return value;
                }
            }
        }

        /// <summary>Start NPCs and talk/whisper/hand-in targets of every data quest (names compared exactly, as DataQuest does).</summary>
        private static HashSet<string> LoadQuestNpcNames()
        {
            HashSet<string> names = new(StringComparer.Ordinal);

            foreach (DbDataQuest quest in GameServer.Database.SelectAllObjects<DbDataQuest>())
            {
                if (!string.IsNullOrEmpty(quest.StartName))
                    names.Add(quest.StartName);

                string[] types = (quest.StepType ?? string.Empty).Split('|');
                string[] targets = (quest.TargetName ?? string.Empty).Split('|');

                for (int i = 0; i < Math.Min(types.Length, targets.Length); i++)
                {
                    if (!byte.TryParse(types[i], out byte type) || !TalksTo((DataQuest.eStepType) type))
                        continue;

                    string name = targets[i].Split(';')[0];
                    if (name.Length > 0)
                        names.Add(name);
                }
            }

            return names;
        }
    }
}
