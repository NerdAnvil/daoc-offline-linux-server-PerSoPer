using System.Collections.Generic;
using System.Text;

namespace DOL.GS
{
    /// <summary>One hit an autonomous gamebot took, kept for its cause-of-death line.</summary>
    public readonly record struct AutonomousDamageHit(string Attacker, int Amount, string DamageType, int HealthBefore);

    /// <summary>
    /// The last few hits an autonomous gamebot took, in a fixed-size ring. Writing
    /// a hit is one array store; nothing grows, so a long fight costs no memory.
    /// </summary>
    public sealed class AutonomousDamageRing
    {
        public const int Capacity = 5;

        private readonly AutonomousDamageHit[] _hits = new AutonomousDamageHit[Capacity];
        private int _next;
        private int _count;

        public int Count => _count;

        public void Record(AutonomousDamageHit hit)
        {
            _hits[_next] = hit;
            _next = (_next + 1) % Capacity;
            if (_count < Capacity)
                _count++;
        }

        public void Clear()
        {
            _next = 0;
            _count = 0;
        }

        /// <summary>The kept hits, newest first.</summary>
        public List<AutonomousDamageHit> NewestFirst()
        {
            List<AutonomousDamageHit> result = new(_count);
            for (int i = 1; i <= _count; i++)
                result.Add(_hits[(_next - i + Capacity) % Capacity]);
            return result;
        }
    }

    /// <summary>Plain values describing one autonomous gamebot death.</summary>
    public sealed class AutonomousDeathCause
    {
        public string BotName = string.Empty;
        public long BotId;
        public string Realm = string.Empty;
        public string ClassName = string.Empty;
        public int Level;
        public int RegionId;
        public string Zone = string.Empty;
        public int X, Y, Z;
        public string Activity = string.Empty;
        public string Target = string.Empty;
        public string Killer = string.Empty;
        public int KillerLevel;
        public string KillerCon = string.Empty;
        public string KillerKind = string.Empty;
        public string KillerOwner = string.Empty;
        public int GroupSize;
        public bool PetOut;
        public int ArmorSlotsFilled;
        public int MaxHealth;
        public int Mana;
        public int MaxMana;
        public IReadOnlyList<AutonomousDamageHit> Hits = [];
    }

    /// <summary>
    /// Builds the AUTONOMOUS_BOT_DEATH_CAUSE log line. Pure: plain values in, one
    /// line out, so it can be tested without a server. Keys follow the other
    /// AUTONOMOUS_* lines, so `bot=` and `id=` filter it the same way.
    /// </summary>
    public static class AutonomousDeathCauseLog
    {
        public const string Tag = "AUTONOMOUS_BOT_DEATH_CAUSE";
        public const int ArmorSlotCount = 6;

        public static string Format(AutonomousDeathCause d)
        {
            StringBuilder line = new(320);
            line.Append(Tag)
                .Append(" bot=").Append(Clean(d.BotName))
                .Append(" id=").Append(d.BotId)
                .Append(" realm=").Append(Clean(d.Realm))
                .Append(" class=\"").Append(Clean(d.ClassName)).Append('"')
                .Append(" level=").Append(d.Level)
                .Append(" region=").Append(d.RegionId)
                .Append(" zone=\"").Append(Clean(d.Zone)).Append('"')
                .Append(" position=").Append(d.X).Append(',').Append(d.Y).Append(',').Append(d.Z)
                .Append(" activity=\"").Append(Clean(d.Activity)).Append('"')
                .Append(" target=\"").Append(Clean(d.Target)).Append('"')
                .Append(" killer=\"").Append(Clean(d.Killer)).Append('"')
                .Append(" killer_level=").Append(d.KillerLevel)
                .Append(" killer_con=").Append(string.IsNullOrEmpty(d.KillerCon) ? "NONE" : Clean(d.KillerCon))
                .Append(" killer_kind=").Append(string.IsNullOrEmpty(d.KillerKind) ? "none" : Clean(d.KillerKind));
            if (!string.IsNullOrEmpty(d.KillerOwner))
                line.Append(" killer_owner=\"").Append(Clean(d.KillerOwner)).Append('"');
            line.Append(" group=").Append(d.GroupSize)
                .Append(" pet=").Append(d.PetOut ? 1 : 0)
                .Append(" armor=").Append(d.ArmorSlotsFilled).Append('/').Append(ArmorSlotCount)
                .Append(" max_hp=").Append(d.MaxHealth)
                .Append(" mana=").Append(d.Mana).Append('/').Append(d.MaxMana);

            // Hits newest first: attacker:damage:type:hp_before, capped at the ring size.
            int hitCount = d.Hits == null ? 0 : System.Math.Min(d.Hits.Count, AutonomousDamageRing.Capacity);
            line.Append(" last_hit_hp_before=").Append(hitCount > 0 ? d.Hits[0].HealthBefore : 0)
                .Append(" hits=\"");
            for (int i = 0; i < hitCount; i++)
            {
                AutonomousDamageHit hit = d.Hits[i];
                if (i > 0)
                    line.Append(';');
                line.Append(Clean(hit.Attacker)).Append(':').Append(hit.Amount)
                    .Append(':').Append(Clean(hit.DamageType)).Append(':').Append(hit.HealthBefore);
            }
            line.Append('"');
            return line.ToString();
        }

        // Keeps a value on one line and inside its quotes.
        private static string Clean(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace('"', '\'').Replace(';', ',').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
