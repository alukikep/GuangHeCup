using System;

namespace Game.Combat
{
    public enum CombatStance { Neutral, Attack, Defense, Transition }

    public readonly struct StanceSnapshot
    {
        public readonly CombatStance Stance;
        public readonly float AttackTimeRemaining;
        public readonly float AttackDuration;
        public readonly float TransitionTimeRemaining;
        public readonly int Rage;
        public readonly int MaxRage;

        /// <summary>完美格挡窗口宽度（秒）；命中剩余时间用 NearestMeleeTimeToHit() 取。</summary>
        public readonly float PerfectWindow;

        public StanceSnapshot(CombatStance stance, float attackTimeRemaining, float attackDuration,
                              float transitionTimeRemaining, int rage, int maxRage, float perfectWindow)
        {
            Stance = stance;
            AttackTimeRemaining = attackTimeRemaining;
            AttackDuration = attackDuration;
            TransitionTimeRemaining = transitionTimeRemaining;
            Rage = rage;
            MaxRage = maxRage;
            PerfectWindow = perfectWindow;
        }
    }

    public interface IStanceService
    {
        StanceSnapshot Snapshot { get; }
        CombatStance Stance { get; }
        bool IsTransitioning { get; }

        event Action<CombatStance> StanceChanged;
        event Action<int, int> RageChanged;

        /// <summary>玩家攻击“命中”时调用（先后手以命中为判定点）。</summary>
        void NotifyPlayerAttack(IPlayerCombatContext player);

        /// <summary>敌人攻击“命中”玩家时调用。</summary>
        void NotifyEnemyAttack(IEnemyCombatInfo enemy);

        /// <summary>格挡结算完成后调用。</summary>
        void OnBlockSuccess(bool perfect, bool ranged);

        bool IsPerfectWindowOpen();
        float NearestMeleeTimeToHit();

        /// <summary>每帧驱动一次，由 Battle 调用。</summary>
        void Tick();

        /// <summary>转场期间选择武器方向 0..3。</summary>
        bool TryChooseDirection(int direction);
    }
}
