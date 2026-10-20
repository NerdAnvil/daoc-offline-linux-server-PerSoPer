// Writes the server's messages (chat, system lines) to a plain text file, one line each with the local time and the
// chat type, optionally only lines that mention a name. Capped at MaxLines lines and MaxBytes bytes; after that it
// stops writing and reports Full.
using System;
using System.IO;
using System.Text;

namespace DaocServer.HeadlessClient;

public sealed class ChatRecorder
{
    public const int MaxLines = 1_000, MaxBytes = 256 * 1024;

    readonly TextWriter _out;
    readonly string? _from;
    long _bytes;

    public int Lines { get; private set; }
    public bool Full { get; private set; }

    public ChatRecorder(TextWriter output, string? from)
    {
        _out = output;
        _from = string.IsNullOrWhiteSpace(from) ? null : from.Trim();
    }

    // Writes the line if it matches; false when it was left out (no match, or the file is full).
    public bool Add(DateTime when, int type, string text)
    {
        if (Full || (_from != null && !text.Contains(_from, StringComparison.OrdinalIgnoreCase))) return false;
        string line = Format(when, type, text);
        int bytes = Encoding.UTF8.GetByteCount(line) + 1;
        if (Lines >= MaxLines || _bytes + bytes > MaxBytes)
        {
            Full = true;
            return false;
        }
        _out.Write(line + "\n");
        _out.Flush();
        Lines++;
        _bytes += bytes;
        return true;
    }

    // "14:02:31 [party] [Party] Persoper: "Lead on!"", on one line whatever the text holds.
    public static string Format(DateTime when, int type, string text) =>
        $"{when:HH:mm:ss} [{TypeName(type)}] {text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ')}";

    // The server's eChatType names for the common ones (IPacketLib.cs), else the number.
    public static string TypeName(int type) => type switch
    {
        0x00 => "system",
        0x01 => "say",
        0x02 => "send",
        0x03 => "party",
        0x04 => "guild",
        0x05 => "broadcast",
        0x06 => "emote",
        0x07 => "help",
        0x08 => "chat",
        0x09 => "advice",
        0x0a => "officer",
        0x0b => "alliance",
        0x0c or 0x0d => "battlegroup",
        0x10 => "spell",
        0x1c => "important",
        0x23 => "lfg",
        0x24 => "trade",
        _ => $"type {type}",
    };
}
