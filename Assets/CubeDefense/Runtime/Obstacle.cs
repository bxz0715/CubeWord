using UnityEngine;

namespace CubeDefense
{
    public enum DamageKind { BreachSingle, NormalSingle, Area }

    // 建筑同时是障碍；伤害入口负责校验，不能只依靠攻击方自觉过滤。
    public sealed class Obstacle : MonoBehaviour
    {
        public Vector2Int cell;
        public float health = 36f;
        public bool fires = true;
        DefenseGame game;
        float cooldown;
        public void Initialize(DefenseGame owner, Vector2Int position)
        { game = owner; cell = position; }

        // 只有“单体破障 + 攻击者此刻确实被本障碍挡住”才能扣血。
        public bool ReceiveDamage(float amount, DamageKind kind, Monster attacker)
        {
            if (amount <= 0 || kind != DamageKind.BreachSingle || attacker == null ||
                attacker.BlockingObstacle != this || !attacker.IsActuallyBlockedBy(this)) return false;
            health -= amount;
            if (health <= 0) game.RemoveObstacle(this);
            return true;
        }

        // 炮塔只查询怪物，不把建筑作为常规攻击目标。
        void Update()
        {
            if (!fires || game == null || !game.towersFire) return;
            cooldown -= Time.deltaTime;
            if (cooldown > 0) return;
            foreach (var monster in game.monsters)
            {
                if (monster == null || Vector3.Distance(transform.position, monster.transform.position) > 3.2f) continue;
                monster.TakeDamage(7f); cooldown = .65f; break;
            }
        }
    }
}
