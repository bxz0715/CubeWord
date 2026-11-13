using UnityEngine;

namespace CubeDefense
{
    [CreateAssetMenu(menuName = "CubeWorld/地块配置")]
    public sealed class TileDefinition : ScriptableObject
    {
        public string displayName;
        public Color color = Color.gray;
        public GameObject visualPrefab;
        public bool groundPassable = true;
        public bool buildable = true;
        [Range(0.1f, 1f)] public float speedMultiplier = 1f;
        public bool flammable;
        [Min(0.1f)] public float burnSeconds = 8f;
        [Min(0)] public float burnDamagePerSecond = 4f;
    }
}
