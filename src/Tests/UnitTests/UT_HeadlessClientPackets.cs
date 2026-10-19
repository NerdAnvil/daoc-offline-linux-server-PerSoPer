using System;
using System.Linq;
using DaocServer.HeadlessClient;
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
        public void ReadsOneCharacterFromAnOverview()
        {
            var w = new Packets.Writer();
            for (int i = 0; i < 4; i++) w.IntLE(0);
            w.Byte(0);                                                                  // slot 0 empty
            w.Byte(12);                                                                 // slot 1: level 12
            w.IntPascal("Aldric");
            w.IntLE(0x18); w.Byte(1);
            w.Bytes(new byte[10 + 13]);
            w.IntPascal("Camelot Hills");
            w.IntPascal("Cleric");
            w.IntPascal("Briton");
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
