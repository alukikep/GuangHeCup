using System;

namespace Game.Combat
{
    /// <summary>战斗规则的全部可调参数。纯数据，后续可由 ScriptableObject 承载。</summary>
    [Serializable]
    public sealed class StanceConfig
    {
        public float AttackSeconds = 4f;
        public float TransitionSeconds = 2f;

        public int MaxRage = 100;
        public int RagePerBlock = 30;
        public int RagePerPerfectBlock = 70;

        /// <summary>完美格挡窗口：命中前 5 帧。</summary>
        public float PerfectWindowSeconds = 5f / 60f;

        public float HoldFreeSeconds = 1f;

        /// <summary>长按架盾超时后的怒气消耗加速度（怒气/秒²）。</summary>
        public float HoldDrainAcceleration = 20f;

        public float SlowTimeScale = 0.1f;
        public int DirectionCount = 4;

        public static StanceConfig Default => new StanceConfig();
    }
}
