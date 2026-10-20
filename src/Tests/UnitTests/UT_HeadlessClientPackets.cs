using System;
using System.Linq;
using DaocServer.HeadlessClient;
using DOL.GS;
using DOL.GS.PacketHandler;
using NUnit.Framework;

namespace DOL.Tests.Unit
{
    // The headless test client must speak exactly what the server reads: its framed packets go through the server's
    // own checksum and GSPacketIn, and server-built packets go through the client's readers.
    [TestFixture]
    public class UT_HeadlessClientPackets
    {
        private static GSPacketIn ServerReads(byte[] framed)
        {
            var packet = new GSPacketIn();
            packet.Load(framed, 0, framed.Length);
            return packet;
        }

        private static byte[] ServerSends(byte code, Action<GSTCPPacketOut> write)
        {
            var pak = new GSTCPPacketOut().Init(code);
            write(pak);
            pak.WritePacketLength();
            byte[] all = pak.ToArray();
            Assert.That((all[0] << 8) | all[1], Is.EqualTo(all.Length - Packets.ServerHeaderSize), "length header");
            Assert.That(all[2], Is.EqualTo(code));
            return all[Packets.ServerHeaderSize..];
        }

        [Test]
        public void ChecksumMatchesTheServers()
        {
            var random = new Random(1127);
            for (int size = 0; size < 300; size += 7)
            {
                var data = new byte[size + 4];
                random.NextBytes(data);
                Assert.That(Packets.Checksum(data, 2, size), Is.EqualTo(PacketProcessor.CalculateChecksum(data, 2, size)));
            }
        }

        [Test]
        public void FramedPacketPassesTheServersHeaderAndChecksumCheck()
        {
            byte[] framed = Packets.Frame(Packets.PlayerCommand, 513, 42, Packets.Command("&say hi"));
            int length = (framed[0] << 8) + framed[1] + GSPacketIn.HDR_SIZE;           // as GameClient reads it
            Assert.That(length, Is.EqualTo(framed.Length));
            int sent = (framed[^2] << 8) | framed[^1];
            Assert.That(sent, Is.EqualTo(PacketProcessor.CalculateChecksum(framed, 0, length - 2)));

            GSPacketIn packet = ServerReads(framed);
            Assert.That(packet.Code, Is.EqualTo(Packets.PlayerCommand));
            Assert.That(packet.Sequence, Is.EqualTo(513));
            Assert.That(packet.SessionID, Is.EqualTo(42));
        }

        [Test]
        public void LoginCarriesAccountAndPasswordAsTheServerReadsThem()
        {
            GSPacketIn packet = ServerReads(Packets.Frame(Packets.LoginRequest, 0, 0, Packets.Login("tester", "pässword")));
            Assert.That(packet.ReadIntPascalStringLowEndian(), Is.EqualTo("tester"));
            Assert.That(packet.ReadIntPascalStringLowEndian(), Is.EqualTo("pässword"));
        }

        [Test]
        public void TypedLinesBecomeCommandsTheServerReads()
        {
            Assert.That(Packets.TypedLine("/persoper"), Is.EqualTo("&persoper"));
            Assert.That(Packets.TypedLine("hello there"), Is.EqualTo("&say hello there"));

            GSPacketIn packet = ServerReads(Packets.Frame(Packets.PlayerCommand, 0, 7, Packets.Command(Packets.TypedLine("/bots list"))));
            packet.Skip(1);                                                             // as PlayerCommandHandler (1.127)
            Assert.That(packet.ReadString(255), Is.EqualTo("&bots list"));
        }

        [Test]
        public void CharacterSelectUsesTheCrossRealmIndexAndName()
        {
            Assert.That(Packets.CharacterIndex(1, 3), Is.EqualTo(3));
            Assert.That(Packets.CharacterIndex(2, 0), Is.EqualTo(10));
            Assert.That(Packets.CharacterIndex(3, 9), Is.EqualTo(29));

            GSPacketIn packet = ServerReads(Packets.Frame(Packets.CharacterSelectRequest, 0, 0, Packets.CharacterSelect(2, 4, "Brynja")));
            Assert.That(packet.ReadByte(), Is.EqualTo(14));
            Assert.That(packet.ReadString(28), Is.EqualTo("Brynja"));
        }

