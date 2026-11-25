namespace Game.Combat
{
    public static class PerfectWindowRules
    {
        /// <summary>近战命中剩余时间落在窗口内即开启；-1（没有近战）视为关闭。</summary>
        public static bool IsOpen(float meleeTimeToHit, float windowSeconds)
        {
            return meleeTimeToHit >= 0f && meleeTimeToHit <= windowSeconds;
        }
    }
}
