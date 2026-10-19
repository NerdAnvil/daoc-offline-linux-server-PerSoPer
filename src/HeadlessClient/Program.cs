// headless-client: logs in to a running game server the way the new (Godot) client does, without graphics, and
// checks each step: login, character list, entering the world, optional chat lines or commands, then the replies.
// Prints OK and exits 0 when every step worked; prints FAIL with the step and exits 1 otherwise.
using System;
using System.Collections.Generic;
using System.Linq;

namespace DaocServer.HeadlessClient;

public static class Program
{
    const string Usage =
        "Usage: HeadlessClient --account=NAME [--password=PW] [options]\n" +
        "  --server=HOST        server address (default 127.0.0.1)\n" +
        "  --password=PW        or set DAOC_PASSWORD instead\n" +
        "  --character=NAME     character to enter the world with (default: the first one found)\n" +
        "  --login-only         stop after the character list\n" +
        "  --send=TEXT          after entering, send a typed line (\"/persoper\", \"hello\"); repeatable\n" +
        "  --listen=SECONDS     how long to print server messages after entering (default 5)\n" +
        "  --timeout=SECONDS    how long each step may take (default 20)\n" +
        "  --verbose            print every packet code received";

    public static int Main(string[] args)
    {
        Options o;
        try { o = Options.Parse(args, Environment.GetEnvironmentVariable("DAOC_PASSWORD")); }
        catch (ArgumentException e) { Console.Error.WriteLine(e.Message + "\n" + Usage); return 2; }
        try { return Run(o); }
        catch (Exception e) when (e is TimeoutException or System.Net.Sockets.SocketException or AggregateException or System.IO.IOException)
        {
            Console.WriteLine($"FAIL connect: {e.GetBaseException().Message}");
            return 1;
        }
    }

    static int Run(Options o)
    {
        var step = TimeSpan.FromSeconds(o.TimeoutSeconds);
        using var c = new Connection(o.Server, Packets.Port, step);
        Console.WriteLine($"connected to {o.Server}:{Packets.Port}");

        // 1. Login (the new client sends the handshake and the login at once).
        c.Send(Packets.CryptKeyRequest, Packets.Handshake());
        c.Send(Packets.LoginRequest, Packets.Login(o.Account, o.Password));
        var granted = WaitFor(c, o, step, Packets.LoginGranted, Packets.LoginDenied);
        if (granted == null) return Fail("login", "no LoginGranted reply");
        if (granted.Code == Packets.LoginDenied) return Fail("login", $"LoginDenied, error code {granted.Data.FirstOrDefault()}");
        var (account, serverName) = Packets.ReadLoginGranted(granted.Data);
        Console.WriteLine($"logged in as {account} on \"{serverName}\" (session {c.SessionId})");

        // 2. Character lists, one realm at a time (the server answers them asynchronously).
        var characters = new List<Packets.Character>();
        for (int realm = 1; realm <= 3; realm++)
        {
            var list = RequestCharacters(c, o, step, realm);
            if (list == null) return Fail("characters", $"no character overview for realm {realm}");
            characters.AddRange(list);
        }
        Console.WriteLine($"{characters.Count} character(s):");
        foreach (var ch in characters)
            Console.WriteLine($"  realm {ch.Realm} slot {ch.Slot}: {ch.Name}, level {ch.Level} {ch.RaceName} {ch.ClassName}, {ch.Location}");
        if (o.LoginOnly) return Ok();

        // 3. Enter the world.
        var pick = o.Character == null ? characters.FirstOrDefault()
            : characters.FirstOrDefault(ch => string.Equals(ch.Name, o.Character, StringComparison.OrdinalIgnoreCase));
        if (pick == null)
            return Fail("enter", o.Character == null ? "the account has no characters" : $"no character named {o.Character}");
        if (RequestCharacters(c, o, step, pick.Realm) == null)            // the server enters from the realm last asked
            return Fail("enter", "no character overview before entering");
        c.Send(Packets.CharacterSelectRequest, Packets.CharacterSelect(pick.Realm, pick.Slot, pick.Name));
        c.Send(Packets.RegionListRequest, Packets.IndexOnly(pick.Realm, pick.Slot));
        c.Send(Packets.WorldInitRequest, Packets.IndexOnly(pick.Realm, pick.Slot));
        var you = WaitFor(c, o, step, Packets.PositionAndObjectId);
        if (you == null) return Fail("enter", "no PositionAndObjectID reply");
        var pos = Packets.ReadPosition(you.Data);
        Console.WriteLine($"in the world as {pick.Name}: region {pos.Region}, object {pos.ObjectId}, at {pos.X:0},{pos.Y:0},{pos.Z:0}");
        c.Send(Packets.PlayerInitRequest, Array.Empty<byte>());           // "the zone is loaded"
        c.Send(Packets.GameOpenRequest, Packets.GameOpen());

        // 4. Typed lines, then the server's messages for a while.
        foreach (var line in o.Send)
        {
            Console.WriteLine($"> {line}");
            c.Send(Packets.PlayerCommand, Packets.Command(Packets.TypedLine(line)));
        }
        var until = DateTime.UtcNow.AddSeconds(o.ListenSeconds);
        for (int i = 0; i < MaxPacketsPerWait; i++)
        {
            var p = c.Next(until);
            if (p == null) break;
            if (p.Code < 0) return Fail("listen", Packets.Text1252.GetString(p.Data));
            Note(c, o, p);
        }
        return Ok();
    }

