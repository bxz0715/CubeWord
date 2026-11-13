using System.Collections.Generic;
using UnityEngine;

namespace CubeDefense
{
    // 每只怪物持有独立路径和进度；共享 RouteDefinition 只读。
    public class Monster : MonoBehaviour
    {
        public RouteDefinition Route { get; private set; }
        public Vector2Int CurrentCell { get; private set; }
        public int NextIndex { get; private set; }
        public Obstacle BlockingObstacle { get; private set; }
        public int RepairCount { get; private set; }
        public float health = 45f;
        public float speed = 1.8f;
        protected DefenseGame game;
        readonly List<Vector2Int> plan = new List<Vector2Int>();
        bool stranded;
        bool recenter;
        float attackTimer;
        public IReadOnlyList<Vector2Int> Plan => plan;

        // 复制本次使用的路线，防止一只怪物的绕行修改其他怪物。
        public void Initialize(DefenseGame owner, RouteDefinition route, List<Vector2Int> initial)
        {
            game = owner; Route = route; CurrentCell = initial[0];
            plan.AddRange(initial); NextIndex = 1;
            transform.position = game.map.World(CurrentCell) + Vector3.up * .48f;
        }

        // 地图变化：只修复未走完的失效边；开放道路时仅唤醒受困者。
        // 优先保留自己的旧路线进度。接不上旧后缀时，才从当前格查到原目标。
        public void MapChanged(PathService paths, bool opened)
        {
            if (opened && !stranded) return;
            var remaining = new List<Vector2Int> { CurrentCell };
            for (int i = NextIndex; i < plan.Count; i++) remaining.Add(plan[i]);
            if (!stranded && paths.Valid(remaining)) return;
            RepairCount++;
            var replacement = paths.Repair(remaining, 0, Route.goal);
            BlockingObstacle = null;
            stranded = replacement == null;
            // 无路可走时保留旧列表，继续走到真正挡路的第一处障碍。
            if (replacement != null)
            {
                plan.Clear(); plan.AddRange(replacement); NextIndex = 1;
            }
            recenter = true;
        }

        // 校验实际堵点：目标不可达、紧邻的下一格被该障碍占用，且到达当前格中心。
        public bool IsActuallyBlockedBy(Obstacle obstacle)
        {
            if (!stranded || obstacle == null || NextIndex >= plan.Count || game == null) return false;
            var next = plan[NextIndex];
            if (next != obstacle.cell || game.map.State(next)?.obstacle != obstacle ||
                (next - CurrentCell).sqrMagnitude != 1 ||
                Vector3.Distance(transform.position, game.map.World(CurrentCell) + Vector3.up * .48f) > .05f) return false;
            return !game.paths.To(Route.goal).Reaches(CurrentCell);
        }

        // 每帧依次处理地块伤害、回到当前格中心、破障或移动。
        // 到达下一格中心才推进下标，防止把尚未走完的一条边记为已完成。
        void Update()
        {
            if (game == null) return;
            float delta = Time.deltaTime;
            var state = game.map.State(CurrentCell);
            if (state != null && state.burnRemaining > 0) TakeDamage(game.map.Cell(CurrentCell).tile.burnDamagePerSecond * delta);
            if (health <= 0) return;
            attackTimer -= delta;
            Vector3 center = game.map.World(CurrentCell) + Vector3.up * .48f;
            if (recenter)
            {
                transform.position = Vector3.MoveTowards(transform.position, center, speed * delta);
                if ((transform.position - center).sqrMagnitude < .0001f) recenter = false;
                return;
            }
            if (NextIndex >= plan.Count)
            {
                if (CurrentCell == Route.goal) { game.ReachedGoal(this); return; }
                return;
            }
            Vector2Int next = plan[NextIndex];
            if (!game.map.Edge(CurrentCell, next))
            {
                BlockingObstacle = game.map.State(next)?.obstacle;
                if (BlockingObstacle != null && IsActuallyBlockedBy(BlockingObstacle) && attackTimer <= 0)
                { BlockingObstacle.ReceiveDamage(6f, DamageKind.BreachSingle, this); attackTimer = .6f; }
                return;
            }
            BlockingObstacle = null;
            RegularAttack(delta);
            float multiplier = game.map.Cell(CurrentCell).tile.speedMultiplier;
            Vector3 destination = game.map.World(next) + Vector3.up * .48f;
            transform.position = Vector3.MoveTowards(transform.position, destination, speed * multiplier * delta);
            if ((transform.position - destination).sqrMagnitude < .0001f) { CurrentCell = next; NextIndex++; }
        }

        // 普通怪无常规远程技能；子类仅覆盖常规技能，复用上面的破障流程。
        protected virtual void RegularAttack(float delta) { }
        public void TakeDamage(float amount)
        {
            health -= amount;
            if (health <= 0) game.Kill(this);
        }
    }
}