        [Test]
        public void ReadsLoginGrantedAndMessagesBuiltByTheServer()
        {
            byte[] granted = ServerSends(Packets.LoginGranted, p =>
            {
                p.WritePascalString("tester");
                p.WritePascalString("OfflineDAoC");
                p.WriteByte(0x05);
            });
            Assert.That(Packets.ReadLoginGranted(granted), Is.EqualTo(("tester", "OfflineDAoC")));

            byte[] message = ServerSends(Packets.Message, p =>                      // as PacketLib1127.SendRawMessage
            {
                p.WriteByte(0x07);
                p.WriteNonNullTerminatedString("@@");
                p.WriteString("Persoper says, \"Hello!\"");
            });
            Assert.That(Packets.ReadMessage(message), Is.EqualTo((7, "Persoper says, \"Hello!\"")));
        }

        [Test]
        public void ReadsNpcCreateBuiltAsTheServerBuildsIt()
        {
            byte[] npc = ServerSends(Packets.NpcCreate, p =>                         // as PacketLib1124.SendNPCCreate
            {
                p.WriteShort(1234);                                                 // object id
                p.WriteShort(0); p.WriteShort(2048);                                // speed, heading
                p.WriteShort(3200);                                                 // z
                p.WriteInt(531000); p.WriteInt(479500);                             // x, y
                p.WriteShort(0); p.WriteShort(32); p.WriteByte(50);                 // speed z, model, size
                p.WriteByte(50 | 0x80);                                             // level, statue bit
                p.WriteByte(1 << 6); p.WriteByte(0x20); p.WriteByte(0); p.WriteByte(0); p.WriteShort(0);
                p.WritePascalString("Persoper");
                p.WritePascalString("Companions");
                p.WriteByte(0);
            });
            Assert.That(Packets.ReadNpcCreate(npc),
                Is.EqualTo(new Packets.Living(1234, false, "Persoper", "Companions", 50, 531000, 479500, 3200)));
        }

        [Test]
        public void ReadsPlayerCreateBuiltAsTheServerBuildsIt()
        {
            byte[] player = ServerSends(Packets.PlayerCreate, p =>                  // as PacketLib1124.SendPlayerCreate
            {
                p.WriteFloatLowEndian(531010.5f); p.WriteFloatLowEndian(479490f); p.WriteFloatLowEndian(3201f);
                p.WriteShort(7); p.WriteShort(88); p.WriteShort(1024); p.WriteShort(32);   // session, oid, heading, model
                p.WriteByte(12); p.WriteByte(1 << 2);                               // level, flags
                for (int i = 0; i < 7 + 3; i++) p.WriteByte(0);                     // face, unknown
                p.WritePascalString("Aldric");
                p.WritePascalString("");
                p.WritePascalString("Smith");
                p.WritePascalString("");
                p.WritePascalString("");
            });
            var l = Packets.ReadPlayerCreate(player);
            Assert.That(l, Is.EqualTo(new Packets.Living(88, true, "Aldric", "", 12, 531010.5f, 479490f, 3201f)));
            Assert.That(l.DistanceTo(new Packets.Position(531010.5f, 479494f, 3204f, 1, 1)), Is.EqualTo(5).Within(0.001));
        }

        [Test]
        public void ReadsTheObjectIdOfObjectsLeavingView()
        {
            byte[] delete = ServerSends(Packets.ObjectDelete, p => { p.WriteShort(1234); p.WriteShort(1); });  // PacketLib168
            Assert.That(Packets.ReadRemovedObjectId(delete), Is.EqualTo(1234));
        }

