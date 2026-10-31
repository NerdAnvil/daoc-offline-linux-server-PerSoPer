using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DOL.GS;

/// <summary>
/// "{bot:shnacksize}" in a line: a named character by its file name, said as the speaker's realm
/// calls it (Shnacksize in Midgard, Shnacksizer in Albion, Shnacksizey in Hibernia). Character
/// files are filled per realm when they load; Persoper's replies when she picks one. Lines shared
/// by all three realms must use it instead of a bot's name (see <see cref="Check"/>).
/// </summary>
public static partial class DialogueBotNames
{
    /// <summary>The files besides the character files whose lines may use it, and the sections it is filled in there.</summary>
    public static readonly IReadOnlySet<string> IdentityFiles = new HashSet<string> { "persoper", "kobaldjovi" };
    public static readonly IReadOnlySet<string> IdentitySections = new HashSet<string> { "reply", "cheeky_reply", "fallback", "ambient" };

    private static readonly eRealm[] Realms = [eRealm.Midgard, eRealm.Albion, eRealm.Hibernia];

    /// <summary>A character's name per realm, by its file name ("shnacksize").</summary>
    private static readonly Dictionary<string, Dictionary<eRealm, string>> ByCharacter = NamedBotLineup.All
        .GroupBy(PersonalityLines.CharacterKey)
        .ToDictionary(group => group.Key, group => group.ToDictionary(entry => entry.Realm, entry => entry.Name));

    /// <summary>The line with each {bot:...} filled for this realm; Midgard's names when it isn't known. Unknown characters stay as written.</summary>
    public static string Fill(string line, eRealm? realm)
    {
        if (string.IsNullOrEmpty(line) || !line.Contains("{bot:", StringComparison.Ordinal))
            return line;
        eRealm speaker = realm is eRealm.Albion or eRealm.Hibernia ? realm.Value : eRealm.Midgard;
        return Slot().Replace(line, match =>
            ByCharacter.TryGetValue(match.Groups["key"].Value.ToLowerInvariant(), out Dictionary<eRealm, string> names) ? names[speaker] : match.Value);
    }

    /// <summary>A pool filled for this realm; the same array when no line needs it.</summary>
    public static string[] Fill(string[] lines, eRealm? realm) =>
        lines == null || !lines.Any(line => line?.Contains("{bot:", StringComparison.Ordinal) == true)
            ? lines
            : lines.Select(line => Fill(line, realm)).ToArray();

    /// <summary>The line as each realm says it (one entry when it has no {bot:...}).</summary>
    public static IEnumerable<string> Variants(string line) =>
        line?.Contains("{bot:", StringComparison.Ordinal) == true ? Realms.Select(realm => Fill(line, realm)).Distinct() : [line];

    /// <summary>
    /// A file's {bot:...} problems: an unknown character, a {bot:...} where nothing fills it, or a
    /// named bot's own name in a line every realm says (it would be wrong in two of them).
    /// Realm blocks of a character file may name bots; Kobaldjovi lives in Midgard only.
    /// </summary>
    public static List<string> Check(DialogueFile file, string name)
    {
        var problems = new List<string>();
        bool character = PersonalityLines.IsCharacterFile(name);
        bool identity = IdentityFiles.Contains(name);
        foreach (string block in file.BlockNames)
        {
            bool realmBlock = character && Realms.Any(realm => string.Equals(block, RealmName(realm), StringComparison.OrdinalIgnoreCase));
            foreach (DialogueSection section in file.Block(block))
                foreach (string line in section.Lines.Concat(section.Answers?.Select(answer => answer.Reply) ?? []))
                {
                    string where = $"{file.FileName}:{section.LineNumber}: ~ {section.Name}{(section.Argument == null ? "" : " " + section.Argument)}";
                    foreach (Match slot in Slot().Matches(line))
                    {
                        if (!ByCharacter.ContainsKey(slot.Groups["key"].Value.ToLowerInvariant()))
                            problems.Add($"{where}: '{slot.Value}' is no character (use a character file's name, like {{bot:shnacksize}})");
                        else if (!character && !(identity && IdentitySections.Contains(section.Name)))
                            problems.Add($"{where}: '{slot.Value}' isn't filled in here (only in character files and in {string.Join(", ", IdentitySections)} of {string.Join(" and ", IdentityFiles)})");
                    }
                    if (realmBlock || name == "kobaldjovi")
                        continue;
                    foreach (Match word in Word().Matches(Slot().Replace(line, string.Empty)))
                        if (NamedBotLineup.FindByName(word.Value) is { } entry)
                            problems.Add($"{where}: '{word.Value}' is one realm's name; write {{bot:{PersonalityLines.CharacterKey(entry)}}}");
                }
        }
        return problems;
    }

    private static string RealmName(eRealm realm) => realm switch
    {
        eRealm.Albion => "albion",
        eRealm.Hibernia => "hibernia",
        _ => "midgard",
    };

    [GeneratedRegex(@"\{bot:(?<key>[A-Za-z]+)\}")]
    private static partial Regex Slot();

    [GeneratedRegex(@"[A-Za-z]+")]
    private static partial Regex Word();
}
