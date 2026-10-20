// The new client's packet code without Godot: framing, checksum, the requests the driver sends and the replies it reads.
// Ported by hand from upstream Offline DAoC 0.37c, source/new-client/godot/game/net/DaocClient.cs (game patch of
// 2026-10-18). Keep the layouts identical to that file; if upstream changes it, port the change here.
//   client -> server: [data length u16 BE][sequence u16][session u16][parameter u16][code u16 (low byte)] data [checksum]
//   server -> client: [data length u16 BE][code u8] data
// Strings are Windows-1252; no encryption (the server has client encryption off unless the client sends a key).
using System;
using System.Collections.Generic;
using System.Text;

namespace DaocServer.HeadlessClient;

public static class Packets
{
    public const int Port = 10300;
    public const byte ClientType = 0x06;                       // Labyrinth of the Minotaur client (every expansion on)
    public static readonly byte[] Version = { 1, 1, 27 };      // 1.127, as game.dll announced it
    public const int ClientHeaderSize = 10;                    // before the data; the checksum (2) follows it
    public const int ServerHeaderSize = 3;

    // Client -> server codes.
    public const byte CryptKeyRequest = 0xF4, LoginRequest = 0xA7, CharacterOverviewRequest = 0xFC,
        CharacterSelectRequest = 0x10, RegionListRequest = 0x9D, WorldInitRequest = 0xD4, PlayerInitRequest = 0xE8,
        GameOpenRequest = 0xBF, PlayerCommand = 0xAF;

    // Server -> client codes.
    public const byte CryptKey = 0x22, LoginGranted = 0x2A, LoginDenied = 0x2C, SessionId = 0x28, Realm = 0xFE,
        CharacterOverview = 0xFC, PositionAndObjectId = 0x20, Message = 0xAF;

    public static readonly Encoding Text1252;

