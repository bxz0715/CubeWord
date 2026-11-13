using UnityEngine;

namespace CubeDefense
{
    public sealed class RangedMonster : Monster
    {
        float timer;
        // 常规远程攻击仅选择诱饵单位；障碍不在这个目标集合里。
        protected override void RegularAttack(float delta)
        {
            timer -= delta;
            if (timer > 0 || game.decoy == null || Vector3.Distance(transform.position, game.decoy.transform.position) > 3f) return;
            game.decoy.TakeDamage(3f); timer = 1f; game.normalHits++;
        }
    }
}
