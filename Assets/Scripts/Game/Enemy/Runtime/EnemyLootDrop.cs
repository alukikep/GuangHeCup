using UnityEngine;

namespace Game.Enemy
{
    /// <summary>
    /// 挂在掉落物 Prefab 上，由 EnemyBrain 在掉落时写入数量。
    /// </summary>
    public sealed class EnemyLootDrop : MonoBehaviour
    {
        [SerializeField] [Min(0)] private int m_amount = 1;

        public int Amount
        {
            get => m_amount;
            set => m_amount = Mathf.Max(0, value);
        }
    }
}