    const int MaxPacketsPerWait = 100_000;

    // Waits for one of the codes, printing messages and noting the session id on the way; null on timeout.
    static Connection.Packet? WaitFor(Connection c, Options o, TimeSpan step, params byte[] codes)
    {
        var deadline = DateTime.UtcNow + step;
        for (int i = 0; i < MaxPacketsPerWait; i++)
        {
            var p = c.Next(deadline);
            if (p == null) return null;
            if (p.Code < 0)
            {
                Console.WriteLine($"disconnected: {Packets.Text1252.GetString(p.Data)}");
                return null;
            }
            Note(c, o, p);
            if (Array.IndexOf(codes, (byte)p.Code) >= 0) return p;
        }
        return null;
    }

    static List<Packets.Character>? RequestCharacters(Connection c, Options o, TimeSpan step, int realm)
    {
        c.Send(Packets.CharacterOverviewRequest, Packets.RequestCharacters(realm));
        var p = WaitFor(c, o, step, Packets.CharacterOverview);
        return p == null ? null : Packets.ReadOverview(p.Data, realm);
    }

    static void Note(Connection c, Options o, Connection.Packet p)
    {
        if (o.Verbose) Console.WriteLine($"  < 0x{p.Code:X2} ({p.Data.Length} bytes)");
        if (p.Code == Packets.SessionId) c.SessionId = Packets.ReadSessionId(p.Data);
        else if (p.Code == Packets.Message)
        {
            var (type, text) = Packets.ReadMessage(p.Data);
            Console.WriteLine($"  [msg {type}] {text}");
        }
    }

    static int Ok() { Console.WriteLine("OK"); return 0; }
    static int Fail(string step, string why) { Console.WriteLine($"FAIL {step}: {why}"); return 1; }
}

public sealed class Options
{
    public string Server = "127.0.0.1";
    public string Account = "";
    public string Password = "";
    public string? Character;
    public bool LoginOnly;
    public bool Verbose;
    public List<string> Send = new();
    public int ListenSeconds = 5;
    public int TimeoutSeconds = 20;

    public static Options Parse(string[] args, string? passwordFromEnvironment)
    {
        var o = new Options { Password = passwordFromEnvironment ?? "" };
        foreach (var a in args)
        {
            int eq = a.IndexOf('=');
            string key = eq < 0 ? a : a[..eq], value = eq < 0 ? "" : a[(eq + 1)..];
            switch (key)
            {
                case "--server": o.Server = value; break;
                case "--account": o.Account = value; break;
                case "--password": o.Password = value; break;
                case "--character": o.Character = value; break;
                case "--login-only": o.LoginOnly = true; break;
                case "--verbose": o.Verbose = true; break;
                case "--send": o.Send.Add(value); break;
                case "--listen": o.ListenSeconds = Seconds(key, value, 0); break;
                case "--timeout": o.TimeoutSeconds = Seconds(key, value, 1); break;
                default: throw new ArgumentException($"unknown option: {a}");
            }
        }
        if (o.Account.Length == 0) throw new ArgumentException("--account is required");
        return o;
    }

    static int Seconds(string key, string value, int min)
    {
        if (!int.TryParse(value, out int s) || s < min || s > 3600)
            throw new ArgumentException($"{key} needs a number of seconds from {min} to 3600");
        return s;
    }
}
