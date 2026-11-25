using NUnit.Framework;

namespace Game.Combat.Tests
{
    [TestFixture]
    public sealed class NeutralTests
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

        [Test]
        public void StaysDefense_WhileEnemyAlive()
        {
            service.NotifyEnemyAttack(enemy);
            service.Tick();
            service.Tick();

            Assert.AreEqual(CombatStance.Defense, service.Stance);
        }

        [Test]
        public void AllEnemiesDead_ReturnsToNeutral_AndResetsTime()
        {
            service.NotifyEnemyAttack(enemy);
            service.Tick();

            enemies.MarkDead(enemy);
            service.Tick();

            Assert.AreEqual(CombatStance.Neutral, service.Stance);
            Assert.AreEqual(1f, clock.Scale, 0.0001f);
            Assert.AreEqual(0, service.Snapshot.Rage);
        }

        [Test]
        public void GuardAgainstNullRegistry_DoesNotThrow()
        {
            var lonely = new StanceService(new StanceConfig(), clock, null, new FakePlayer());

            Assert.AreEqual(-1f, lonely.NearestMeleeTimeToHit(), 0.0001f);
            Assert.DoesNotThrow(() => lonely.Tick());
        }
    }
}
