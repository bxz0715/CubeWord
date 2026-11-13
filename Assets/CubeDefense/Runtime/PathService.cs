using System.Collections.Generic;
using UnityEngine;

namespace CubeDefense
{
    // 某一目的格的反向广度优先搜索结果：记录每个可达格应走向的下一格。
    public sealed class ReverseField
    {
        readonly Dictionary<Vector2Int, Vector2Int> next = new Dictionary<Vector2Int, Vector2Int>();
        readonly Vector2Int goal;
        // 从 destination 向外搜索；blocked 是试建塔时额外视为被占用的一格。
        // 检查 predecessor -> v，而不是 v -> predecessor，避免把单向路反过来走。
        public ReverseField(GridMap map, Vector2Int destination, Vector2Int? blocked)
        {
            goal = destination;
            if (!map.Open(goal, blocked)) return;
            var queue = new Queue<Vector2Int>(); queue.Enqueue(goal); next[goal] = goal;
            while (queue.Count > 0)
            {
                var v = queue.Dequeue();
                foreach (var step in GridMap.Steps)
                {
                    var predecessor = v + step;
                    if (!next.ContainsKey(predecessor) && map.Edge(predecessor, v, blocked))
                    { next[predecessor] = v; queue.Enqueue(predecessor); }
                }
            }
        }
        public bool Reaches(Vector2Int p) => next.ContainsKey(p);
        // 从 start 顺着搜索记录还原到目标的路径；不可达时返回 null。
        public List<Vector2Int> Path(Vector2Int start)
        {
            if (!Reaches(start)) return null;
            var result = new List<Vector2Int> { start };
            while (start != goal) { start = next[start]; result.Add(start); }
            return result;
        }
    }
    // 一次地图状态对应一个缓存；地图占用变化后必须新建，防止复用过期结果。
    public sealed class PathService
    {
        readonly GridMap map;
        readonly Vector2Int? blocked;
        readonly Dictionary<Vector2Int, ReverseField> fields = new Dictionary<Vector2Int, ReverseField>();
        public int FieldCount => fields.Count;
        public PathService(GridMap map, Vector2Int? blocked = null) { this.map = map; this.blocked = blocked; }
        // 按目的格缓存搜索结果，让不同怪物的起点共用同一轮搜索。
        public ReverseField To(Vector2Int destination)
        {
            if (!fields.TryGetValue(destination, out var field))
            { field = new ReverseField(map, destination, blocked); fields.Add(destination, field); }
            return field;
        }
        // 只检查关卡要求保通的出怪口，不要求场上每一只怪物都可达目标。
        public bool RequiredConnected()
        {
            foreach (var route in map.routes) if (route.required && !To(route.goal).Reaches(route.spawn)) return false;
            return true;
        }
        // 检查从 start 下标开始的每条相邻边，包括地块、占用和出边方向。
        public bool Valid(IReadOnlyList<Vector2Int> route, int start = 0)
        {
            if (route == null || route.Count == 0 || !map.Open(route[start], blocked)) return false;
            for (int i = start + 1; i < route.Count; i++) if (!map.Edge(route[i - 1], route[i], blocked)) return false;
            return true;
        }
        // 输入旧逐格列表、当前位置下标、原目标，输出独立的新列表；完全受困返回 null。
        // 1. 找第一条失效边。2. 以待保留节点为目的反向搜索。
        // 3. 从断点前向前倒查可接入位置，保留最长合法前缀。
        // 4. 拼接“旧前缀 + 绕行 + 旧后缀”，继续处理后续的重复堵点。
        // 后缀按下标查找，不能用坐标去重，否则会误删尚未走过的绕圈。
        public List<Vector2Int> Repair(IReadOnlyList<Vector2Int> old, int start, Vector2Int goal)
        {
            if (old == null || start >= old.Count || !To(goal).Reaches(old[start])) return null;
            var result = new List<Vector2Int>();
            for (int i = start; i < old.Count; i++) result.Add(old[i]);
            int guard = result.Count + map.width * map.height + 1;
            while (guard-- > 0)
            {
                int broken = -1;
                for (int i = 1; i < result.Count; i++) if (!map.Edge(result[i - 1], result[i], blocked)) { broken = i; break; }
                if (broken < 0) return result;
                bool joined = false;
                // 优先保存绕圈节点；只有该次经过无法接回，才推进到更晚的下标。
                for (int suffix = broken; suffix < result.Count && !joined; suffix++)
                {
                    var field = To(result[suffix]);
                    for (int prefix = broken - 1; prefix >= 0; prefix--)
                    {
                        var detour = field.Path(result[prefix]);
                        if (detour == null) continue;
                        var repaired = result.GetRange(0, prefix + 1);
                        for (int j = 1; j < detour.Count; j++) repaired.Add(detour[j]);
                        for (int j = suffix + 1; j < result.Count; j++) repaired.Add(result[j]);
                        result = repaired; joined = true; break;
                    }
                }
                if (!joined) return To(goal).Path(result[0]);
            }
            return To(goal).Path(result[0]);
        }
    }
}
