using NUnit.Framework;

namespace Game.Combat.Tests
{
    [TestFixture]
    public sealed class GuardRageTests
    {
        private FakeClock clock;
        private StanceService service;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock();
            service = new StanceService(new StanceConfig(), clock, new FakeRegistry(), new FakePlayer());
            service.NotifyEnemyAttack(new FakeEnemy());
            service.Tick();
        }

        [Test]
        public void Block_AwardsThirtyRage()
        {
            service.OnBlockSuccess(false, false);

            Assert.AreEqual(30, service.Snapshot.Rage);
        }

        [Test]
        public void PerfectBlock_AwardsSeventyRage()
        {
            service.OnBlockSuccess(true, false);

            Assert.AreEqual(70, service.Snapshot.Rage);
        }

        [Test]
        public void PerfectBlock_DoesNotStackNormalReward()
        {
            service.OnBlockSuccess(false, false);
            service.OnBlockSuccess(true, false);

            Assert.AreEqual(100, service.Snapshot.Rage);
        }

        [Test]
        public void RageReachesMax_EntersTransition()
        {
            service.OnBlockSuccess(true, false);
            service.OnBlockSuccess(false, false);

            Assert.AreEqual(CombatStance.Transition, service.Stance);
            Assert.AreEqual(2f, service.Snapshot.TransitionTimeRemaining, 0.001f);
        }

        [Test]
        public void BlockOutsideDefense_IsIgnored()
        {
            var neutral = new StanceService(new StanceConfig(), clock, new FakeRegistry(), new FakePlayer());
            neutral.OnBlockSuccess(true, false);

            Assert.AreEqual(0, neutral.Snapshot.Rage);
            Assert.AreEqual(CombatStance.Neutral, neutral.Stance);
        }

        [Test]
        public void RageChanged_ReportsCurrentAndMax()
        {
            int lastCurrent = -1;
            int lastMax = -1;
            service.RageChanged += (current, max) => { lastCurrent = current; lastMax = max; };

            service.OnBlockSuccess(false, false);

            Assert.AreEqual(30, lastCurrent);
            Assert.AreEqual(100, lastMax);
        }

        [Test]
        public void EnteringDefense_RaisesRageChangedWithZero()
        {
            var fresh = new StanceService(new StanceConfig(), new FakeClock(), new FakeRegistry(), new FakePlayer());
            int lastCurrent = -1;
            fresh.RageChanged += (current, max) => lastCurrent = current;

            fresh.NotifyEnemyAttack(new FakeEnemy());
            fresh.Tick();

            Assert.AreEqual(0, lastCurrent);
        }
    }
}
