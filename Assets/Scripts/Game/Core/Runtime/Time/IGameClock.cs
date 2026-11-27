namespace Game.Core
{
    /// <summary>
    /// 游戏时间入口。慢放只改这里，玩法系统不直接写 Time.timeScale。
    /// 规则层计时一律使用真实时间，避免慢放把判定窗口一并放大。
    /// </summary>
    public interface IGameClock
    {
        /// <summary>上一帧的真实时间（不受慢放影响）。</summary>
        float UnscaledDeltaTime { get; }

        /// <summary>当前时间缩放，1 表示正常。</summary>
        float Scale { get; }

        void SetScale(float scale);

        void ResetScale();
    }
}
