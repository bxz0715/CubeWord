using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeDefense
{
    [Flags] public enum Directions { None = 0, East = 1, North = 2, West = 4, South = 8, All = 15 }
    // 可序列化的每格配置；行优先下标 = y * width + x。
    [Serializable] public sealed class MapCell
    {
        public TileDefinition tile;
        public Directions exits = Directions.All;
    }
    [Serializable] public sealed class RouteDefinition
    {
        public string name = "Route";
        public Vector2Int spawn;
        public Vector2Int goal;
        public bool required = true;
        public List<Vector2Int> nodes = new List<Vector2Int>();
        // 配置节点可重复；转成逐格路线时仍保留每次经过的下标。
        public List<Vector2Int> Expand(GridMap map, PathService paths)
        {
            var result = new List<Vector2Int> { spawn };
            var anchors = new List<Vector2Int>(nodes) { goal };
            foreach (var anchor in anchors)
            {
                var segment = paths.To(anchor).Path(result[result.Count - 1]);
                if (segment == null) return null;
                for (int i = 1; i < segment.Count; i++) result.Add(segment[i]);
            }
            return result;
        }
    }
    // 每局单独创建，既不写入地块资产，也不写回场景配置。
    public sealed class CellState
    {
        public Obstacle obstacle;
        public float burnRemaining;
    }
    public sealed class GridMap : MonoBehaviour
    {
        [Range(1, 20)] public int width = 16;
        [Range(1, 20)] public int height = 12;
        public List<MapCell> cells = new List<MapCell>();
        public List<RouteDefinition> routes = new List<RouteDefinition>();
        [NonSerialized] public CellState[] state;
        public static readonly Vector2Int[] Steps = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        // 调整地图尺寸，按坐标保留旧区域，新增区域使用空配置。
        public void Resize(int w, int h)
        {
            w = Mathf.Clamp(w, 1, 20); h = Mathf.Clamp(h, 1, 20);
            var old = cells; int oldWidth = width;
            cells = new List<MapCell>(w * h);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                cells.Add(x < width && y < height && y * oldWidth + x < old.Count ? old[y * oldWidth + x] : new MapCell());
            width = w; height = h;
        }
        // 开局清空每格独立状态；共享的地块配置保持不变。
        public void ResetState()
        {
            state = new CellState[width * height];
            for (int i = 0; i < state.Length; i++) state[i] = new CellState();
        }
        public bool Inside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
        public int Index(Vector2Int p) => p.y * width + p.x;
        public MapCell Cell(Vector2Int p) => Inside(p) && Index(p) < cells.Count ? cells[Index(p)] : null;
        public CellState State(Vector2Int p) => state != null && Inside(p) ? state[Index(p)] : null;
        public Vector3 World(Vector2Int p) => transform.position + new Vector3(p.x, 0, p.y);
        // 判断格子能否站立；blocked 用于尚未真正放置建筑时的试算。
        public bool Open(Vector2Int p, Vector2Int? blocked = null)
        {
            var c = Cell(p);
            return c != null && c.tile != null && c.tile.groundPassable && p != blocked && State(p)?.obstacle == null;
        }
        // 所有寻路统一使用这个有向边判定，反向搜索也检查原方向。
        public bool Edge(Vector2Int from, Vector2Int to, Vector2Int? blocked = null)
        {
            if (!Open(from, blocked) || !Open(to, blocked)) return false;
            Vector2Int d = to - from;
            for (int i = 0; i < 4; i++) if (d == Steps[i]) return (Cell(from).exits & (Directions)(1 << i)) != 0;
            return false;
        }
        void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                var p = new Vector2Int(x, y); var c = Cell(p);
                Gizmos.color = c?.tile != null ? c.tile.color : Color.gray;
                Gizmos.DrawCube(World(p) + Vector3.down * .12f, new Vector3(.92f, .2f, .92f));
            }
            foreach (var route in routes)
            {
                Gizmos.color = Color.green; Gizmos.DrawSphere(World(route.spawn) + Vector3.up * .3f, .25f);
                Gizmos.color = Color.cyan; Gizmos.DrawCube(World(route.goal) + Vector3.up * .3f, Vector3.one * .4f);
                Vector3 previous = World(route.spawn) + Vector3.up * .4f;
                foreach (var node in route.nodes)
                {
                    Vector3 next = World(node) + Vector3.up * .4f; Gizmos.DrawLine(previous, next); previous = next;
                }
                Gizmos.DrawLine(previous, World(route.goal) + Vector3.up * .4f);
            }
        }
    }
}
