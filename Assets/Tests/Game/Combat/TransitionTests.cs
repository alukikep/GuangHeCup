using NUnit.Framework;

namespace Game.Combat.Tests
{
    [TestFixture]
    public sealed class TransitionTests
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
            service.OnBlockSuccess(true, false);   // 70
            service.OnBlockSuccess(false, false);  // +30 → 100
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
        public void EnteringTransition_KeepsSlowMotion()
        {
            Assert.AreEqual(CombatStance.Transition, service.Stance);
            Assert.IsTrue(service.IsTransitioning);
            Assert.AreEqual(0.1f, clock.Scale, 0.0001f);
        }

        [Test]
        public void Countdown_ReportsRemainingTime()
        {
            Run(0.5f, 0.25f);

            Assert.AreEqual(1.5f, service.Snapshot.TransitionTimeRemaining, 0.001f);
        }

        [Test]
        public void ChooseDirection_ExecutesSkill_AndEntersAttack()
        {
            bool chosen = service.TryChooseDirection(2);

            Assert.IsTrue(chosen);
            Assert.AreEqual(2, player.LastDirection);
            Assert.AreEqual(1, player.DirectionCalls);
            Assert.AreEqual(CombatStance.Attack, service.Stance);
        }

        [Test]
        public void Timeout_EntersAttackWithoutSkill()
        {
            Run(2f, 0.25f);

            Assert.AreEqual(CombatStance.Attack, service.Stance);
            Assert.AreEqual(0, player.DirectionCalls);
        }

        [Test]
        public void InvalidDirection_IsRejected()
        {
            bool chosen = service.TryChooseDirection(9);

            Assert.IsFalse(chosen);
            Assert.AreEqual(CombatStance.Transition, service.Stance);
            Assert.AreEqual(0, player.DirectionCalls);
        }

        [Test]
        public void ChooseOutsideTransition_IsRejected()
        {
            var neutral = new StanceService(new StanceConfig(), new FakeClock(), new FakeRegistry(), player);

            Assert.IsFalse(neutral.TryChooseDirection(0));
        }
    }
}
