// headless-client: logs in to a running game server the way the new (Godot) client does, without graphics, and
// checks each step: login, character list, optionally creating a character, entering the world, optional chat lines
// or commands, then the replies and who is nearby, and optionally /quit and deleting the created (or a named level 1)
// character.
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
        "  --login-only         stop after the character list (and --create, if given)\n" +
        "  --create=NAME        create a character, then enter the world with it; needs --realm, --race and --class\n" +
        "  --realm=REALM        albion, midgard or hibernia (or alb, mid, hib, 1-3)\n" +
        "  --race=RACE          Briton, Avalonian, Highlander, Saracen, Norseman, Troll, Dwarf, Kobold, Celt,\n" +
        "                       Firbolg, Elf, Lurikeen, Inconnu, Valkyn or Sylvan\n" +
        "  --class=CLASS        a starting class by name (Armsman, Warrior, Hero, ...) or the server's class number\n" +
        "  --female             create a female character (default male)\n" +
        "  --delete=NAME        delete a level 1 character: alone, right after the character list (no world);\n" +
        "                       with --create of the same name, at the end, after /quit back to the character screen\n" +
        "  --send=TEXT          after entering, send a typed line (\"/persoper\", \"hello\"); repeatable\n" +
        "  --listen=SECONDS     how long to print server messages after entering, then who is nearby (default 5)\n" +
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

        // Optional: create a character, or delete one without entering the world.
        if (o.Create != null)
        {
            var created = Create(c, o, step, characters);
            if (created == null) return 1;
            characters.Add(created);
        }
        else if (o.Delete != null)
            return Delete(c, o, step, characters) ? Ok() : 1;
        if (o.LoginOnly) return Ok();

        // 3. Enter the world (with the new character, if one was created).
        string? wanted = o.Create ?? o.Character;
        var pick = wanted == null ? characters.FirstOrDefault()
            : characters.FirstOrDefault(ch => Same(ch.Name, wanted));
        if (pick == null)
            return Fail("enter", wanted == null ? "the account has no characters" : $"no character named {wanted}");
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
        PrintNearby(pos);

        // 5. Optional: back to the character screen the way the new client leaves (/quit: sit, the server's
        // countdown of up to 20 s, more after a fight, then Quit), and delete the character created above.
        if (o.Delete != null)
        {
            Console.WriteLine("> /quit");
            c.Send(Packets.PlayerCommand, Packets.Command(Packets.TypedLine("/quit")));
            if (WaitFor(c, o, TimeSpan.FromSeconds(QuitWaitSeconds), Packets.Quit) == null)
                return Fail("quit", $"no Quit reply within {QuitWaitSeconds} s");
            Console.WriteLine("back at the character screen");
            if (!Delete(c, o, step, characters)) return 1;
        }
        return Ok();
    }

    const int MaxPacketsPerWait = 100_000;
    const int MaxNearby = 5_000;

    // Players and NPCs (bots included) in view, by object id: added on NPCCreate / PlayerCreate, dropped when the
    // server says they left view.
    static readonly Dictionary<int, Packets.Living> Nearby = new();

    static void PrintNearby(Packets.Position you)
    {
        var list = Nearby.Values.OrderBy(l => l.DistanceTo(you)).ToList();
        Console.WriteLine($"nearby: {list.Count} (distance from where you entered, to where each was first seen)");
        foreach (var l in list)
        {
            string guild = l.Guild.Length > 0 ? $" <{l.Guild}>" : "";
            Console.WriteLine($"  {(l.IsPlayer ? "player" : "npc   ")} {l.Name}{guild}, level {l.Level}, {l.DistanceTo(you):0} away");
        }
    }
    const int QuitWaitSeconds = 90;

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // Creates the --create character in the realm's first free slot, then checks the realm's list for it (the server
    // answers a refused creation the same way as a good one: LoginGranted, with the reason only in its log).
    static Packets.Character? Create(Connection c, Options o, TimeSpan step, List<Packets.Character> characters)
    {
        string name = o.Create!;
        if (characters.Any(ch => Same(ch.Name, name)))
            return FailNull("create", $"the account already has a character named {name}");
        var before = RequestCharacters(c, o, step, o.Realm);              // also tells the server which realm
        if (before == null) return FailNull("create", $"no character overview for realm {o.Realm}");
        int slot = Enumerable.Range(0, 10).FirstOrDefault(s => before.All(ch => ch.Slot != s), -1);
        if (slot < 0) return FailNull("create", $"realm {o.Realm} has no free character slot");

        var race = Packets.Races.First(r => r.Id == o.RaceId);
        Console.WriteLine($"creating {name}: realm {o.Realm} slot {slot}, {(o.Female ? "female" : "male")} {race.Name}, class {o.ClassId}");
        c.Send(Packets.CharacterCreateRequest, Packets.CreateCharacter(o.Realm, slot, name, o.ClassId, race.Id, o.Female,
            o.Female ? race.FemaleModel : race.MaleModel, Packets.CreationStats(race.Id)));

        var after = RequestCharacters(c, o, step, o.Realm);
        if (after == null) return FailNull("create", $"no character overview for realm {o.Realm} after creating");
        var made = after.FirstOrDefault(ch => Same(ch.Name, name));
        if (made == null)
            return FailNull("create", "the server refused it (its logs/server.log says why: name, race/class, ...)");
        Console.WriteLine($"created {made.Name}: realm {made.Realm} slot {made.Slot}, level {made.Level} {made.RaceName} {made.ClassName}, {made.Location}");
        return made;
    }

    // Deletes the --delete character from the character screen, then checks its realm's list no longer has it.
    // Only level 1 characters, so a typing slip cannot delete a played one.
    static bool Delete(Connection c, Options o, TimeSpan step, List<Packets.Character> characters)
    {
        string name = o.Delete!;
        var known = characters.FirstOrDefault(ch => Same(ch.Name, name));
        if (known == null) return FailFalse("delete", $"no character named {name}");
        var list = RequestCharacters(c, o, step, known.Realm);             // the server deletes in the realm last asked
        var target = list?.FirstOrDefault(ch => Same(ch.Name, name));
        if (target == null) return FailFalse("delete", $"{name} is not in realm {known.Realm}'s list");
        if (target.Level > 1) return FailFalse("delete", $"{name} is level {target.Level}; only level 1 characters are deleted");

        Console.WriteLine($"deleting {target.Name}: realm {target.Realm} slot {target.Slot}");
        c.Send(Packets.CharacterCreateRequest, Packets.DeleteCharacter(target.Realm, target.Slot));
        var after = RequestCharacters(c, o, step, target.Realm);
        if (after == null) return FailFalse("delete", $"no character overview for realm {target.Realm} after deleting");
        if (after.Any(ch => Same(ch.Name, name)))
            return FailFalse("delete", "the server kept it (its logs/server.log may say why, e.g. the character owns a house)");
        Console.WriteLine($"deleted {target.Name}");
        return true;
    }

    static Packets.Character? FailNull(string step, string why) { Fail(step, why); return null; }
    static bool FailFalse(string step, string why) { Fail(step, why); return false; }

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
        else if (p.Code is Packets.NpcCreate or Packets.PlayerCreate)
        {
            var l = p.Code == Packets.NpcCreate ? Packets.ReadNpcCreate(p.Data) : Packets.ReadPlayerCreate(p.Data);
            if (Nearby.Count < MaxNearby || Nearby.ContainsKey(l.ObjectId)) Nearby[l.ObjectId] = l;
        }
        else if (p.Code is Packets.ObjectDelete or Packets.RemoveObject)
            Nearby.Remove(Packets.ReadRemovedObjectId(p.Data));
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
    public string? Create, Delete;
    public int Realm, RaceId, ClassId;
    public bool Female;

    public static Options Parse(string[] args, string? passwordFromEnvironment)
    {
        var o = new Options { Password = passwordFromEnvironment ?? "" };
        string? realm = null, race = null, cls = null;
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
                case "--create": o.Create = value; break;
                case "--delete": o.Delete = value; break;
                case "--realm": realm = value; break;
                case "--race": race = value; break;
                case "--class": cls = value; break;
                case "--female": o.Female = true; break;
                default: throw new ArgumentException($"unknown option: {a}");
            }
        }
        if (o.Account.Length == 0) throw new ArgumentException("--account is required");
        if (o.Create != null)
        {
            if (!Packets.IsValidName(o.Create))
                throw new ArgumentException("--create needs a name of 3 to 23 letters starting with a capital (\"Testa\")");
            if (realm == null || race == null || cls == null)
                throw new ArgumentException("--create needs --realm, --race and --class");
            o.Realm = ParseRealm(realm);
            o.RaceId = Packets.Races.FirstOrDefault(r => string.Equals(r.Name, race, StringComparison.OrdinalIgnoreCase)
                || (r.Id == 5 && string.Equals(race, "Norse", StringComparison.OrdinalIgnoreCase)))?.Id
                ?? throw new ArgumentException($"unknown race: {race}");
            o.ClassId = Packets.ClassIds.TryGetValue(cls, out int id) ? id
                : int.TryParse(cls, out id) && id is > 0 and < 256 ? id
                : throw new ArgumentException($"unknown class: {cls}");
            if (o.Character != null && !string.Equals(o.Character, o.Create, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("--character and --create name different characters");
        }
        else if (realm != null || race != null || cls != null || o.Female)
            throw new ArgumentException("--realm, --race, --class and --female go with --create");
        if (o.Delete != null && o.Create != null && !string.Equals(o.Delete, o.Create, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("with --create, --delete must name the same character");
        if (o.Delete is "") throw new ArgumentException("--delete needs a character name");
        if (o.LoginOnly && o.Create != null && o.Delete != null)
            throw new ArgumentException("--login-only stops before the world, so --create with --delete would never delete");
        return o;
    }

    static int ParseRealm(string value) => value.ToLowerInvariant() switch
    {
        "albion" or "alb" or "1" => 1,
        "midgard" or "mid" or "2" => 2,
        "hibernia" or "hib" or "3" => 3,
        _ => throw new ArgumentException($"unknown realm: {value} (albion, midgard or hibernia)"),
    };

    static int Seconds(string key, string value, int min)
    {
        if (!int.TryParse(value, out int s) || s < min || s > 3600)
            throw new ArgumentException($"{key} needs a number of seconds from {min} to 3600");
        return s;
    }
}
