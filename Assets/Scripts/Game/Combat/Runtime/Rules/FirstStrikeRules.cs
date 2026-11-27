namespace Game.Combat
{
    /// <summary>脱战进入战斗时，用“谁先命中”决定初始形态；同帧双方命中判玩家先手。</summary>
    public static class FirstStrikeRules
    {
        public static CombatStance Resolve(bool playerHit, bool enemyHit)
        {
            if (playerHit) return CombatStance.Attack;
            if (enemyHit) return CombatStance.Defense;
            return CombatStance.Neutral;
        }
    }
}
