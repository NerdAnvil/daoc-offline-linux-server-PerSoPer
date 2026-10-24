using System;
using System.Collections.Generic;
using System.Linq;
using DOL.Logging;

namespace DOL.GS;

/// <summary>
/// The chat lines of one personality bot that has its own file (dialogue/&lt;name&gt;.dlg, block
/// "=== &lt;name&gt;"): Persoper and Kobaldjovi. Unlike the six types (PersonalityLines), the
/// sections are named by the code that reads them. A missing section is logged and comes back empty.
/// </summary>
public sealed class IdentityDialogue
{
    private static readonly Logger Log = LoggerManager.Create(typeof(IdentityDialogue));

    private readonly string _name;
    private readonly IReadOnlyList<DialogueSection> _sections;

    public IdentityDialogue(string name)
    {
        _name = name;
        DialogueFile file = DialogueLibrary.For(name);
        if (file == null && Log.IsErrorEnabled)
            Log.Error($"Dialogue: {name}.dlg is missing");
        _sections = file?.Block(name) ?? [];
    }

    /// <summary>The lines of a section by name and argument ("song", "greeting first_meeting"); empty when missing.</summary>
    public string[] Lines(string section, string argument = null)
    {
        string[] lines = _sections.FirstOrDefault(found => found.Name == section && found.Argument == argument)?.Lines.ToArray();
        if (lines is not { Length: > 0 } && Log.IsErrorEnabled)
            Log.Error($"Dialogue: {_name}.dlg has no '{section} {argument}' lines");
        return lines ?? [];
    }

    /// <summary>The sections of one kind keyed by intent ("reply greeting" -> Greeting); a bad intent is logged and skipped.</summary>
    public Dictionary<eAutonomousChatIntent, string[]> Intents(string section)
    {
        var intents = new Dictionary<eAutonomousChatIntent, string[]>();
        foreach (DialogueSection found in _sections.Where(found => found.Name == section))
        {
            if (Enum.TryParse(found.Argument, true, out eAutonomousChatIntent intent) && Enum.IsDefined(intent))
                intents[intent] = found.Lines.ToArray();
            else if (Log.IsErrorEnabled)
                Log.Error($"Dialogue: {_name}.dlg:{found.LineNumber}: unknown intent '{found.Argument}'");
        }
        return intents;
    }
}