        [Test]
        public void ReadsOneCharacterFromAnOverview()
        {
            var w = new Packets.Writer();
            for (int i = 0; i < 4; i++) w.IntLE(0);
            w.Byte(0);                                                                  // slot 0 empty
            w.Byte(12);                                                                 // slot 1: level 12
            w.IntPascal("Aldric\0");                                                    // the server counts and sends a closing NUL
            w.IntLE(0x18); w.Byte(1);
            w.Bytes(new byte[10 + 13]);
            w.IntPascal("Camelot Hills\0");
            w.IntPascal("Cleric\0");
            w.IntPascal("Briton\0");
            w.ShortLE(0); w.Byte(1); w.Byte(2);
            w.Bytes(new byte[2 * 20 + 8]);
            w.Byte(6); w.Byte(1); w.Byte(0);                                            // class, realm, race/gender
            w.Bytes(new byte[2 + 3]);
            for (int slot = 2; slot < 10; slot++) w.Byte(0);

            var list = Packets.ReadOverview(w.ToArray(), 1);
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list.Single(), Is.EqualTo(new Packets.Character(1, 1, 12, "Aldric", "Cleric", "Briton", "Camelot Hills", 6)));
        }

        [Test]
        public void CreateRequestReadsAsTheServerReadsIt()
        {
            byte[] stats = Packets.CreationStats(1);
            GSPacketIn packet = ServerReads(Packets.Frame(Packets.CharacterCreateRequest, 0, 0,
                Packets.CreateCharacter(2, 3, "Testa", 22, 6, false, 137, stats)));

            // CharacterCreateRequestHandler.CreationCharacterData, 1.126+ layout
            Assert.That(packet.Code, Is.EqualTo(0xFF));
            Assert.That(packet.ReadByte(), Is.EqualTo(13), "cross-realm slot");
            Assert.That(packet.ReadIntPascalStringLowEndian(), Is.EqualTo("Testa"));
            packet.Skip(4);
            Assert.That(packet.ReadByte(), Is.EqualTo(1), "custom mode");
            packet.Skip(6 + 3 + 1 + 9);
            Assert.That(packet.ReadByte(), Is.EqualTo(1), "operation: create");
            packet.Skip(1 + 2 + 15);
            Assert.That(packet.ReadByte(), Is.EqualTo(22), "class");
            Assert.That(packet.ReadByte(), Is.EqualTo(2), "realm");
            Assert.That(packet.ReadByte() & 0x1F, Is.EqualTo(6), "race");
            Assert.That(packet.ReadShortLowEndian(), Is.EqualTo(137), "model");
            for (int i = 0; i < 8; i++) Assert.That(packet.ReadByte(), Is.EqualTo(stats[i]));
            packet.Skip(43);
            Assert.That(packet.ReadByte(), Is.EqualTo(stats[2]), "constitution again");

            byte[] female = Packets.CreateCharacter(1, 0, "Testa", 2, 1, true, 35, stats);
            Assert.That(female[1 + 4 + 5 + 4 + 20 + 4 + 15 + 2] >> 7, Is.EqualTo(1), "gender bit");
        }

        [Test]
        public void DeleteRequestCarriesOperationThreeAndTheSlot()
        {
            GSPacketIn packet = ServerReads(Packets.Frame(Packets.CharacterCreateRequest, 0, 0, Packets.DeleteCharacter(3, 2)));
            Assert.That(packet.ReadByte(), Is.EqualTo(22), "cross-realm slot");
            Assert.That(packet.ReadIntPascalStringLowEndian(), Is.Empty);
            packet.Skip(4 + 1 + 6 + 3 + 1 + 9);
            Assert.That(packet.ReadByte(), Is.EqualTo(3), "operation: delete");
            Assert.That(packet.DataSize, Is.EqualTo(Packets.CreateCharacter(1, 0, "", 1, 1, false, 0, new byte[8]).Length),
                "same size as a create");
        }

