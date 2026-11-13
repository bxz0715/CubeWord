using System.Collections.Generic;
using UnityEngine;

namespace CubeDefense
{
    public sealed class DefenseGame : MonoBehaviour
    {
        public GridMap map;
        public readonly List<Monster> monsters = new List<Monster>();
        public PathService paths;
        public CombatDummy decoy;
        public bool towersFire;
        public bool autoSpawn = true;
        public int normalHits, areaHits, escaped, killed;
        readonly Dictionary<RouteDefinition, List<Vector2Int>> presets = new Dictionary<RouteDefinition, List<Vector2Int>>();
        readonly List<Material> materials = new List<Material>();
        Renderer[] ground;
        float spawnTimer;
        int wave;
        int mode;
        Vector2Int selected = new Vector2Int(4, 2);
        string status = "Select a cell. Build / ignite / inspect.";

        // 初始化运行时状态并复制展开路线；场景配置资产不会被运行时改写。
        void Start()
        {
            map.ResetState(); paths = new PathService(map);
            foreach (var route in map.routes)
            {
                var expanded = route.Expand(map, paths);
                if (expanded != null) presets.Add(route, expanded);
                else Debug.LogError("路线无法展开: " + route.name);
            }
            ground = new Renderer[map.width * map.height];
            for (int y = 0; y < map.height; y++) for (int x = 0; x < map.width; x++)
            {
                var p = new Vector2Int(x, y); var tile = map.Cell(p)?.tile;
                var obj = tile != null && tile.visualPrefab != null ? Instantiate(tile.visualPrefab) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "Cell_" + x + "_" + y; obj.transform.SetParent(transform);
                obj.transform.position = map.World(p) + Vector3.down * .15f;
                if (tile == null || tile.visualPrefab == null) obj.transform.localScale = new Vector3(.94f, tile != null && !tile.groundPassable ? .6f : .22f, .94f);
                var renderer = obj.GetComponentInChildren<Renderer>();
                if (renderer != null) { renderer.material = Material(tile != null ? tile.color : Color.gray); ground[map.Index(p)] = renderer; }
                if (map.Cell(p).exits != Directions.All) Label(p, "E >", Color.yellow, .08f);
            }
            for (int i = 0; i < map.routes.Count; i++)
            {
                var route = map.routes[i]; Label(route.spawn, "S" + (i + 1), Color.green, .15f);
                Label(route.goal, "G" + (i + 1), Color.cyan, .15f);
                if (presets.TryGetValue(route, out var nodes)) DrawRoute(nodes, i);
            }
            var dummy = Cube("Friendly decoy", map.World(new Vector2Int(6, 4)) + Vector3.up * .45f, new Vector3(.6f, .9f, .6f), Color.cyan);
            decoy = dummy.AddComponent<CombatDummy>();
        }

        Material Material(Color color)
        {
            var material = new Material(Shader.Find("Standard")); material.color = color; materials.Add(material); return material;
        }
        GameObject Cube(string title, Vector3 position, Vector3 scale, Color color)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = title;
            obj.transform.SetParent(transform); obj.transform.position = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = Material(color); return obj;
        }
        void Label(Vector2Int p, string text, Color color, float elevation)
        {
            var obj = new GameObject(text); obj.transform.SetParent(transform); obj.transform.position = map.World(p) + Vector3.up * elevation;
            obj.transform.rotation = Quaternion.Euler(90, 0, 0);
            var label = obj.AddComponent<TextMesh>(); label.text = text; label.fontSize = 50; label.characterSize = .07f; label.anchor = TextAnchor.MiddleCenter; label.color = color;
        }
        void DrawRoute(List<Vector2Int> nodes, int number)
        {
            var obj = new GameObject("Preset route " + number); obj.transform.SetParent(transform);
            var line = obj.AddComponent<LineRenderer>(); line.positionCount = nodes.Count; line.startWidth = line.endWidth = .055f;
            var material = new Material(Shader.Find("Sprites/Default")); materials.Add(material); line.sharedMaterial = material;
            line.startColor = line.endColor = number == 0 ? new Color(1f, .7f, .2f) : new Color(.2f, .9f, 1f);
            for (int i = 0; i < nodes.Count; i++) line.SetPosition(i, map.World(nodes[i]) + Vector3.up * .025f);
        }

