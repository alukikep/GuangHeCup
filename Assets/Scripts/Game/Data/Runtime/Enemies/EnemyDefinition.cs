using System;
using UnityEngine;

namespace Game.Data
{
    [Serializable]
    public struct EnemyAttackData
    {
        [Min(0f)] public float windup;
        [Min(0f)] public float active;
        [Min(0f)] public float recovery;
        [Min(0f)] public float cooldown;
        [Min(0)] public int damage;

        public float TotalDuration => windup + active + recovery;
    }

    [Serializable]
    public sealed class LootEntry
    {
        public GameObject itemPrefab;
        [Range(0f, 1f)] public float dropChance = 0.3f;
        [Min(1)] public int minAmount = 1;
        [Min(1)] public int maxAmount = 1;
    }

    [CreateAssetMenu(menuName = "Game/Enemies/Enemy Definition", fileName = "Enemy_New")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Enemy";

        [Header("Health")]
        [Min(1)] public int maxHealth = 45;

        [Header("Movement")]
        [Min(0f)] public float patrolSpeed = 1.5f;
        [Min(0f)] public float chaseSpeed = 2.5f;
        [Min(0f)] public float patrolRadius = 3f;
        public Vector2 patrolIdleRange = new Vector2(0.4f, 1.4f);
        [Min(0.01f)] public float arriveDistance = 0.08f;

        [Header("Alert")]
        [Min(0f)] public float loseSightTime = 3f;
        [Min(0f)] public float repositionRadius = 2.5f;
        [Min(0f)] public float repositionDuration = 4f;
        [Min(0f)] public float playerAttackMemory = 0.5f;

        [Header("Attack")]
        public EnemyAttackData attack = new EnemyAttackData
        {
            windup = 0.35f,
            active = 0.10f,
            recovery = 0.40f,
            cooldown = 1.0f,
            damage = 8,
        };

        [Header("Defence")]
        public bool guardWhenAttacked = true;
        [Min(0f)] public float guardDuration = 0.45f;

        [Header("Death")]
        [Min(0f)] public float deathDestroyDelay = 1.5f;
        [Min(0f)] public float lootScatterRadius = 0.35f;
        public LootEntry[] loot = Array.Empty<LootEntry>();
    }
}
