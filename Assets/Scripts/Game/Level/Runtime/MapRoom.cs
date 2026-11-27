using UnityEngine;

namespace Game.Level
{
    [DisallowMultipleComponent]
    public sealed class MapRoom : MonoBehaviour
    {
        public const float CellSize = 0.5f;
        [SerializeField, HideInInspector] private Vector2Int dimensions = Vector2Int.one;
        public Vector2Int Dimensions => dimensions;
        public Rect Bounds => new Rect(transform.position, (Vector2)dimensions * CellSize);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(Bounds.center, Bounds.size);
        }
    }
}
