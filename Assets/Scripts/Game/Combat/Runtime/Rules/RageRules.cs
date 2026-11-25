namespace Game.Combat
{
    public static class RageRules
    {
        /// <summary>
        /// 长按架盾的累计怒气消耗：免费时间之后速率线性增长（速率 = 加速度 × 超时秒数），
        /// 积分后为 0.5 × 加速度 × 超时秒数²。
        /// </summary>
        public static float TotalDrain(float heldSeconds, float freeSeconds, float acceleration)
        {
            float over = heldSeconds - freeSeconds;
            return over <= 0f ? 0f : 0.5f * acceleration * over * over;
        }
    }
}
