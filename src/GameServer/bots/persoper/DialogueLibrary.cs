using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using DOL.Logging;

namespace DOL.GS;

/// <summary>What one load of the dialogue files did: where each file came from and what was wrong.</summary>
public sealed record DialogueLoadReport(
    int Version,
    string Folder,
    IReadOnlyList<string> FromFolder,
    IReadOnlyList<string> BuiltIn,
    IReadOnlyList<string> Errors,
    int Lines)
{
    /// <summary>One line for chat and the admin console.</summary>
    public string Summary =>
        $"Dialogue: {FromFolder.Count} files from {(Folder ?? "(no folder)")}, {BuiltIn.Count} built in, {Lines} lines, {Errors.Count} problems.";
}

/// <summary>
/// The bot dialogue files. The server reads them from the release folder's dialogue/ (set by
/// <see cref="Initialize"/>); the copies built into the server (bots/persoper/dialogue/*.dlg) are
/// the fallback. A folder file is used only when <see cref="DialogueCheck"/> finds nothing wrong
/// with it; otherwise its problems are logged and the built-in copy stays, so a broken file never
/// leaves a bot silent. <see cref="Reload"/> reads the folder again while the server runs; code that
/// keeps lines of its own builds them again through <see cref="DialogueCache{T}"/>.
/// </summary>
public static class DialogueLibrary
{
    /// <summary>Files read from the folder at most, and the largest file read.</summary>
    public const int MaxFiles = 200;
    public const long MaxFileBytes = 4 * 1024 * 1024;

    private static readonly Logger Log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly Lazy<IReadOnlyDictionary<string, DialogueFile>> BuiltInFiles = new(LoadBuiltIn, true);
    private static readonly Lock Sync = new();
    private static Snapshot _current;
    private static string _folder;

    private sealed record Snapshot(IReadOnlyDictionary<string, DialogueFile> Files, IReadOnlyDictionary<string, DialogueRule> Rules, DialogueLoadReport Report);

    private static Snapshot Current
    {
        get
        {
            Snapshot current = Volatile.Read(ref _current);
            if (current != null)
                return current;
            lock (Sync)
            {
                if (_current == null)
                    Volatile.Write(ref _current, Build(_folder, 1));
                return _current;
            }
        }
    }

    /// <summary>Goes up by one at every load; caches compare it to know when to build again.</summary>
    public static int Version => Current.Report.Version;

    /// <summary>The folder files are read from, or null (built-in files only).</summary>
    public static string Folder => _folder;

    /// <summary>What the last load did.</summary>
    public static DialogueLoadReport LastLoad => Current.Report;

    /// <summary>All loaded files, keyed by file name without ".dlg" ("joker").</summary>
    public static IReadOnlyDictionary<string, DialogueFile> All => Current.Files;

    /// <summary>The files built into the server, keyed like <see cref="All"/>.</summary>
    public static IReadOnlyDictionary<string, DialogueFile> BuiltIn => BuiltInFiles.Value;

    /// <summary>A file by name ("joker"), or null.</summary>
    public static DialogueFile For(string name) => Current.Files.GetValueOrDefault(name ?? string.Empty);

    /// <summary>
    /// The rule of a line by its text, from any file, or null when it has none. Pools mix lines
    /// from several blocks, so the text is what ties a line to its tags.
    /// </summary>
    public static DialogueRule RuleFor(string text) => Current.Rules.GetValueOrDefault(text ?? string.Empty);

    /// <summary>Server start (and the checker's preview): read the files from this folder from now on.</summary>
    public static DialogueLoadReport Initialize(string folder)
    {
        _folder = folder;
        return Reload();
    }

    /// <summary>Reads the folder again. Bots use the new lines from their next line on.</summary>
    public static DialogueLoadReport Reload()
    {
        lock (Sync)
        {
            Snapshot next = Build(_folder, (_current?.Report.Version ?? 0) + 1);
            Volatile.Write(ref _current, next);
            return next.Report;
        }
    }

    private static Snapshot Build(string folder, int version)
    {
        var files = new Dictionary<string, DialogueFile>(BuiltIn, StringComparer.OrdinalIgnoreCase);
        var fromFolder = new List<string>();
        var errors = new List<string>();
        if (folder != null && Directory.Exists(folder))
        {
            foreach (string path in Directory.EnumerateFiles(folder, "*.dlg").Order(StringComparer.Ordinal).Take(MaxFiles))
            {
                string fileName = Path.GetFileName(path);
                string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                DialogueFile file;
                try
                {
                    if (new FileInfo(path).Length > MaxFileBytes)
                    {
                        errors.Add($"{fileName}: bigger than {MaxFileBytes / (1024 * 1024)} MB; the built-in copy stays");
                        continue;
                    }
                    file = DialogueFile.Parse(File.ReadAllText(path), fileName);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    errors.Add($"{fileName}: can't be read ({exception.Message}); the built-in copy stays");
                    continue;
                }
                List<string> problems = DialogueCheck.Problems(file, name, BuiltIn.GetValueOrDefault(name));
                if (problems.Count > 0)
                {
                    errors.AddRange(problems);
                    errors.Add(BuiltIn.ContainsKey(name) ? $"{fileName}: not used; the built-in copy stays" : $"{fileName}: not used");
                    continue;
                }
                files[name] = file;
                fromFolder.Add(name);
            }
        }
        else if (folder != null)
            errors.Add($"folder {folder} not found; using the built-in files");

        var rules = new Dictionary<string, DialogueRule>(StringComparer.Ordinal);
        foreach (DialogueFile file in files.Values)
            foreach ((string text, DialogueRule rule) in file.Rules)
                if (!rules.TryAdd(text, rule))
                    errors.Add($"{file.FileName}: the tagged line '{text}' is also in another file; only the first one's tags count");

        var report = new DialogueLoadReport(version, folder, fromFolder,
            files.Keys.Except(fromFolder, StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList(),
            errors, files.Values.Sum(file => file.LineCount));
        foreach (string error in errors)
            if (Log.IsErrorEnabled)
                Log.Error($"Dialogue: {error}");
        if (Log.IsInfoEnabled)
            Log.Info(report.Summary);
        return new Snapshot(files, rules, report);
    }

    private static IReadOnlyDictionary<string, DialogueFile> LoadBuiltIn()
    {
        Assembly assembly = typeof(DialogueLibrary).Assembly;
        var files = new Dictionary<string, DialogueFile>(StringComparer.OrdinalIgnoreCase);
        foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".dlg", StringComparison.Ordinal)))
        {
            string name = resource[..^4];
            name = name[(name.LastIndexOf('.') + 1)..];
            using Stream stream = assembly.GetManifestResourceStream(resource);
            using var reader = new StreamReader(stream);
            DialogueFile file = DialogueFile.Parse(reader.ReadToEnd(), name + ".dlg");
            files[name] = file;
            foreach (string error in file.Errors)
                if (Log.IsErrorEnabled)
                    Log.Error($"Dialogue: built in: {error}");
        }
        return files;
    }
}

/// <summary>
/// A value built from the dialogue files (a bot's line pools, the intent phrases) that is built
/// again on first use after <see cref="DialogueLibrary.Reload"/>.
/// </summary>
public sealed class DialogueCache<T>(Func<T> build) where T : class
{
    private sealed record Entry(int Version, T Value);

    private Entry _entry;

    public T Value
    {
        get
        {
            int version = DialogueLibrary.Version;
            Entry entry = Volatile.Read(ref _entry);
            if (entry == null || entry.Version != version)
            {
                entry = new Entry(version, build());
                Volatile.Write(ref _entry, entry);
            }
            return entry.Value;
        }
    }
}
