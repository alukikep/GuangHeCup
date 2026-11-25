using NUnit.Framework;

namespace Game.Combat.Tests
{
    [TestFixture]
    public sealed class PerfectWindowTests
    {
        private FakeClock clock;
        private FakeEnemy enemy;
        private FakeRegistry enemies;
        private StanceService service;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock();
            enemy = new FakeEnemy();
            enemies = new FakeRegistry();
            enemies.Add(enemy);
            service = new StanceService(new StanceConfig(), clock, enemies, new FakePlayer());
        }

        private void EnterDefense()
        {
            service.NotifyEnemyAttack(enemy);
            service.Tick();
        }

        [Test]
        public void ClosedInNeutral()
        {
            enemy.MeleeTimeToHit = 0.05f;

            Assert.IsFalse(service.IsPerfectWindowOpen());
        }

        [Test]
        public void OpenInDefense_WhenMeleeWithinWindow()
        {
            EnterDefense();
            enemy.MeleeTimeToHit = 0.05f;   // 窗口 5/60 ≈ 0.0833

            Assert.IsTrue(service.IsPerfectWindowOpen());
        }

        [Test]
        public void ClosedInDefense_WhenMeleeBeyondWindow()
        {
            EnterDefense();
            enemy.MeleeTimeToHit = 0.5f;

            Assert.IsFalse(service.IsPerfectWindowOpen());
        }

        [Test]
        public void Closed_WhenNoMeleeIncoming()
        {
            EnterDefense();
            enemy.MeleeTimeToHit = -1f;

            Assert.IsFalse(service.IsPerfectWindowOpen());
        }

        [Test]
        public void NearestMeleeTimeToHit_PassesThrough()
        {
            enemy.MeleeTimeToHit = 0.2f;

            Assert.AreEqual(0.2f, service.NearestMeleeTimeToHit(), 0.0001f);
        }

        [Test]
        public void DeadEnemy_IsIgnored()
        {
            enemy.MeleeTimeToHit = 0.05f;
            enemies.MarkDead(enemy);

            Assert.AreEqual(-1f, service.NearestMeleeTimeToHit(), 0.0001f);
        }
    }
}