    static Packets()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Text1252 = Encoding.GetEncoding(1252);
    }

    // PacketProcessor.CalculateChecksum
    public static ushort Checksum(byte[] packet, int offset, int size)
    {
        byte val1 = 0x7E, val2 = 0x7E;
        for (int i = offset; i < offset + size; i++)
        {
            val1 += packet[i];
            val2 += val1;
        }
        return (ushort)(val2 - ((val1 + val2) << 8));
    }

    // One framed client packet, ready to write to the socket.
    public static byte[] Frame(byte code, ushort sequence, int sessionId, byte[] body)
    {
        var buf = new byte[ClientHeaderSize + body.Length + 2];
        buf[0] = (byte)(body.Length >> 8);
        buf[1] = (byte)body.Length;
        buf[2] = (byte)(sequence >> 8);
        buf[3] = (byte)sequence;
        buf[4] = (byte)(sessionId >> 8);
        buf[5] = (byte)sessionId;
        buf[6] = 0;                                            // parameter
        buf[7] = 0;
        buf[8] = 0;                                            // code (the server reads the low byte)
        buf[9] = code;
        Buffer.BlockCopy(body, 0, buf, ClientHeaderSize, body.Length);
        ushort sum = Checksum(buf, 0, buf.Length - 2);
        buf[^2] = (byte)(sum >> 8);
        buf[^1] = (byte)sum;
        return buf;
    }

    // ---------------------------------------------------------------- request bodies

    // CryptKeyRequestHandler: client type, version (1, 1, 27), revision letter, build (u16).
    public static byte[] Handshake()
    {
        var w = new Writer();
        w.Byte(ClientType);
        w.Bytes(Version);
        w.Byte((byte)'a');
        w.Short(0);
        return w.ToArray();
    }

    // LoginRequestHandler (1.126+): username and password, each a little-endian u32 length and its bytes.
    public static byte[] Login(string account, string password)
    {
        var w = new Writer();
        w.IntPascal(account);
        w.IntPascal(password);
        return w.ToArray();
    }

    // CharacterOverviewRequestHandler (1.124+): 0 asks which realm the account may use, 1-3 that realm's list.
    public static byte[] RequestCharacters(int realm) => new[] { (byte)realm };

    // The cross-realm character index the three enter-world requests carry (realm 2's first slot is 10).
    public static byte CharacterIndex(int realm, int slot) => (byte)(slot + (realm - 1) * 10);

    // CharacterSelectRequest: the index and the name in 28 bytes.
    public static byte[] CharacterSelect(int realm, int slot, string name)
    {
        var w = new Writer();
        w.Byte(CharacterIndex(realm, slot));
        w.Fixed(name, 28);
        return w.ToArray();
    }

    // RegionListRequest and WorldInitRequest: the index alone.
    public static byte[] IndexOnly(int realm, int slot) => new[] { CharacterIndex(realm, slot) };

    // GameOpenRequest: flag 0 = no UDP (the server sends everything over TCP).
    public static byte[] GameOpen() => new byte[] { 0 };

    // PlayerCommandHandler (1.127): one byte, then the text - "&command args" for a slash command, "&say text" for chat.
    public static byte[] Command(string text)
    {
        var w = new Writer();
        w.Byte(0);
        w.CString(text);
        return w.ToArray();
    }

    // What a typed chat line becomes, as the new client sends it: "/cmd x" -> "&cmd x", anything else is /say.
    public static string TypedLine(string text) => text.StartsWith('/') ? "&" + text[1..] : "&say " + text;

    // ---------------------------------------------------------------- replies

    public sealed record Character(int Realm, int Slot, int Level, string Name, string ClassName, string RaceName,
        string Location, int ClassId);

    public sealed record Position(float X, float Y, float Z, int ObjectId, int Region);

    // LoginGranted (PacketLib1127): account, then server name.
    public static (string Account, string Server) ReadLoginGranted(byte[] data)
    {
        var r = new Reader(data);
        return (r.Pascal(), r.Pascal());
    }

    // SessionID (little-endian).
    public static int ReadSessionId(byte[] data) => new Reader(data).ShortLE();

    // PositionAndObjectID (1124): you, in the world.
    public static Position ReadPosition(byte[] data)
    {
        var r = new Reader(data);
        float x = r.FloatLE(), y = r.FloatLE(), z = r.FloatLE();
        int oid = r.Short();
        r.Short(); r.Short(); r.Short();                       // heading, zone x, zone y
        return new Position(x, y, z, oid, r.Short());
    }

    // Message (1127): chat type, then the text ("@@" chat window / "##" popup prefix removed).
    public static (int Type, string Text) ReadMessage(byte[] data)
    {
        var r = new Reader(data);
        int type = r.Byte();
        string text = r.CString();
        if (text.StartsWith("##") || text.StartsWith("@@")) text = text[2..];
        return (type, text);
    }

    // PacketLib1126.SendCharacterOverview: four u32 flags, then ten slots - 0 for an empty one, else the character.
    // The realm is the one asked for; each character also carries its own.
    public static List<Character> ReadOverview(byte[] data, int askedRealm)
    {
        var list = new List<Character>();
        var r = new Reader(data);
        r.IntLE(); r.IntLE(); r.IntLE(); r.IntLE();
        for (int slot = 0; slot < 10 && r.Left > 0; slot++)
        {
            int level = r.Byte();
            if (level == 0) continue;
            string name = r.IntPascal();
            r.IntLE();                                         // 0x18
            r.Byte();                                          // always 1
            r.Skip(10);                                        // face and customisation bytes, mood
            r.Skip(13);
            string location = r.IntPascal();
            string className = r.IntPascal();
            string raceName = r.IntPascal();
            r.ShortLE();                                       // model
            r.Byte(); r.Byte();                                // region, expansion
            r.Skip(2 * 20);                                    // equipment models (12) and colours (8)
            r.Skip(8);                                         // STR DEX CON QUI INT PIE EMP CHA
            int classId = r.Byte();
            int realm = r.Byte();
            r.Byte();                                          // race and gender
            r.Byte(); r.Byte();                                // weapon slots
            r.Byte(); r.Byte(); r.Byte();                      // SI flag, constitution again, unused
            list.Add(new Character(realm != 0 ? realm : askedRealm, slot, level, name, className, raceName, location, classId));
        }
        return list;
    }

    // ---------------------------------------------------------------- byte helpers

    public sealed class Writer
    {
        readonly List<byte> _b = new();
        public void Byte(byte v) => _b.Add(v);
        public void Bytes(byte[] v) => _b.AddRange(v);
        public void Short(ushort v) { _b.Add((byte)(v >> 8)); _b.Add((byte)v); }
        public void ShortLE(ushort v) { _b.Add((byte)v); _b.Add((byte)(v >> 8)); }
        public void Int(uint v) { _b.Add((byte)(v >> 24)); _b.Add((byte)(v >> 16)); _b.Add((byte)(v >> 8)); _b.Add((byte)v); }
        public void IntLE(uint v) { _b.Add((byte)v); _b.Add((byte)(v >> 8)); _b.Add((byte)(v >> 16)); _b.Add((byte)(v >> 24)); }
        public void IntPascal(string s) { var b = Text1252.GetBytes(s ?? ""); IntLE((uint)b.Length); _b.AddRange(b); }
        public void Pascal(string s) { var b = Text1252.GetBytes(s ?? ""); int n = Math.Min(255, b.Length); Byte((byte)n); _b.AddRange(b[..n]); }
        public void Fixed(string s, int len) { var b = Text1252.GetBytes(s ?? ""); for (int i = 0; i < len; i++) _b.Add(i < b.Length ? b[i] : (byte)0); }
        public void CString(string s) { _b.AddRange(Text1252.GetBytes(s ?? "")); _b.Add(0); }
        public void FloatLE(float v) { var b = BitConverter.GetBytes(v); if (!BitConverter.IsLittleEndian) Array.Reverse(b); _b.AddRange(b); }
        public byte[] ToArray() => _b.ToArray();
    }

    public sealed class Reader
    {
        readonly byte[] _d;
        int _p;
        public Reader(byte[] d) { _d = d; }
        public int Left => _d.Length - _p;
        public byte Byte() => _p < _d.Length ? _d[_p++] : (byte)0;
        public void Skip(int n) => _p = Math.Min(_d.Length, _p + n);
        public int Short() => (Byte() << 8) | Byte();
        public int ShortLE() => Byte() | (Byte() << 8);
        public uint IntLE() => Byte() | ((uint)Byte() << 8) | ((uint)Byte() << 16) | ((uint)Byte() << 24);
        public float FloatLE() => BitConverter.Int32BitsToSingle((int)IntLE());
        public string Pascal() => Str(Byte());
        // The server counts a closing NUL in the length (PacketOut.WritePascalStringIntLE); drop it.
    public string IntPascal() => Str((int)Math.Min(IntLE(), int.MaxValue)).TrimEnd('\0');
        public string CString()
        {
            int s = _p;
            while (_p < _d.Length && _d[_p] != 0) _p++;
            var t = Text1252.GetString(_d, s, _p - s);
            if (_p < _d.Length) _p++;
            return t;
        }
        string Str(int n) { n = Math.Max(0, Math.Min(n, Left)); var s = Text1252.GetString(_d, _p, n); _p += n; return s; }
    }
}
