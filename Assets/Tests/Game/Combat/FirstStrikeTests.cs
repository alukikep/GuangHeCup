using NUnit.Framework;

namespace Game.Combat.Tests
{
    [TestFixture]
    public sealed class FirstStrikeTests
    {
        private FakeClock clock;
        private FakePlayer player;
        private FakeRegistry enemies;
        private StanceService service;

        [SetUp]
        public void SetUp()
        {
            clock = new FakeClock();
            player = new FakePlayer();
            enemies = new FakeRegistry();
            enemies.Add(new FakeEnemy());
            service = new StanceService(new StanceConfig(), clock, enemies, player);
        }

        [Test]
        public void StartsNeutral()
        {
            Assert.AreEqual(CombatStance.Neutral, service.Stance);
        }

        [Test]
        public void PlayerHitFirst_EntersAttack()
        {
            service.NotifyPlayerAttack(player);
            service.Tick();

            Assert.AreEqual(CombatStance.Attack, service.Stance);
        }

        [Test]
        public void EnemyHitFirst_EntersDefenseWithEmptyRage()
        {
            service.NotifyEnemyAttack(new FakeEnemy());
            service.Tick();

            Assert.AreEqual(CombatStance.Defense, service.Stance);
            Assert.AreEqual(0, service.Snapshot.Rage);
        }

        [Test]
        public void BothHitOnSameTick_PlayerWins()
        {
            service.NotifyPlayerAttack(player);
            service.NotifyEnemyAttack(new FakeEnemy());
            service.Tick();

            Assert.AreEqual(CombatStance.Attack, service.Stance);
        }

        [Test]
        public void NoHit_StaysNeutral()
        {
            service.Tick();

            Assert.AreEqual(CombatStance.Neutral, service.Stance);
        }
    }
}
