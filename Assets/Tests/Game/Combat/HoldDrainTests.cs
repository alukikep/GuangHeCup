using NUnit.Framework;

namespace Game.Combat.Tests
{
    [TestFixture]
    public sealed class HoldDrainTests
    {
        private FakeClock clock;
        private FakePlayer player;
        private StanceService service;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock();
            player = new FakePlayer();
            var enemies = new FakeRegistry();
            enemies.Add(new FakeEnemy());
            service = new StanceService(new StanceConfig(), clock, enemies, player);

            service.NotifyEnemyAttack(new FakeEnemy());
            service.Tick();
            service.OnBlockSuccess(false, false);   // 30 点怒气做本金
        }

        private void Run(float total, float step)
        {
            int steps = (int)System.Math.Round(total / step);
            for (int i = 0; i < steps; i++)
            {
                clock.Advance(step);
                service.Tick();
            }
        }

        [Test]
        public void NoDrainBeforeFreePeriod()
        {
            player.IsGuarding = false;
            Run(1.0f, 0.25f);

            Assert.AreEqual(30, service.Snapshot.Rage);
        }

        [Test]
        public void DrainsAfterFreePeriod()
        {
            player.IsGuarding = true;
            Run(2.0f, 0.25f);   // 超时 1s，加速度 20 → 消耗 0.5*20*1 = 10

            Assert.AreEqual(20, service.Snapshot.Rage);
        }

        [Test]
        public void DrainStopsAtZero()
        {
            player.IsGuarding = true;
            Run(6.0f, 0.25f);

            Assert.AreEqual(0, service.Snapshot.Rage);
        }

        [Test]
        public void ReleasingGuard_ResetsDrainTimeline()
        {
            player.IsGuarding = true;
            Run(2.0f, 0.25f);
            player.IsGuarding = false;
            service.Tick();

            player.IsGuarding = true;
            Run(1.0f, 0.25f);

            Assert.AreEqual(20, service.Snapshot.Rage);
        }
    }
}
