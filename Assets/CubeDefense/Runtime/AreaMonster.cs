using UnityEngine;

namespace CubeDefense
{
    public sealed class AreaMonster : Monster
    {
        float timer;
        // 范围技能只遍历可受常规伤害的单位，不遍历障碍。
        protected override void RegularAttack(float delta)
        {
            timer -= delta;
            if (timer > 0) return;
            game.AreaDamage(transform.position, 2.5f, 2f); timer = 1.5f;
        }
    }
}
