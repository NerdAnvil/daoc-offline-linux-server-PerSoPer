using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DOL.GS;

namespace DaocServer.DialogueCheck;

/// <summary>
/// dialogue-check: checks bot dialogue files with the server's own reader and rules, and previews
/// what a bot would answer. Exit code 0 = no problems, 1 = problems, 2 = bad command line.
/// </summary>
public static class Program
{
    private const string Usage = """
        dialogue-check [FILE.dlg | FOLDER ...]
            Checks dialogue files the way the server does. Problems are printed as file:line: what.
            With no file or folder it checks the release folder's dialogue/.

        dialogue-check --preview BOT INTENT [FACT ...] [--folder FOLDER] [--cheeky] [--player NAME]
            Shows every line BOT could answer to INTENT and whether it would say it now.
            BOT:    persoper, kobaldjovi (or a nickname such as KJ), a type such as joker, or a named bot.
            INTENT: greeting, thank, help, farewell, banter, ...
            FACT:   the situation, e.g. player_dead  health=20  mood=warm  recent=loot  nearby=kobaldjovi
            Uses the files in FOLDER (default: the release folder's dialogue/), with the built-in copy for any file that has problems.
        """;

    /// <summary>The folder checked when none is given: $DAOC_DIALOGUE_DIR (set by the release script), else ./dialogue.</summary>
    private static readonly string DefaultFolder = Environment.GetEnvironmentVariable("DAOC_DIALOGUE_DIR") is { Length: > 0 } dir ? dir : "dialogue";

    public static int Main(string[] args)
    {
        // Only the first word: "help" is also an intent ("--preview persoper help").
        if (args.Length > 0 && args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return 0;
        }
        return args.Length > 0 && args[0] == "--preview" ? Preview(args.Skip(1).ToList()) : Check(args);
    }

    private static int Check(IReadOnlyList<string> args)
    {
        var paths = new List<string>();
        foreach (string arg in args.DefaultIfEmpty(DefaultFolder))
        {
            if (Directory.Exists(arg))
                paths.AddRange(Directory.EnumerateFiles(arg, "*.dlg").Order(StringComparer.Ordinal).Take(DialogueLibrary.MaxFiles));
            else if (File.Exists(arg))
                paths.Add(arg);
            else
            {
                Console.Error.WriteLine($"dialogue-check: no file or folder '{arg}'\n\n{Usage}");
                return 2;
            }
        }

        int problems = 0;
        foreach (string path in paths)
        {
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (new FileInfo(path).Length > DialogueLibrary.MaxFileBytes)
            {
                Console.WriteLine($"{Path.GetFileName(path)}: bigger than {DialogueLibrary.MaxFileBytes / (1024 * 1024)} MB");
                problems++;
                continue;
            }
            DialogueFile file = DialogueFile.Parse(File.ReadAllText(path), Path.GetFileName(path));
            List<string> found = DOL.GS.DialogueCheck.Problems(file, name, DialogueLibrary.BuiltIn.GetValueOrDefault(name));
            foreach (string problem in found)
                Console.WriteLine(problem);
            if (found.Count == 0)
                Console.WriteLine($"{file.FileName}: OK ({file.LineCount} lines)");
            if (!DialogueLibrary.BuiltIn.ContainsKey(name))
                Console.WriteLine($"{file.FileName}: note: the server has no file of this name, so no bot uses it yet");
            problems += found.Count;
        }
        Console.WriteLine(problems == 0 ? $"{paths.Count} files, no problems." : $"{paths.Count} files, {problems} problems.");
        return problems == 0 ? 0 : 1;
    }

    private static int Preview(List<string> args)
    {
        string folder = DefaultFolder, player = "Player";
        bool cheeky = false;
        var words = new List<string>();
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] == "--folder" && i + 1 < args.Count)
                folder = args[++i];
            else if (args[i] == "--player" && i + 1 < args.Count)
                player = args[++i];
            else if (args[i] == "--cheeky")
                cheeky = true;
            else
                words.Add(args[i]);
        }
        if (words.Count < 2)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        DialogueLoadReport load = DialogueLibrary.Initialize(Directory.Exists(folder) ? folder : null);
        if (!Directory.Exists(folder))
            Console.WriteLine($"No folder '{folder}'; using the built-in files.");
        foreach (string error in load.Errors)
            Console.WriteLine(error);

        BotPersonality bot = DialoguePreview.Find(words[0]);
        if (bot == null)
        {
            Console.Error.WriteLine($"dialogue-check: no bot or type '{words[0]}'");
            return 2;
        }
        if (!Enum.TryParse(words[1], true, out eAutonomousChatIntent intent) || !Enum.IsDefined(intent))
        {
            Console.Error.WriteLine($"dialogue-check: no intent '{words[1]}'. Intents: {string.Join(", ", Enum.GetNames<eAutonomousChatIntent>().Select(name => name.ToLowerInvariant()))}");
            return 2;
        }
        var errors = new List<string>();
        DialogueFacts facts = DialoguePreview.Facts(words.Skip(2), errors);
        if (errors.Count > 0)
        {
            foreach (string error in errors)
                Console.Error.WriteLine($"dialogue-check: {error}");
            return 2;
        }

        (string pool, List<DialoguePreviewLine> lines) = DialoguePreview.Reply(bot, intent, facts, cheeky);
        string situation = words.Count > 2 ? string.Join(" ", words.Skip(2)) : "nothing special";
        Console.WriteLine($"{bot.Name}, '{pool}', situation: {situation}");
        if (lines.Count == 0)
            Console.WriteLine("  (no lines)");
        foreach (DialoguePreviewLine line in lines)
            Console.WriteLine($"  {(line.Verdict == DialoguePreview.CanSay ? "YES" : "no ")}  {line.Text.Replace("{0}", player)}{(line.Verdict == DialoguePreview.CanSay ? "" : $"   ({line.Verdict})")}");
        int canSay = lines.Count(line => line.Verdict == DialoguePreview.CanSay);
        Console.WriteLine($"{canSay} of {lines.Count} lines possible; the bot picks one at random.");
        return 0;
    }
}
