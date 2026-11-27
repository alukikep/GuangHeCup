using System;
using Game.Core;
using UnityEngine;

namespace Game.Combat
{
    /// <summary>
    /// 战斗规则状态机：脱战 → 进攻 → 防守 → 转场 → 进攻。
    /// 不引用 UI / MonoBehaviour / Input，时间全部来自 IGameClock.UnscaledDeltaTime。
    /// </summary>
    public sealed class StanceService : IStanceService
    {
        private readonly StanceConfig config;
        private readonly IGameClock clock;
        private readonly IEnemyRegistry enemies;

        private IPlayerCombatContext player;
        private CombatStance stance = CombatStance.Neutral;
        private float rage;
        private float attackRemaining;
        private float transitionRemaining;
        private float guardHeldSeconds;
        private float guardDrainedRage;
        private bool playerHitPending;
        private bool enemyHitPending;
        private float appliedScale = 1f;

        public StanceService(StanceConfig config, IGameClock clock,
                             IEnemyRegistry enemies = null, IPlayerCombatContext player = null)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.enemies = enemies;
            this.player = player;
        }

        public event Action<CombatStance> StanceChanged;
        public event Action<int, int> RageChanged;

        public CombatStance Stance => stance;
        public bool IsTransitioning => stance == CombatStance.Transition;

        public StanceSnapshot Snapshot => new StanceSnapshot(
            stance,
            Mathf.Max(0f, attackRemaining),
            config.AttackSeconds,
            Mathf.Max(0f, transitionRemaining),
            RoundedRage,
            config.MaxRage,
            config.PerfectWindowSeconds);

        public void Tick()
        {
            float dt = clock.UnscaledDeltaTime;
            if (dt < 0f) return;

            // 本帧刚切形态时不推进计时，保证“4 秒”从现在开始算。
            if (ResolveFirstStrike()) return;
            if (CheckEnemyPresence()) return;

            switch (stance)
            {
                case CombatStance.Attack: TickAttack(dt); break;
                case CombatStance.Defense: TickDefense(dt); break;
                case CombatStance.Transition: TickTransition(dt); break;
            }
        }

        public void NotifyPlayerAttack(IPlayerCombatContext playerContext)
        {
            if (playerContext != null) player = playerContext;
            if (stance == CombatStance.Neutral) playerHitPending = true;
        }

        public void NotifyEnemyAttack(IEnemyCombatInfo enemy)
        {
            if (stance == CombatStance.Neutral) enemyHitPending = true;
        }

        public void OnBlockSuccess(bool perfect, bool ranged)
        {
            if (stance != CombatStance.Defense) return;

            rage = Mathf.Min(config.MaxRage,
                rage + (perfect ? config.RagePerPerfectBlock : config.RagePerBlock));
            RaiseRageChanged();

            // 怒气满发生在格挡结算之后，天然满足“完成最后一次格挡再转场”。
            if (rage >= config.MaxRage) EnterTransition();
        }

        public bool IsPerfectWindowOpen()
        {
            return stance == CombatStance.Defense &&
                   PerfectWindowRules.IsOpen(NearestMeleeTimeToHit(), config.PerfectWindowSeconds);
        }

        public float NearestMeleeTimeToHit()
        {
            return enemies != null ? enemies.NearestMeleeTimeToHit() : -1f;
        }

        public bool TryChooseDirection(int direction)
        {
            if (stance != CombatStance.Transition) return false;
            if (direction < 0 || direction >= config.DirectionCount) return false;

            if (player != null) player.ExecuteDirectionalSkill(direction);
            EnterAttack();
            return true;
        }

        private bool ResolveFirstStrike()
        {
            bool playerHit = playerHitPending;
            bool enemyHit = enemyHitPending;
            playerHitPending = false;
            enemyHitPending = false;

            if (stance != CombatStance.Neutral || (!playerHit && !enemyHit)) return false;

            if (FirstStrikeRules.Resolve(playerHit, enemyHit) == CombatStance.Attack) EnterAttack();
            else EnterDefense();
            return true;
        }

        private bool CheckEnemyPresence()
        {
            if (enemies == null || stance == CombatStance.Neutral) return false;

            // 空注册表表示“还不知道有没有敌人”，不能据此判定脱战。
            var all = enemies.All;
            if (all == null || all.Count == 0) return false;
            if (enemies.AnyLiving) return false;

            EnterNeutral();
            return true;
        }

        private void TickAttack(float dt)
        {
            attackRemaining -= dt;
            if (attackRemaining > 0f) return;
            EnterDefense();
        }

        private void TickTransition(float dt)
        {
            transitionRemaining -= dt;
            if (transitionRemaining > 0f) return;
            EnterAttack();
        }

        private void TickDefense(float dt)
        {
            if (player == null || !player.IsGuarding)
            {
                guardHeldSeconds = 0f;
                guardDrainedRage = 0f;
                return;
            }

            guardHeldSeconds += dt;
            float total = RageRules.TotalDrain(guardHeldSeconds, config.HoldFreeSeconds,
                                               config.HoldDrainAcceleration);
            float delta = total - guardDrainedRage;
            if (delta <= 0f) return;

            guardDrainedRage = total;
            if (rage <= 0f) return;

            rage = Mathf.Max(0f, rage - delta);
            RaiseRageChanged();
        }

        private void EnterAttack()
        {
            stance = CombatStance.Attack;
            attackRemaining = config.AttackSeconds;
            transitionRemaining = 0f;
            guardHeldSeconds = 0f;
            guardDrainedRage = 0f;
            ApplySlowMotion(true);
            StanceChanged?.Invoke(stance);
        }

        private void EnterDefense()
        {
            stance = CombatStance.Defense;
            attackRemaining = 0f;
            transitionRemaining = 0f;
            rage = 0f;
            guardHeldSeconds = 0f;
            guardDrainedRage = 0f;
            ApplySlowMotion(false);
            StanceChanged?.Invoke(stance);
            RaiseRageChanged();
        }

        private void EnterTransition()
        {
            stance = CombatStance.Transition;
            transitionRemaining = config.TransitionSeconds;
            attackRemaining = 0f;
            guardHeldSeconds = 0f;
            guardDrainedRage = 0f;
            ApplySlowMotion(true);
            StanceChanged?.Invoke(stance);
        }

        private void EnterNeutral()
        {
            stance = CombatStance.Neutral;
            attackRemaining = 0f;
            transitionRemaining = 0f;
            rage = 0f;
            guardHeldSeconds = 0f;
            guardDrainedRage = 0f;
            playerHitPending = false;
            enemyHitPending = false;
            ApplySlowMotion(false);
            StanceChanged?.Invoke(stance);
            RaiseRageChanged();
        }

        private void ApplySlowMotion(bool slow)
        {
            float target = slow ? config.SlowTimeScale : 1f;
            if (Mathf.Approximately(target, appliedScale)) return;

            appliedScale = target;
            if (Mathf.Approximately(target, 1f)) clock.ResetScale();
            else clock.SetScale(target);
        }

        private int RoundedRage => Mathf.Clamp(Mathf.FloorToInt(rage + 0.0001f), 0, config.MaxRage);

        private void RaiseRageChanged() => RageChanged?.Invoke(RoundedRage, config.MaxRage);
    }
}
