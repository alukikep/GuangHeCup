using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Combat.Tests
{
    internal sealed class FakeClock : IGameClock
    {
        public float UnscaledDeltaTime { get; private set; }
        public float Scale { get; private set; } = 1f;

        public void SetScale(float scale) => Scale = scale;
        public void ResetScale() => Scale = 1f;
        public void Advance(float deltaTime) => UnscaledDeltaTime = deltaTime;
    }

    internal sealed class FakeHealth : IHealth
    {
        public int Current { get; private set; } = 100;
        public int Max => 100;
        public bool IsAlive => Current > 0;

        public event Action<int, int> Changed;
        public event Action Died;

        public void Heal(int amount)
        {
            Current = Mathf.Min(Max, Current + amount);
            Changed?.Invoke(Current, Max);
        }

        public void Kill()
        {
            Current = 0;
            Changed?.Invoke(Current, Max);
            Died?.Invoke();
        }
    }

    internal sealed class FakePlayer : IPlayerCombatContext
    {
        private readonly FakeHealth health = new FakeHealth();

        public Transform Transform => null;
        public IHealth Health => health;
        public bool IsDead { get; set; }
        public bool IsGuarding { get; set; }
        public bool IsAttacking { get; set; }
        public WeaponType CurrentWeapon { get; set; } = WeaponType.Katana;
        public int LastDirection { get; private set; } = -1;
        public int DirectionCalls { get; private set; }

        public bool TryBlock(in BlockContext context) => true;

        public void ExecuteDirectionalSkill(int direction)
        {
            LastDirection = direction;
            DirectionCalls++;
        }
    }

    internal sealed class FakeEnemy : IEnemyCombatInfo
    {
        public Transform Transform => null;
        public bool IsDead { get; set; }
        public IHealth Health { get; } = new FakeHealth();
        public EnemyState State { get; set; } = EnemyState.Patrol;
        public float DistanceToPlayer { get; set; }
        public float MeleeTimeToHit { get; set; } = -1f;
        public bool IsCharging { get; set; }

        public void ApplyBlockRecoil(float direction) { }
    }

    internal sealed class FakeRegistry : IEnemyRegistry
    {
        private readonly List<IEnemyCombatInfo> all = new List<IEnemyCombatInfo>();

        public IReadOnlyList<IEnemyCombatInfo> All => all;
        public event Action<IEnemyCombatInfo> Registered;
        public event Action<IEnemyCombatInfo> Died;

        public void Add(IEnemyCombatInfo enemy)
        {
            all.Add(enemy);
            Registered?.Invoke(enemy);
        }

        public void MarkDead(IEnemyCombatInfo enemy)
        {
            if (enemy is FakeEnemy fake) fake.IsDead = true;
            Died?.Invoke(enemy);
        }

        public IEnemyCombatInfo NearestTo(Vector2 point, float maxDistance) => null;

        public float NearestMeleeTimeToHit()
        {
            float best = -1f;
            foreach (var enemy in all)
            {
                if (enemy.IsDead || enemy.MeleeTimeToHit < 0f) continue;
                if (best < 0f || enemy.MeleeTimeToHit < best) best = enemy.MeleeTimeToHit;
            }
            return best;
        }

        public bool AnyLiving
        {
            get
            {
                foreach (var enemy in all)
                {
                    if (!enemy.IsDead) return true;
                }
                return false;
            }
        }

        public void Refresh() { }
    }
}
