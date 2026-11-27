using NUnit.Framework;

namespace Game.Combat.Tests
{
    [TestFixture]
    public sealed class AttackStanceTests
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

            service.NotifyPlayerAttack(player);
            service.Tick();
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
        public void Enter_AppliesSlowMotion()
        {
            Assert.AreEqual(CombatStance.Attack, service.Stance);
            Assert.AreEqual(0.1f, clock.Scale, 0.0001f);
        }

        [Test]
        public void Countdown_ReportsRemainingTime()
        {
            Run(1.5f, 0.25f);

            Assert.AreEqual(2.5f, service.Snapshot.AttackTimeRemaining, 0.001f);
            Assert.AreEqual(4f, service.Snapshot.AttackDuration, 0.001f);
        }

        [Test]
        public void Expires_AfterFourSeconds_EntersDefense()
        {
            Run(4f, 0.25f);

            Assert.AreEqual(CombatStance.Defense, service.Stance);
            Assert.AreEqual(1f, clock.Scale, 0.0001f);
            Assert.AreEqual(0, service.Snapshot.Rage);
        }

        [Test]
        public void EnemyHitWhileAttacking_DoesNotChangeStance()
        {
            service.NotifyEnemyAttack(new FakeEnemy());
            service.Tick();

            Assert.AreEqual(CombatStance.Attack, service.Stance);
        }
    }
}
