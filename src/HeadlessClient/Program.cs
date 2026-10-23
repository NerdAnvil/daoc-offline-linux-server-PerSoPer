// headless-client: logs in to a running game server the way the new (Godot) client does, without graphics, and
// checks each step: login, character list, optionally creating a character, entering the world, optional chat lines
// or commands, then the replies and who is nearby, optionally walking to a spot or someone nearby, optionally recording
// chat to a file for a while (optionally following someone), then /quit and optionally deleting the created (or a named
// level 1) character. It keeps track of where the server puts it (/tele, portals), so walking starts from there.
// Prints OK and exits 0 when every step worked; prints FAIL with the step and exits 1 otherwise.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DaocServer.HeadlessClient;

public static class Program
{
    const string Usage =
        "Usage: HeadlessClient --account=NAME [--password=PW] [options]\n" +
        "  --server=127.0.0.1   the only address allowed (the default); other addresses are refused\n" +
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
        "  --goto=NAME|X,Y      then walk to someone nearby (\"Persoper\": full name, first name or part of it) or to a\n" +
        "                       spot in the region; stops within 150 units, or fails after 120 s\n" +
        "  --record=FILE        write the server's messages from entering the world on to FILE (time, type, text), and\n" +
        "                       after --listen and --goto keep listening for --record-seconds; at most 1000 lines, 256 KB\n" +
        "  --record-seconds=N   how long to keep recording at the end (default 30)\n" +
        "  --record-from=NAME   record only lines that mention NAME (\"Persoper\")\n" +
        "  --follow=NAME        during --listen and --record, walk back to someone nearby (as --goto) whenever they are\n" +
        "                       more than 250 units away, so they stay in /say range; prints how far they got. If a\n" +
        "                       --send was a /tele, also /tele NAME again when they are out of view or 400+ away\n" +
        "  --overwrite          let --record replace an existing file\n" +
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
        // Open the --record file first, so a bad path or an existing file fails before logging in.
        StreamWriter? file = null;
        if (o.Record != null)
        {
            try { file = new StreamWriter(new FileStream(o.Record, o.Overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return Fail("record", File.Exists(o.Record) && !o.Overwrite
                    ? $"{o.Record} already exists (add --overwrite to replace it)" : e.Message);
            }
            Recorder = new ChatRecorder(file, o.RecordFrom);
        }
        using var recordFile = file;
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
        Me = Packets.ReadPosition(you.Data);
        WorldInit = Packets.IndexOnly(pick.Realm, pick.Slot);
        InWorld = true;
        Console.WriteLine($"in the world as {pick.Name}: region {Me.Region}, object {Me.ObjectId}, at {Me.X:0},{Me.Y:0},{Me.Z:0}");
        c.Send(Packets.PlayerInitRequest, Array.Empty<byte>());           // "the zone is loaded"
        c.Send(Packets.GameOpenRequest, Packets.GameOpen());

        // In the world from here on: whatever happens, leave with /quit, so the next login isn't refused while this
        // character stays linkdead (LoginDenied 17 for about a minute).
        int result = Play(c, o);
        Recorder = null;                                                     // nothing more after the recording time
        Console.WriteLine("> /quit");
        c.Send(Packets.PlayerCommand, Packets.Command(Packets.TypedLine("/quit")));
        if (WaitFor(c, o, TimeSpan.FromSeconds(QuitWaitSeconds), Packets.Quit) == null)
            return result != 0 ? result : Fail("quit", $"no Quit reply within {QuitWaitSeconds} s");
        InWorld = false;
        // The server counts you as playing (and a closed connection as linkdead) until the character list is asked for.
        if (RequestCharacters(c, o, step, pick.Realm) == null)
            return result != 0 ? result : Fail("quit", "no character overview after Quit");
        Console.WriteLine("back at the character screen");

        // Optional: delete the character created above (also after a failed step, so test characters don't pile up).
        if (o.Delete != null && !Delete(c, o, step, characters)) return 1;
        return result != 0 ? result : Ok();
    }

    // Everything between entering the world and /quit; 0 when it all worked, else 1 after printing FAIL.
    static int Play(Connection c, Options o)
    {
        // 4. Typed lines, then the server's messages for a while.
        foreach (var line in o.Send)
        {
            Console.WriteLine($"> {line}");
            c.Send(Packets.PlayerCommand, Packets.Command(Packets.TypedLine(line)));
        }
        var follow = o.Follow == null ? null : new FollowStats();
        if (!Listen(c, o, DateTime.UtcNow.AddSeconds(o.ListenSeconds), follow)) return Fail("listen", "disconnected");
        PrintNearby(Me);
        if (o.Goto != null)
        {
            string? why = Walk(c, o, o.GotoName, o.GotoX, o.GotoY, DateTime.UtcNow.AddSeconds(MaxWalkSeconds), true);
            if (why != null) return Fail("goto", why);
        }
        if (Recorder != null)
        {
            string path = Path.GetFullPath(o.Record!);
            Console.WriteLine($"recording for {o.RecordSeconds} s to {path}");
            if (!Listen(c, o, DateTime.UtcNow.AddSeconds(o.RecordSeconds), follow)) return Fail("record", "disconnected");
            Console.WriteLine($"recorded {Recorder.Lines} line(s) to {path}{(Recorder.Full ? " (full: stopped at the limit)" : "")}");
        }
        if (follow != null) Console.WriteLine($"follow {o.Follow}: {follow}");
        // 5. Back to the character screen the way the new client leaves (Run sends /quit: sit, the server's countdown
        // of up to 20 s, more after a fight, then Quit).
        return 0;
    }

    // Reads packets until the deadline; with --follow, checks every FollowCheckMilliseconds how far the one followed is and
    // walks back to them when they are more than FollowStartDistance away, or, if the run used /tele, teleports to them
    // again when they got out of view or more than FollowTeleDistance away (a hunting bot runs at about 480 units a
    // second between fights, far faster than walking; walking still covers the slow moments). Capped in
    // checks, walks and teleports. False if the connection ended.
    static bool Listen(Connection c, Options o, DateTime until, FollowStats? follow)
    {
        if (follow == null) return Pump(c, o, until);
        bool canTele = o.Send.Any(line => line.StartsWith("/tele ", StringComparison.OrdinalIgnoreCase));
        for (int i = 0; i < MaxFollowChecks && DateTime.UtcNow < until; i++)
        {
            var next = DateTime.UtcNow.AddMilliseconds(FollowCheckMilliseconds);
            if (!Pump(c, o, next < until ? next : until)) return false;
            if (DateTime.UtcNow >= until) break;
            var target = FindNearby(Nearby.Values, o.Follow!, Me);
            double? distance = target?.DistanceTo(Me);
            follow.Check(distance);
            var action = Decide(distance, canTele && follow.Teles < MaxFollowTeles, follow.Walks < MaxFollowWalks);
            if (action == FollowAction.Teleport)
            {
                follow.Teles++;
                string line = $"/tele {o.Follow}";
                Console.WriteLine($"  follow: {(distance == null ? "not in view" : $"{distance:0} away")}; > {line}");
                c.Send(Packets.PlayerCommand, Packets.Command(Packets.TypedLine(line)));
            }
            else if (action == FollowAction.Walk)
            {
                follow.Walks++;
                var walkUntil = DateTime.UtcNow.AddSeconds(MaxFollowWalkSeconds);
                string? why = Walk(c, o, target!.Name, 0, 0, walkUntil < until ? walkUntil : until, false, follow,
                    canTele ? FollowTeleDistance : double.MaxValue);
                if (why == "disconnected") return false;
                if (why != null) Console.WriteLine($"  follow: {why}");
            }
        }
        return Pump(c, o, until);                                            // after the check cap: just listen
    }

    // --follow: walk when the one followed is further than FollowStartDistance, teleport when further than
    // FollowTeleDistance; both inside the server's /say range (WorldMgr.SAY_DISTANCE). Checking twice a second, a bot
    // running at 480 units a second gets about 240 further between checks.
    public const int FollowStartDistance = 250, SayDistance = 512, FollowTeleDistance = 400;
    const int FollowCheckMilliseconds = 500, MaxFollowChecks = 8_000, MaxFollowWalks = 1_000, MaxFollowTeles = 1_000,
        MaxFollowWalkSeconds = 30;
    public static bool NeedsWalk(double distance) => distance > FollowStartDistance;

    public enum FollowAction { Wait, Walk, Teleport }

    // What --follow does after a check (distance null: not in view).
    public static FollowAction Decide(double? distance, bool mayTele, bool mayWalk)
    {
        if (distance == null || distance > FollowTeleDistance)
            return mayTele ? FollowAction.Teleport : distance != null && mayWalk ? FollowAction.Walk : FollowAction.Wait;
        return NeedsWalk(distance.Value) && mayWalk ? FollowAction.Walk : FollowAction.Wait;
    }

    // How --follow went: how often they were seen, out of /say range or out of view, and how far they got.
    public sealed class FollowStats
    {
        public int Checks, Unseen, OutOfRange, Walks, Teles;
        public double Furthest;

        public void Check(double? distance)
        {
            Checks++;
            if (distance == null) { Unseen++; return; }
            if (distance > SayDistance) OutOfRange++;
            Furthest = Math.Max(Furthest, distance.Value);
        }

        public override string ToString() =>
            $"{Checks} check(s), {Walks} walk(s), {Teles} teleport(s), furthest {Furthest:0}, out of /say range ({SayDistance}) {OutOfRange}, not in view {Unseen}";
    }

    const int MaxPacketsPerWait = 100_000;
    const int MaxNearby = 5_000;

    // Players and NPCs (bots included) in view, by object id: added on NPCCreate / PlayerCreate, dropped when the
    // server says they left view.
    static readonly Dictionary<int, Packets.Living> Nearby = new();

    static void PrintNearby(Packets.Position you)
    {
        var list = Nearby.Values.OrderBy(l => l.DistanceTo(you)).ToList();
        Console.WriteLine($"nearby: {list.Count} (distance from where you are, to where each was last seen)");
        foreach (var l in list)
        {
            string guild = l.Guild.Length > 0 ? $" <{l.Guild}>" : "";
            Console.WriteLine($"  {(l.IsPlayer ? "player" : "npc   ")} {l.Name}{guild}, level {l.Level}, {l.DistanceTo(you):0} away");
        }
    }
    const int QuitWaitSeconds = 90;

    // --record: messages are written from entering the world on.
    static ChatRecorder? Recorder;
    static bool InWorld;

    // Where you are: from entering the world, then your own walking and the server's moves (CharacterJump, and
    // PositionAndObjectID again after a region change).
    static Packets.Position Me = new(0, 0, 0, 0, 0);
    static byte[] WorldInit = Array.Empty<byte>();                   // the enter-world request, sent again on a region change

    // Zone corners in region coordinates, learned from ObjectUpdates (the client has no zone files).
    static readonly Dictionary<int, (int X, int Y)> ZoneCorners = new();

    const float WalkSpeed = 191;                                   // GamePlayer.PLAYER_BASE_SPEED, units a second
    const int StepMilliseconds = 200, MaxWalkSeconds = 120, ZoneWaitSeconds = 15;
    const int ChecksEverySteps = 1000 / StepMilliseconds;                // --follow counts one check a second while walking
    public const int ArriveDistance = 150;

    // Walks in a straight line to someone nearby (followed as ObjectUpdates move them; name null: to x, y) from where
    // you are, sending the position the way the client does while running, until within ArriveDistance or the deadline.
    // Picks up the server's moves on the way (Me). With `follow`, counts a check each second on the way, and gives up
    // when the one walked to is further than giveUpBeyond. Returns null on arrival, else why not ("disconnected" if so).
    static string? Walk(Connection c, Options o, string? name, float gotoX, float gotoY, DateTime deadline, bool chatty,
        FollowStats? follow = null, double giveUpBeyond = double.MaxValue)
    {
        Packets.Living? target = null;
        if (name != null)
        {
            target = FindNearby(Nearby.Values, name, Me);
            if (target == null) return $"nobody called {name} nearby";
        }

        // The position update names your zone; take the nearest living's, once the server has placed one.
        var zoneDeadline = DateTime.UtcNow.AddSeconds(ZoneWaitSeconds);
        int zone = ZoneNear(Me);
        while (zone < 0 && DateTime.UtcNow < zoneDeadline)
        {
            if (!Pump(c, o, DateTime.UtcNow.AddMilliseconds(StepMilliseconds))) return "disconnected";
            zone = ZoneNear(Me);
        }
        if (zone < 0) return $"no ObjectUpdate within {ZoneWaitSeconds} s, so your zone is unknown";

        string what = target == null ? $"{gotoX:0},{gotoY:0}" : target.Name;
        if (chatty) Console.WriteLine($"walking to {what} (zone {zone})");
        double distance = double.MaxValue;
        int maxSteps = MaxWalkSeconds * 1000 / StepMilliseconds;
        for (int i = 0; i < maxSteps && DateTime.UtcNow < deadline; i++)
        {
            if (!Pump(c, o, DateTime.UtcNow.AddMilliseconds(StepMilliseconds))) return "disconnected";
            float tx = gotoX, ty = gotoY, tz = Me.Z;
            if (target != null)
            {
                if (!Nearby.TryGetValue(target.ObjectId, out var now))       // out of view and back: a new object id
                    now = FindNearby(Nearby.Values, target.Name, Me);
                if (now == null) { Stop(c, zone, 0); return $"{target.Name} left view"; }
                target = now;
                (tx, ty, tz) = (now.X, now.Y, now.Z);
            }
            distance = Math.Sqrt((tx - Me.X) * (tx - Me.X) + (ty - Me.Y) * (ty - Me.Y));
            if (follow != null && i % ChecksEverySteps == ChecksEverySteps - 1) follow.Check(target?.DistanceTo(Me) ?? distance);
            if (distance > giveUpBeyond) { Stop(c, zone, 0); return $"{what} is {distance:0} away, too far to walk"; }
            if (distance <= ArriveDistance)
            {
                Stop(c, zone, Packets.Heading(Me.X, Me.Y, tx, ty));
                Console.WriteLine($"{(chatty ? "" : "  follow: ")}arrived: {what} is {distance:0} away (within {ArriveDistance}), at {Me.X:0},{Me.Y:0},{Me.Z:0}");
                return null;
            }
            int heading = Packets.Heading(Me.X, Me.Y, tx, ty);
            var (x, y, z) = StepToward((Me.X, Me.Y, Me.Z), (tx, ty, tz), WalkSpeed * StepMilliseconds / 1000f);
            Me = Me with { X = x, Y = y, Z = z };
            int near = ZoneNear(Me);                                         // crossing into the next zone
            if (near >= 0) zone = near;
            c.Send(Packets.PositionUpdate, Packets.PositionUpdateBody(x, y, z, WalkSpeed, c.SessionId, Me.ObjectId, zone, heading));
            if (chatty && i % 25 == 24) Console.WriteLine($"  walking: {distance:0} away");
        }
        Stop(c, zone, 0);
        return $"still {distance:0} away from {what} when the walking time ran out";
    }

    // Standing still where you are.
    static void Stop(Connection c, int zone, int heading) =>
        c.Send(Packets.PositionUpdate, Packets.PositionUpdateBody(Me.X, Me.Y, Me.Z, 0, c.SessionId, Me.ObjectId, zone, heading));

    // One step of at most `step` units in a straight line (x/y distance; z follows along), not past the target.
    public static (float X, float Y, float Z) StepToward((float X, float Y, float Z) from, (float X, float Y, float Z) to, float step)
    {
        double distance = Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y));
        if (distance <= step) return to;
        double f = step / distance;
        return ((float)(from.X + (to.X - from.X) * f), (float)(from.Y + (to.Y - from.Y) * f), (float)(from.Z + (to.Z - from.Z) * f));
    }

    // Someone nearby by name, closest first: the full name, else the first name ("Persoper" for "Persoper Mendsalot"),
    // else any name containing it.
    public static Packets.Living? FindNearby(IEnumerable<Packets.Living> nearby, string name, Packets.Position you)
    {
        var byDistance = nearby.OrderBy(l => l.DistanceTo(you)).ToList();
        return byDistance.FirstOrDefault(l => Same(l.Name, name))
            ?? byDistance.FirstOrDefault(l => Same(l.Name.Split(' ')[0], name))
            ?? byDistance.FirstOrDefault(l => l.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    static int ZoneNear(Packets.Position you) =>
        Nearby.Values.Where(l => l.Zone >= 0).OrderBy(l => l.DistanceTo(you)).Select(l => l.Zone).DefaultIfEmpty(-1).First();

    // Reads packets until the deadline (capped); false if the connection ended.
    static bool Pump(Connection c, Options o, DateTime deadline)
    {
        for (int i = 0; i < MaxPacketsPerWait; i++)
        {
            var p = c.Next(deadline);
            if (p == null) return true;
            if (p.Code < 0) return false;
            Note(c, o, p);
        }
        return true;
    }

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
            if (Recorder != null && InWorld && !Recorder.Full)
            {
                Recorder.Add(DateTime.Now, type, text);
                if (Recorder.Full) Console.WriteLine($"  (record file full at {Recorder.Lines} lines; no more lines written)");
            }
        }
        else if (p.Code is Packets.NpcCreate or Packets.PlayerCreate)
        {
            var l = p.Code == Packets.NpcCreate ? Packets.ReadNpcCreate(p.Data) : Packets.ReadPlayerCreate(p.Data);
            if (Nearby.Count < MaxNearby || Nearby.ContainsKey(l.ObjectId)) Nearby[l.ObjectId] = l;
        }
        else if (p.Code == Packets.CharacterJump && InWorld)
        {
            var jump = Packets.ReadJump(p.Data);
            if (jump == null) return;                                        // heading only
            Me = Me with { X = jump.Value.X, Y = jump.Value.Y, Z = jump.Value.Z };
            Console.WriteLine($"  moved by the server to {Me.X:0},{Me.Y:0},{Me.Z:0}");
        }
        else if (p.Code == Packets.RegionChanged && InWorld)
        {
            // Into another region: everything in view is gone; ask for the world again, as the client does after loading.
            Nearby.Clear();
            ZoneCorners.Clear();
            Console.WriteLine("  region changed; entering it");
            c.Send(Packets.WorldInitRequest, WorldInit);
        }
        else if (p.Code == Packets.PositionAndObjectId && InWorld)
        {
            Me = Packets.ReadPosition(p.Data);
            Console.WriteLine($"  now in region {Me.Region}, object {Me.ObjectId}, at {Me.X:0},{Me.Y:0},{Me.Z:0}");
            c.Send(Packets.PlayerInitRequest, Array.Empty<byte>());
            c.Send(Packets.GameOpenRequest, Packets.GameOpen());              // playing again: objects in view come now
        }
        else if (p.Code is Packets.ObjectDelete or Packets.RemoveObject)
            Nearby.Remove(Packets.ReadRemovedObjectId(p.Data));
        else if (p.Code == Packets.ObjectUpdate)
        {
            var m = Packets.ReadObjectUpdate(p.Data);
            if (Nearby.TryGetValue(m.ObjectId, out var l))
            {
                if (!ZoneCorners.TryGetValue(m.Zone, out var corner))
                    ZoneCorners[m.Zone] = corner = Packets.ZoneCorner(l.X, l.Y, m.ZoneX, m.ZoneY);
                Nearby[m.ObjectId] = l with { X = corner.X + m.ZoneX, Y = corner.Y + m.ZoneY, Z = m.Z, Zone = m.Zone };
            }
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
    public string? Create, Delete;
    public string? Goto, GotoName;                             // GotoName null: walk to GotoX, GotoY
    public float GotoX, GotoY;
    public int Realm, RaceId, ClassId;
    public bool Female;
    public string? Record, RecordFrom, Follow;
    public int RecordSeconds = 30;
    public bool Overwrite;

    public static Options Parse(string[] args, string? passwordFromEnvironment)
    {
        var o = new Options { Password = passwordFromEnvironment ?? "" };
        string? realm = null, race = null, cls = null;
        bool recordOption = false;
        foreach (var a in args)
        {
            int eq = a.IndexOf('=');
            string key = eq < 0 ? a : a[..eq], value = eq < 0 ? "" : a[(eq + 1)..];
            switch (key)
            {
                case "--server":
                    if (value != "127.0.0.1")
                        throw new ArgumentException("--server must be 127.0.0.1: this client only talks to the local test server");
                    o.Server = value; break;
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
                case "--goto": o.Goto = value; break;
                case "--record": o.Record = value; break;
                case "--follow": o.Follow = value.Trim(); break;
                case "--record-seconds": o.RecordSeconds = Seconds(key, value, 0); recordOption = true; break;
                case "--record-from": o.RecordFrom = value; recordOption = true; break;
                case "--overwrite": o.Overwrite = true; recordOption = true; break;
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
        if (o.Goto != null)
        {
            var xy = o.Goto.Split(',');
            if (o.Goto.Trim().Length == 0) throw new ArgumentException("--goto needs a name or x,y");
            if (xy.Length == 2 && float.TryParse(xy[0], System.Globalization.CultureInfo.InvariantCulture, out o.GotoX)
                && float.TryParse(xy[1], System.Globalization.CultureInfo.InvariantCulture, out o.GotoY))
            {
                if (o.GotoX is < 0 or > 2_000_000 || o.GotoY is < 0 or > 2_000_000)
                    throw new ArgumentException("--goto x,y must be region coordinates (0 to 2000000)");
            }
            else if (xy.Length > 1) throw new ArgumentException("--goto x,y needs two numbers");
            else o.GotoName = o.Goto.Trim();
            if (o.LoginOnly) throw new ArgumentException("--goto needs the world, so not with --login-only");
        }
        if (o.Follow != null)
        {
            if (o.Follow.Length == 0) throw new ArgumentException("--follow needs a name");
            if (o.LoginOnly) throw new ArgumentException("--follow needs the world, so not with --login-only");
        }
        if (o.Record != null)
        {
            if (o.Record.Trim().Length == 0) throw new ArgumentException("--record needs a file name");
            if (o.LoginOnly) throw new ArgumentException("--record needs the world, so not with --login-only");
            if (o.RecordFrom is "") throw new ArgumentException("--record-from needs a name");
        }
        else if (recordOption)
            throw new ArgumentException("--record-seconds, --record-from and --overwrite go with --record");
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
