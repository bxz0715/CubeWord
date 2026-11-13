using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeDefense.Editor
{
    // 二维网格编辑器：数据直接保存在场景组件，Undo 记录每次编辑前的快照。
    public sealed class MapEditorWindow : EditorWindow
    {
        GridMap map;
        TileDefinition brush;
        int tool, routeIndex, width = 16, height = 12;
        Directions exits = Directions.All;
        Vector2 scroll;
        readonly string[] tools = { "刷地块", "出怪口", "目标点", "追加路点", "通行方向" };

        [MenuItem("工具/CubeWorld/地图编辑器")]
        public static void Open() { GetWindow<MapEditorWindow>("CubeWorld 地图").minSize = new Vector2(620, 600); }
        void OnEnable() { Undo.undoRedoPerformed += Repaint; }
        void OnDisable() { Undo.undoRedoPerformed -= Repaint; }
        void Record(string operation) { Undo.RecordObject(map, operation); }
        void Dirty() { EditorUtility.SetDirty(map); EditorSceneManager.MarkSceneDirty(map.gameObject.scene); SceneView.RepaintAll(); }

        void OnGUI()
        {
            if (map == null) map = FindObjectOfType<GridMap>();
            map = (GridMap)EditorGUILayout.ObjectField("场景地图", map, typeof(GridMap), true);
            if (map == null) { EditorGUILayout.HelpBox("打开 Assets/CubeDefense/Scenes/CubeWorld_Demo.unity。", MessageType.Info); return; }
            if (Application.isPlaying) { EditorGUILayout.HelpBox("请退出运行模式后编辑地图。", MessageType.Info); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("当前尺寸", map.width + " × " + map.height + "（最大 20 × 20）");
            EditorGUILayout.BeginHorizontal();
            width = EditorGUILayout.IntField("新宽度", width); height = EditorGUILayout.IntField("新高度", height);
            if (GUILayout.Button("调整尺寸")) { Record("调整地图尺寸"); map.Resize(width, height); Dirty(); }
            EditorGUILayout.EndHorizontal();
            tool = GUILayout.Toolbar(tool, tools);
            brush = (TileDefinition)EditorGUILayout.ObjectField("地块笔刷", brush, typeof(TileDefinition), false);
            exits = (Directions)EditorGUILayout.EnumFlagsField("允许离开当前格的方向", exits);
            EditorGUILayout.HelpBox("方向表示出边：东 +X，北 +Y。反方向是否可走由邻格决定。路点可重复；非相邻节点会自动寻路展开。", MessageType.None);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("新增路线")) { Record("新增路线"); map.routes.Add(new RouteDefinition { name = "Route " + (map.routes.Count + 1) }); routeIndex = map.routes.Count - 1; Dirty(); }
            if (GUILayout.Button("保存场景")) EditorSceneManager.SaveScene(map.gameObject.scene);
            if (GUILayout.Button("验证路线")) ValidateMap();
            EditorGUILayout.EndHorizontal();
            if (map.routes.Count > 0)
            {
                var names = map.routes.ConvertAll(r => r.name).ToArray();
                routeIndex = Mathf.Clamp(routeIndex, 0, map.routes.Count - 1);
                routeIndex = EditorGUILayout.Popup("编辑路线", routeIndex, names);
                var route = map.routes[routeIndex];
                EditorGUI.BeginChangeCheck();
                string title = EditorGUILayout.TextField("路线名", route.name);
                bool required = EditorGUILayout.Toggle("建塔时保证出怪口连通", route.required);
                Vector2Int spawn = EditorGUILayout.Vector2IntField("出怪口", route.spawn);
                Vector2Int goal = EditorGUILayout.Vector2IntField("目标点", route.goal);
                if (EditorGUI.EndChangeCheck()) { Record("编辑路线属性"); route.name = title; route.required = required; route.spawn = spawn; route.goal = goal; Dirty(); }
            }
            DrawGrid();
            if (map.routes.Count > 0)
            {
                var route = map.routes[routeIndex];
                EditorGUILayout.LabelField("有序路点（序号区分同一坐标的不同次经过）");
                for (int i = 0; i < route.nodes.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginChangeCheck();
                    var node = EditorGUILayout.Vector2IntField("节点 " + i, route.nodes[i]);
                    if (EditorGUI.EndChangeCheck()) { Record("修改路点"); route.nodes[i] = node; Dirty(); }
                    if (GUILayout.Button("↑", GUILayout.Width(28)) && i > 0) { Record("上移路点"); var old = route.nodes[i - 1]; route.nodes[i - 1] = route.nodes[i]; route.nodes[i] = old; Dirty(); }
                    if (GUILayout.Button("↓", GUILayout.Width(28)) && i + 1 < route.nodes.Count) { Record("下移路点"); var old = route.nodes[i + 1]; route.nodes[i + 1] = route.nodes[i]; route.nodes[i] = old; Dirty(); }
                    bool remove = GUILayout.Button("删除", GUILayout.Width(45));
                    EditorGUILayout.EndHorizontal();
                    if (remove) { Record("删除路点"); route.nodes.RemoveAt(i); Dirty(); break; }
                }
                if (GUILayout.Button("删除当前路线")) { Record("删除路线"); map.routes.RemoveAt(routeIndex); Dirty(); }
            }
            EditorGUILayout.EndScrollView();
        }

        // 把屏幕格子映射到整数坐标；刷地块和方向支持拖动，路点只响应单次点击。
        void DrawGrid()
        {
            float size = Mathf.Clamp((position.width - 40) / map.width, 18, 36);
            Rect area = GUILayoutUtility.GetRect(map.width * size, map.height * size);
            var e = Event.current;
            for (int y = map.height - 1; y >= 0; y--) for (int x = 0; x < map.width; x++)
            {
                var p = new Vector2Int(x, y); var c = map.Cell(p);
                Rect rect = new Rect(area.x + x * size, area.y + (map.height - 1 - y) * size, size - 1, size - 1);
                EditorGUI.DrawRect(rect, c?.tile != null ? c.tile.color : Color.gray);
                string label = x + "," + y;
                if (map.routes.Count > 0)
                {
                    var r = map.routes[routeIndex];
                    if (r.spawn == p) label = "S";
                    else if (r.goal == p) label = "G";
                    else
                    {
                        string indices = "";
                        for (int i = 0; i < r.nodes.Count; i++) if (r.nodes[i] == p) indices += (indices.Length == 0 ? "" : ",") + i;
                        if (indices.Length > 0) label = indices;
                    }
                }
                GUI.Label(rect, new GUIContent(label, p + " | " + c?.exits), EditorStyles.miniLabel);
                if (rect.Contains(e.mousePosition) && e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag && (tool == 0 || tool == 4)))
                {
                    Record(tools[tool]);
                    if (tool == 0 && brush != null) c.tile = brush;
                    if (tool == 4) c.exits = exits;
                    if (map.routes.Count > 0)
                    {
                        var r = map.routes[routeIndex];
                        if (tool == 1) r.spawn = p;
                        if (tool == 2) r.goal = p;
                        if (tool == 3) r.nodes.Add(p);
                    }
                    Dirty(); e.Use(); Repaint();
                }
            }
        }
        void ValidateMap()
        {
            map.ResetState(); var paths = new PathService(map);
            string result = "";
            foreach (var route in map.routes)
            {
                var expanded = route.Expand(map, paths);
                result += route.name + (expanded == null ? "：无效，检查地块/方向/路点\n" : "：有效，共 " + expanded.Count + " 个逐格节点\n");
            }
            EditorUtility.DisplayDialog("路线验证", result.Length > 0 ? result : "没有配置路线", "确定");
        }
    }

    [CustomEditor(typeof(GridMap))]
    public sealed class GridMapInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var map = (GridMap)target;
            EditorGUILayout.LabelField("地图", map.width + " × " + map.height);
            EditorGUILayout.LabelField("路线数量", map.routes.Count.ToString());
            if (GUILayout.Button("打开二维地图编辑器")) MapEditorWindow.Open();
            EditorGUILayout.HelpBox("地块与路线保存在这个场景组件中。编辑后保存场景；地块外观与规则在 TileDefinition 资产中修改。", MessageType.Info);
        }
    }
}
