using UnityEngine;

namespace CubeDefense
{
    // 演示常规远程/范围攻击的友方诱饵，生命耗尽后复原以便持续观察。
    public sealed class CombatDummy : MonoBehaviour
    {
        public float health = 100f;
        public void TakeDamage(float amount) { health -= amount; if (health <= 0) health = 100f; }
    }
}
