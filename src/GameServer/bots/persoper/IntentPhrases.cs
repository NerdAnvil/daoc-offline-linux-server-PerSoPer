using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Logging;

namespace DOL.GS;

/// <summary>
/// Words and phrases that sort a player's chat into an intent, from dialogue/intents.dlg:
/// <code>
/// === intents
/// ~ intent thank
/// thanks, thank you, ty, cheers
/// </code>
/// A phrase matches when its words appear in a row in the message. The longest phrase wins, then
/// the one higher in the file. Words of one letter are dropped, as the classifier drops them.
/// The classifier (AutonomousChatIntentModel) asks here first, after its rude and out-of-world words.
/// </summary>
public sealed class IntentPhrases
{
    /// <summary>Phrases read at most, and words per phrase at most.</summary>
    public const int MaxPhrases = 2000;
    public const int MaxWords = 6;

    private static readonly Logger Log = LoggerManager.Create(typeof(IntentPhrases));
    private static readonly DialogueCache<IntentPhrases> Loaded = new(Load);

    private readonly List<(string[] Words, eAutonomousChatIntent Intent)> _phrases = [];

    /// <summary>The phrases from intents.dlg (read again after a dialogue reload); none when the file is missing.</summary>
    public static IntentPhrases Shared => Loaded.Value;

    /// <summary>Problems found while reading, as "file:line: what".</summary>
    public List<string> Errors { get; } = [];

    public int Count => _phrases.Count;

    /// <summary>Reads the "intents" block of a parsed file.</summary>
    public static IntentPhrases From(DialogueFile file)
    {
        var phrases = new IntentPhrases();
        if (file == null)
            return phrases;
        foreach (DialogueSection section in file.Block("intents"))
        {
            if (section.Name != "intent")
            {
                phrases.Errors.Add($"{file.FileName}:{section.LineNumber}: only '~ intent <name>' sections belong here");
                continue;
            }
            if (!Enum.TryParse(section.Argument, true, out eAutonomousChatIntent intent) || !Enum.IsDefined(intent) ||
                intent == eAutonomousChatIntent.Unknown)
            {
                phrases.Errors.Add($"{file.FileName}:{section.LineNumber}: unknown intent '{section.Argument}'");
                continue;
            }
            foreach (string phrase in section.Lines.SelectMany(line => line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)))
            {
                if (phrases._phrases.Count >= MaxPhrases)
                {
                    phrases.Errors.Add($"{file.FileName}:{section.LineNumber}: more than {MaxPhrases} phrases; the rest are ignored");
                    break;
                }
                string[] words = AutonomousChatIntentModel.Tokens(phrase).ToArray();
                if (words.Length == 0 || words.Length > MaxWords)
                    phrases.Errors.Add($"{file.FileName}:{section.LineNumber}: phrase '{phrase}' needs 1 to {MaxWords} words of two letters or more");
                else
                    phrases._phrases.Add((words, intent));
            }
        }
        // Longest first; a stable sort keeps file order among equals.
        List<(string[] Words, eAutonomousChatIntent Intent)> sorted = phrases._phrases.OrderByDescending(phrase => phrase.Words.Length).ToList();
        phrases._phrases.Clear();
        phrases._phrases.AddRange(sorted);
        return phrases;
    }

    /// <summary>The intent of the first phrase found in the message words, or null.</summary>
    public eAutonomousChatIntent? Match(IReadOnlyList<string> tokens)
    {
        if (tokens == null || tokens.Count == 0)
            return null;
        foreach ((string[] words, eAutonomousChatIntent intent) in _phrases)
        {
            for (int start = 0; start + words.Length <= tokens.Count; start++)
            {
                int matched = 0;
                while (matched < words.Length && string.Equals(tokens[start + matched], words[matched], StringComparison.OrdinalIgnoreCase))
                    matched++;
                if (matched == words.Length)
                    return intent;
            }
        }
        return null;
    }

    /// <summary>The intent of the first phrase found in a chat line, or null.</summary>
    public eAutonomousChatIntent? Match(string text) => Match(AutonomousChatIntentModel.Tokens(text).ToArray());

    private static IntentPhrases Load()
    {
        IntentPhrases phrases = From(DialogueLibrary.For("intents"));
        if (phrases.Count == 0 && Log.IsErrorEnabled)
            Log.Error("Dialogue: intents.dlg is missing or has no phrases");
        foreach (string error in phrases.Errors)
            if (Log.IsErrorEnabled)
                Log.Error($"Dialogue: {error}");
        return phrases;
    }
}
