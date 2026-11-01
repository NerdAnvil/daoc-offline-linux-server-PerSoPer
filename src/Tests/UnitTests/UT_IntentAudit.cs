using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace DOL.GS.Tests;

/// <summary>
/// Read-rate audit: how many realistic player lines the intent model reads as the intent a
/// sensible player means. A baseline, not a pass mark: it fails only on a broken set. Each set is
/// an embedded UnitTests/Data/intent-audit-*.json. See the report with:
/// dotnet test --filter UT_IntentAudit --logger "console;verbosity=detailed"
/// </summary>
[TestFixture]
public class UT_IntentAudit
{
    private const int MaxLines = 2000;

    private sealed record Line(string Text, string Expect, string To, string Note);

    private static IEnumerable<string> Sets() =>
        typeof(UT_IntentAudit).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("intent-audit-", StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);

    [Test]
    public void TheTunedSetIsThere()
    {
        Assert.That(Sets(), Does.Contain("intent-audit-tuned.json"));
    }

    [TestCaseSource(nameof(Sets))]
    public void ReadRate(string set)
    {
        List<Line> lines = Load(set);
        Assert.That(lines, Is.Not.Empty, $"{set} has no lines");

        var known = new List<(Line Line, eAutonomousChatIntent Expect, eAutonomousChatIntent Read)>();
        var unmapped = new List<(Line Line, eAutonomousChatIntent Read)>();
        foreach (Line line in lines)
        {
            Assert.That(line.Text, Is.Not.Empty, $"{set}: a line has no text");
            Assert.That(line.Expect, Is.Not.Empty, $"{set}: '{line.Text}' has no expect");
            eAutonomousChatIntent read = AutonomousChatIntentModel.Read(line.Text);
            if (Enum.TryParse(line.Expect, true, out eAutonomousChatIntent expect) && Enum.IsDefined(expect) &&
                expect != eAutonomousChatIntent.Unknown)
                known.Add((line, expect, read));
            else
                unmapped.Add((line, read));
        }

        var report = new StringBuilder();
        int right = known.Count(entry => entry.Read == entry.Expect);
        report.AppendLine($"Intent audit, {set}: {right} of {known.Count} read right ({Percent(right, known.Count)}); " +
                          $"{unmapped.Count} more lines mean something with no intent yet.");
        report.AppendLine();
        report.AppendLine($"{"intent",-18}{"lines",6}{"right",7}{"unknown",9}{"wrong",7}{"rate",7}");
        foreach (IGrouping<eAutonomousChatIntent, (Line Line, eAutonomousChatIntent Expect, eAutonomousChatIntent Read)> group in
                 known.GroupBy(entry => entry.Expect).OrderBy(group => group.Key.ToString(), StringComparer.Ordinal))
        {
            int good = group.Count(entry => entry.Read == entry.Expect);
            int unknown = group.Count(entry => entry.Read == eAutonomousChatIntent.Unknown);
            report.AppendLine($"{group.Key.ToString().ToLowerInvariant(),-18}{group.Count(),6}{good,7}{unknown,9}{group.Count() - good - unknown,7}{Percent(good, group.Count()),7}");
        }

        if (unmapped.Count > 0)
        {
            report.AppendLine();
            report.AppendLine("No intent yet (meaning: read as now):");
            foreach (IGrouping<string, (Line Line, eAutonomousChatIntent Read)> group in
                     unmapped.GroupBy(entry => entry.Line.Expect.ToLowerInvariant()).OrderBy(group => group.Key, StringComparer.Ordinal))
                report.AppendLine($"  {group.Key} ({group.Count()}): " +
                                  string.Join(", ", group.Select(entry => $"'{entry.Line.Text}' -> {Name(entry.Read)}")));
        }

        report.AppendLine();
        report.AppendLine("Misses (expected: line -> read as):");
        foreach ((Line line, eAutonomousChatIntent expect, eAutonomousChatIntent read) in known.Where(entry => entry.Read != entry.Expect)
                     .OrderBy(entry => entry.Expect.ToString(), StringComparer.Ordinal))
            report.AppendLine($"  {Name(expect)}: '{line.Text}' -> {Name(read)}" +
                              (line.To != null ? $"  [to {line.To}]" : "") + (line.Note != null ? $"  ({line.Note})" : ""));

        TestContext.Out.Write(report.ToString());
    }

    private static string Name(eAutonomousChatIntent intent) => intent.ToString().ToLowerInvariant();

    private static string Percent(int part, int whole) => whole == 0 ? "-" : $"{100 * part / whole}%";

    private static List<Line> Load(string set)
    {
        using Stream stream = typeof(UT_IntentAudit).Assembly.GetManifestResourceStream(set);
        Assert.That(stream, Is.Not.Null, $"{set} is not embedded");
        using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var lines = new List<Line>();
        foreach (JsonElement element in document.RootElement.GetProperty("lines").EnumerateArray().Take(MaxLines))
            lines.Add(new Line(Text(element, "text"), Text(element, "expect"), Text(element, "to"), Text(element, "note")));
        return lines;
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