        [Test]
        public void CreationStatsSpendExactlyTheServersPointBudgetForEveryRace()
        {
            string[] order = { "STR", "DEX", "CON", "QUI", "INT", "PIE", "EMP", "CHR" };
            foreach (var race in Packets.Races)
            {
                Assert.That(race.Name, Is.EqualTo(((eRace)race.Id).ToString()), "race id");
                byte[] s = Packets.CreationStats(race.Id);
                var stats = order.Select((name, i) => (Enum.Parse<eStat>(name), (int)s[i])).ToDictionary(x => x.Item1, x => x.Item2);
                bool valid = CharacterStatValidator.Validate(stats, GlobalConstants.STARTING_STATS_DICT[(eRace)race.Id],
                    eStat.STR, eStat.DEX, eStat.CON, 1, out int points);
                Assert.That(valid, Is.True, $"{race.Name}: {points} points");
            }
        }

        [Test]
        public void ClassNamesMatchTheServersClassIds()
        {
            foreach (var (name, id) in Packets.ClassIds)
                Assert.That(id, Is.EqualTo((int)Enum.Parse<eCharacterClass>(name)), name);
            foreach (var realm in GlobalConstants.STARTING_CLASSES_DICT.Values)
                foreach (var cls in realm)
                    Assert.That(Packets.ClassIds.ContainsValue((int)cls), Is.True, $"starting class {cls} missing");
        }

        [Test]
        public void NamesFollowTheServersRule()
        {
            Assert.That(Packets.IsValidName("Testa"), Is.True);
            Assert.That(Packets.IsValidName("Te"), Is.False);
            Assert.That(Packets.IsValidName("testa"), Is.False);
            Assert.That(Packets.IsValidName("Test_a"), Is.False);
            Assert.That(Packets.IsValidName("Tëst"), Is.False);
            Assert.That(Packets.IsValidName(new string('a', 23).Insert(0, "A")), Is.False, "24 letters");
        }

        [Test]
        public void CreateAndDeleteOptions()
        {
            Options o = Options.Parse(new[] { "--account=a", "--create=Testa", "--realm=mid", "--race=troll", "--class=Warrior",
                "--delete=testa" }, null);
            Assert.That((o.Create, o.Delete, o.Realm, o.RaceId, o.ClassId, o.Female), Is.EqualTo(("Testa", "testa", 2, 6, 22, false)));
            Assert.That(Options.Parse(new[] { "--account=a", "--create=Testa", "--realm=1", "--race=Norse", "--class=2", "--female" },
                null).RaceId, Is.EqualTo(5));
            Assert.That(Options.Parse(new[] { "--account=a", "--delete=Testa" }, null).Delete, Is.EqualTo("Testa"));

            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "--account=a", "--create=Testa", "--realm=1", "--race=Briton" }, null));
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "--account=a", "--create=te", "--realm=1", "--race=Briton", "--class=2" }, null));
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "--account=a", "--create=Testa", "--realm=4", "--race=Briton", "--class=2" }, null));
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "--account=a", "--create=Testa", "--realm=1", "--race=Ogre", "--class=2" }, null));
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "--account=a", "--create=Testa", "--realm=1", "--race=Briton", "--class=Jester" }, null));
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "--account=a", "--create=Testa", "--realm=1", "--race=Briton", "--class=2", "--delete=Other" }, null));
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "--account=a", "--race=Briton" }, null));
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "--account=a", "--delete=" }, null));
        }

        [Test]
        public void OptionsNeedAnAccountAndTakeThePasswordFromTheEnvironment()
        {
            Assert.Throws<ArgumentException>(() => Options.Parse(new string[0], null));
            Assert.Throws<ArgumentException>(() => Options.Parse(new[] { "--account=a", "--listen=x" }, null));
            Options o = Options.Parse(new[] { "--account=a", "--send=/persoper", "--send=hi", "--login-only" }, "secret");
            Assert.That(o.Password, Is.EqualTo("secret"));
            Assert.That(o.Send, Is.EqualTo(new[] { "/persoper", "hi" }));
            Assert.That(o.LoginOnly, Is.True);
            Assert.That(o.Server, Is.EqualTo("127.0.0.1"));
        }
    }
}
