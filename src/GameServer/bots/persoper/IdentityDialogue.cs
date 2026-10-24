using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DOL.Logging;

namespace DOL.GS;

/// <summary>
/// The chat lines of one personality bot that has its own file (dialogue/&lt;name&gt;.dlg, block
/// "=== &lt;name&gt;"): Persoper and Kobaldjovi. Unlike the six types (PersonalityLines), the
/// sections are named by the code that reads them. A missing section is logged once and comes back
/// empty. The lines are read again after a dialogue reload.
/// </summary>
public sealed class IdentityDialogue
{
    /// <summary>The bots with a file of their own.</summary>
    public static readonly IReadOnlyList<string> FileNames = ["persoper", "kobaldjovi"];

    private static readonly Logger Log = LoggerManager.Create(typeof(IdentityDialogue));

    private sealed record Snapshot(IReadOnlyList<DialogueSection> Sections, IReadOnlyList<string> Names)
    {
        public ConcurrentDictionary<(string, string), string[]> Lines { get; } = new();
        public TypeTalk Talk { get; set; }
    }

    private readonly string _name;
    private readonly DialogueCache<Snapshot> _current;

    public IdentityDialogue(string name)
    {
        _name = name;
        _current = new DialogueCache<Snapshot>(Load);
    }

    /// <summary>The bot's nicknames from "names:" in its block; empty when it has none.</summary>
    public IReadOnlyList<string> Names => _current.Value.Names;

    /// <summary>
    /// What its personality answers chat with: "reply" and "cheeky_reply" sections by intent,
    /// "fallback", "song_reaction" (null when missing) and its nicknames.
    /// </summary>
    public TypeTalk Talk
    {
        get
        {
            Snapshot current = _current.Value;
            return current.Talk ??= new TypeTalk(null, Intents("reply"), Intents("cheeky_reply"), Lines("fallback"))
            {
                Nicknames = current.Names,
                SongReactions = current.Sections.Any(section => section.Name == "song_reaction") ? Lines("song_reaction") : null,
            };
        }
    }

    private Snapshot Load()
    {
        DialogueFile file = DialogueLibrary.For(_name);
        if (file == null && Log.IsErrorEnabled)
            Log.Error($"Dialogue: {_name}.dlg is missing");
        return new Snapshot(file?.Block(_name) ?? [], file?.Names(_name) ?? []);
    }

    /// <summary>The lines of a section by name and argument ("song", "greeting first_meeting"); empty when missing.</summary>
    public string[] Lines(string section, string argument = null)
    {
        Snapshot current = _current.Value;
        return current.Lines.GetOrAdd((section, argument), key =>
        {
            string[] lines = current.Sections.FirstOrDefault(found => found.Name == key.Item1 && found.Argument == key.Item2)?.Lines.ToArray();
            if (lines is not { Length: > 0 } && Log.IsErrorEnabled)
                Log.Error($"Dialogue: {_name}.dlg has no '{key.Item1} {key.Item2}' lines");
            return lines ?? [];
        });
    }

    /// <summary>The sections of one kind keyed by intent ("reply greeting" -> Greeting); a bad intent is logged and skipped.</summary>
    public Dictionary<eAutonomousChatIntent, string[]> Intents(string section)
    {
        var intents = new Dictionary<eAutonomousChatIntent, string[]>();
        foreach (DialogueSection found in _current.Value.Sections.Where(found => found.Name == section))
        {
            if (ToIntent(found) is { } intent)
                intents[intent] = found.Lines.ToArray();
            else if (Log.IsErrorEnabled)
                Log.Error($"Dialogue: {_name}.dlg:{found.LineNumber}: unknown intent '{found.Argument}'");
        }
        return intents;
    }

    /// <summary>What's wrong with a bot's own file beyond the reader's errors: a missing block, unknown intents.</summary>
    public static List<string> Check(DialogueFile file, string name)
    {
        var problems = new List<string>();
        IReadOnlyList<DialogueSection> sections = file.Block(name);
        if (sections.Count == 0)
            problems.Add($"{file.FileName}: no block '=== {name}'");
        foreach (DialogueSection section in sections.Where(section => section.Name is "reply" or "cheeky_reply"))
            if (ToIntent(section) == null)
                problems.Add($"{file.FileName}:{section.LineNumber}: unknown intent '{section.Argument}'");
        return problems;
    }

    private static eAutonomousChatIntent? ToIntent(DialogueSection section) =>
        Enum.TryParse(section.Argument, true, out eAutonomousChatIntent intent) && Enum.IsDefined(intent) ? intent : null;
}
