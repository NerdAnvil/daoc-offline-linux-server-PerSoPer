using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using DOL.Database;
using DOL.GS;
using NUnit.Framework;

namespace DOL.UnitTests
{
    // An effect that is ended before its queued start runs must not be added
    // to EffectService. Once there it can never end again and ticks forever.
    [TestFixture, NonParallelizable]
    public class UT_EffectEndedBeforeStart
    {
        private static readonly IObjectDatabase Empty = DispatchProxy.Create<IObjectDatabase, UT_UnobservedConcentration.EmptyReads>();
        private GameServer _previous;

        private sealed class Server : GameServer
        {
            protected override IObjectDatabase DataBaseImpl => Empty;
        }

        [SetUp]
        public void SetUp()
        {
            _previous = GameServer.Instance;
            GameServer.LoadTestDouble((Server) RuntimeHelpers.GetUninitializedObject(typeof(Server)));
        }

        [TearDown]
        public void TearDown()
        {
            GameServer.LoadTestDouble(_previous);
        }

        private sealed class Npc : GameNPC
        {
            public override bool IsAlive => true;
        }

        private static Npc NewNpc()
        {
            Npc npc = (Npc) RuntimeHelpers.GetUninitializedObject(typeof(Npc));
            typeof(GameNPC).GetField("m_brains", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(npc, new ArrayList());
            npc.effectListComponent = EffectListComponent.Create(npc);
            return npc;
        }

        [Test]
        public void EffectEndedBeforeItsStartIsProcessedIsNotAddedToTheEffectService()
        {
            Npc npc = NewNpc();
            ECSGameAbilityEffect effect = new(new ECSGameEffectInitParams(npc, 0, 1));

            try
            {
                Assert.That(effect.Start(), Is.True);
                Assert.That(effect.End(), Is.True, "Ending before the queued start runs must be allowed");
                npc.effectListComponent.BeginTick();

                Assert.That(effect.IsEnded, Is.True);
                Assert.That(effect.ServiceObjectId.IsSet || effect.ServiceObjectId.IsPendingAddition, Is.False,
                    "An ended effect was put back in the effect service");
            }
            finally
            {
                ServiceObjectStore.Remove<ECSGameEffect>(effect);
                ServiceObjectStore.Remove(npc.effectListComponent);
            }
        }

        [Test]
        public void EffectThatStartsNormallyIsStillAddedToTheEffectService()
        {
            Npc npc = NewNpc();
            ECSGameAbilityEffect effect = new(new ECSGameEffectInitParams(npc, 0, 1));

            try
            {
                Assert.That(effect.Start(), Is.True);
                npc.effectListComponent.BeginTick();

                Assert.That(effect.IsActive, Is.True);
                Assert.That(effect.ServiceObjectId.IsSet || effect.ServiceObjectId.IsPendingAddition, Is.True);
            }
            finally
            {
                ServiceObjectStore.Remove<ECSGameEffect>(effect);
                ServiceObjectStore.Remove(npc.effectListComponent);
            }
        }
    }
}