        void Update()
        {
            // 为右侧操作面板留出空间，窗口尺寸改变时仍能看到完整地图。
            var camera = Camera.main;
            if (camera != null)
            {
                float usableWidth = Mathf.Max(100, Screen.width - 310);
                camera.orthographicSize = Mathf.Max((map.height + 2) * .5f, (map.width + 2) * .5f * Screen.height / usableWidth);
                camera.transform.position = map.transform.position + new Vector3((map.width - 1) * .5f + 310f / Screen.height * camera.orthographicSize, 22, (map.height - 1) * .5f);
            }
            if (Input.GetKeyDown(KeyCode.Space)) autoSpawn = !autoSpawn;
            if (Input.GetKeyDown(KeyCode.Alpha1)) mode = 0;
            if (Input.GetKeyDown(KeyCode.Alpha2)) mode = 1;
            if (Input.GetKeyDown(KeyCode.Alpha3)) mode = 2;
            if (Input.GetMouseButtonDown(0) && Input.mousePosition.x < Screen.width - 310)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                var plane = new Plane(Vector3.up, map.transform.position);
                if (plane.Raycast(ray, out float distance))
                {
                    Vector3 point = ray.GetPoint(distance) - map.transform.position;
                    selected = new Vector2Int(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z));
                    if (map.Inside(selected))
                    {
                        if (mode == 0) TryBuild(selected);
                        if (mode == 1) Ignite(selected);
                    }
                }
            }
            spawnTimer -= Time.deltaTime;
            if (autoSpawn && spawnTimer <= 0 && monsters.Count < 70)
            { SpawnWave(); spawnTimer = 3.5f; }
            for (int i = 0; i < map.state.Length; i++)
            {
                var state = map.state[i];
                if (state.burnRemaining <= 0) continue;
                state.burnRemaining = Mathf.Max(0, state.burnRemaining - Time.deltaTime);
                if (ground[i] != null) ground[i].sharedMaterial.color = state.burnRemaining > 0 ? Color.Lerp(Color.red, Color.yellow, Mathf.PingPong(Time.time * 4f, 1)) : map.cells[i].tile.color;
            }
        }

        // 试算只传入候选堵点，不临时修改地图。必须保障每个 required 出怪口到自己的目标。
        public bool TryBuild(Vector2Int p)
        {
            if (!map.Open(p) || !map.Cell(p).tile.buildable) { status = "Rejected: tile is not buildable."; return false; }
            foreach (var route in map.routes) if (route.spawn == p || route.goal == p) { status = "Rejected: spawn / goal."; return false; }
            foreach (var monster in monsters)
                if (monster.CurrentCell == p || (monster.NextIndex < monster.Plan.Count && monster.Plan[monster.NextIndex] == p &&
                    Vector3.Distance(monster.transform.position, map.World(p) + Vector3.up * .48f) < .7f))
                { status = "Rejected: monster occupies cell."; return false; }
            if (!new PathService(map, p).RequiredConnected()) { status = "Rejected: a required spawn loses its goal."; return false; }
            var tower = Cube("Tower " + p, map.World(p) + Vector3.up * .55f, new Vector3(.75f, 1.1f, .75f), new Color(.3f, .55f, .9f)).AddComponent<Obstacle>();
            tower.Initialize(this, p); map.State(p).obstacle = tower;
            NotifyChange(false); status = "Built at " + p + ". Affected monsters repaired."; return true;
        }

        // 同一次变化只创建一个 PathService，各怪物共享按目的格缓存的反向搜索场。
        void NotifyChange(bool opened)
        {
            paths = new PathService(map);
            foreach (var monster in monsters.ToArray()) if (monster != null) monster.MapChanged(paths, opened);
        }
        public void RemoveObstacle(Obstacle obstacle)
        {
            map.State(obstacle.cell).obstacle = null; Destroy(obstacle.gameObject); NotifyChange(true);
            status = "Obstacle destroyed. Stranded monsters can move again.";
        }
        // 只写选中格的燃烧计时器；不修改所有草格共享的 TileDefinition。
        public void Ignite(Vector2Int p)
        {
            var tile = map.Cell(p)?.tile;
            if (tile == null || !tile.flammable) { status = "Choose a grass cell (green)."; return; }
            map.State(p).burnRemaining = tile.burnSeconds; status = "Only " + p + " is burning.";
        }
        // 新怪重新检查只读预设路线：已畅通时用原路线，否则生成自己的绕行副本。
        public void SpawnWave()
        {
            foreach (var route in map.routes)
            {
                if (!presets.TryGetValue(route, out var preset)) continue;
                var plan = paths.Repair(preset, 0, route.goal);
                if (plan != null) Spawn(route, plan, wave % 3);
            }
            wave++;
        }
        public Monster Spawn(RouteDefinition route, List<Vector2Int> plan, int kind)
        {
            var obj = Cube(kind == 0 ? "Monster" : kind == 1 ? "Ranged monster" : "Area monster", Vector3.zero, Vector3.one * .55f,
                kind == 0 ? Color.red : kind == 1 ? Color.magenta : Color.yellow);
            Monster monster = kind == 0 ? obj.AddComponent<Monster>() : kind == 1 ? (Monster)obj.AddComponent<RangedMonster>() : obj.AddComponent<AreaMonster>();
            monster.Initialize(this, route, plan); monsters.Add(monster); return monster;
        }
        public void ReachedGoal(Monster monster) { escaped++; monsters.Remove(monster); Destroy(monster.gameObject); }
        public void Kill(Monster monster) { killed++; monsters.Remove(monster); Destroy(monster.gameObject); }
        public void AreaDamage(Vector3 center, float radius, float amount)
        {
            if (decoy != null && Vector3.Distance(center, decoy.transform.position) <= radius)
            { decoy.TakeDamage(amount); areaHits++; }
        }

        // 可复现验收：单向格只能向东走，堵东侧后此怪受困，但两个出怪口依然通畅。
        public void TrapDemo()
        {
            var p = new Vector2Int(4, 5); var blocker = new Vector2Int(5, 5);
            if (map.Cell(p).exits != Directions.East) { status = "Demo requires the original east-only cell (4,5)."; return; }
            if (map.State(blocker).obstacle != null) { status = "Clear (5,5) before repeating trap demo."; return; }
            var plan = paths.To(map.routes[0].goal).Path(p);
            if (plan == null || plan.Count < 2 || plan[1] != blocker) { status = "Restore demo map to run trap test."; return; }
            autoSpawn = false; towersFire = false;
            var monster = Spawn(map.routes[0], plan, 1); monster.speed = 1.2f;
            if (!TryBuild(blocker)) { monsters.Remove(monster); Destroy(monster.gameObject); return; }
            selected = blocker; status = "Trapped ranged monster at (4,5): watch breach -> resume.";
        }

        // 从伤害接收端直接注入非法伤害，验证不能绕过选敌层的过滤。
        public void DamageProbe()
        {
            var obstacle = map.State(selected)?.obstacle;
            if (obstacle == null) { status = "Inspect a tower first, then run damage probe."; return; }
            float before = obstacle.health;
            bool normal = obstacle.ReceiveDamage(999, DamageKind.NormalSingle, monsters.Count > 0 ? monsters[0] : null);
            bool area = obstacle.ReceiveDamage(999, DamageKind.Area, monsters.Count > 0 ? monsters[0] : null);
            bool fake = obstacle.ReceiveDamage(999, DamageKind.BreachSingle, null);
            status = !normal && !area && !fake && before == obstacle.health ? "PASS: normal / area / fake breach rejected." : "FAIL: damage gate.";
            Debug.Log(status);
        }

        void OnGUI()
        {
            if (map == null || map.state == null) return;
            GUILayout.BeginArea(new Rect(Screen.width - 305, 10, 295, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("CUBE WORLD / GRID DEFENSE");
            GUILayout.Label("1 Build  |  2 Ignite  |  3 Inspect");
            mode = GUILayout.SelectionGrid(mode, new[] { "Build", "Ignite", "Inspect" }, 3);
            autoSpawn = GUILayout.Toggle(autoSpawn, "Auto spawn (Space)");
            towersFire = GUILayout.Toggle(towersFire, "Tower fire (off = observe routes)");
            if (GUILayout.Button("Spawn one wave")) SpawnWave();
            if (GUILayout.Button("Trap demo: east-only pocket")) TrapDemo();
            if (GUILayout.Button("Probe: reject normal / area damage")) DamageProbe();
            if (GUILayout.Button("Restart demo")) UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            GUILayout.Space(10);
            GUILayout.Label("Selected: " + selected);
            var cell = map.Cell(selected); var state = map.State(selected);
            if (cell?.tile != null) GUILayout.Label(cell.tile.displayName + " | exits: " + cell.exits);
            if (state != null) GUILayout.Label("Fire: " + state.burnRemaining.ToString("F1") + "s | Tower HP: " + (state.obstacle != null ? state.obstacle.health.ToString("F0") : "--"));
            GUILayout.Label("Monsters " + monsters.Count + " | escaped " + escaped + " | killed " + killed);
            GUILayout.Label("Normal hits " + normalHits + " | area hits " + areaHits);
            foreach (var route in map.routes)
            {
                int alive = 0, repairs = 0;
                foreach (var monster in monsters) if (monster.Route == route) { alive++; repairs += monster.RepairCount; }
                GUILayout.Label(route.name + ": " + alive + " alive / " + repairs + " repairs");
            }
            GUILayout.Label("Shared BFS fields: " + paths.FieldCount);
            GUILayout.Label(status);
            GUILayout.Space(8);
            GUILayout.Label("Orange: loop route -> G1\nCyan: independent route -> G2\nGreen tiles: grass; brown: slow swamp\nBlue water: impassable; E >: one-way\nTrap demo pauses auto spawning.\nSelect Inspect to check tower HP.");
            GUILayout.EndArea();
            if (map.Inside(selected))
            {
                Vector3 screen = Camera.main.WorldToScreenPoint(map.World(selected));
                GUI.Box(new Rect(screen.x - 15, Screen.height - screen.y - 15, 30, 30), "+");
            }
        }
        void OnDestroy() { foreach (var material in materials) if (material != null) Destroy(material); }
    }
}
