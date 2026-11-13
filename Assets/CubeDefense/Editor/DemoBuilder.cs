using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeDefense.Editor
{
    public static class DemoBuilder
    {
        public const string ScenePath = "Assets/CubeDefense/Scenes/CubeWorld_Demo.unity";

        // 命令行入口：生成演示资产和场景，再执行确定性的逻辑验收。
        public static void BuildAndValidate()
        {
            Build(); RunValidation();
        }

        [MenuItem("工具/CubeWorld/创建或重置演示关卡")]
        public static void BuildMenu()
        {
            if (Application.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("重置演示关卡", "这会覆盖演示场景；自行编辑的地图请先另存场景。", "重置", "取消")) return;
            Build();
        }

        static TileDefinition Tile(string file, string title, Color color, bool pass, bool build, float speed = 1, bool fire = false)
        {
            string path = "Assets/CubeDefense/Tiles/" + file + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<TileDefinition>(path);
            if (asset == null) { asset = ScriptableObject.CreateInstance<TileDefinition>(); AssetDatabase.CreateAsset(asset, path); }
            asset.displayName = title; asset.color = color; asset.groundPassable = pass; asset.buildable = build;
            asset.speedMultiplier = speed; asset.flammable = fire; EditorUtility.SetDirty(asset); return asset;
        }

        public static void Build()
        {
            Directory.CreateDirectory("Assets/CubeDefense/Tiles"); Directory.CreateDirectory("Assets/CubeDefense/Scenes"); AssetDatabase.Refresh();
            var ground = Tile("Ground", "Ground / 普通地面", new Color(.55f, .6f, .65f), true, true);
            var wall = Tile("Wall", "Wall / 墙", new Color(.23f, .25f, .3f), false, false);
            var water = Tile("ShallowWater", "Water / 浅水", new Color(.15f, .5f, .8f), false, false);
            var swamp = Tile("Swamp", "Swamp / 沼泽", new Color(.4f, .32f, .15f), true, true, .4f);
            var grass = Tile("Grass", "Grass / 可燃草", new Color(.3f, .7f, .25f), true, true, 1f, true);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("CubeWorld Grid Defense"); var map = root.AddComponent<GridMap>(); map.Resize(16, 12);
            foreach (var cell in map.cells) cell.tile = ground;
            for (int x = 0; x < map.width; x++) { map.Cell(new Vector2Int(x, 0)).tile = wall; map.Cell(new Vector2Int(x, 11)).tile = wall; }
            for (int y = 0; y < map.height; y++) { map.Cell(new Vector2Int(0, y)).tile = wall; map.Cell(new Vector2Int(15, y)).tile = wall; }
            // 一整行墙隔开两条路线，便于观察另一条路线不受修补影响。
            for (int x = 1; x < 15; x++) map.Cell(new Vector2Int(x, 7)).tile = wall;
            for (int x = 9; x <= 11; x++) map.Cell(new Vector2Int(x, 4)).tile = water;
            for (int x = 7; x <= 9; x++) map.Cell(new Vector2Int(x, 9)).tile = swamp;
            for (int x = 10; x <= 13; x++) map.Cell(new Vector2Int(x, 2)).tile = grass;
            map.Cell(new Vector2Int(4, 5)).exits = Directions.East;
            map.routes.Add(new RouteDefinition
            {
                name = "A - loop to G1", spawn = new Vector2Int(1, 2), goal = new Vector2Int(14, 2),
                nodes = new List<Vector2Int> { new Vector2Int(4, 2), new Vector2Int(4, 5), new Vector2Int(7, 5), new Vector2Int(7, 2), new Vector2Int(4, 2) }
            });
            map.routes.Add(new RouteDefinition
            {
                name = "B - swamp to G2", spawn = new Vector2Int(1, 9), goal = new Vector2Int(14, 9),
                nodes = new List<Vector2Int> { new Vector2Int(7, 9), new Vector2Int(10, 9) }
            });
            var game = root.AddComponent<DefenseGame>(); game.map = map;
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(10.2f, 22, 5.5f); camera.transform.rotation = Quaternion.Euler(90, 0, 0);
            camera.orthographic = true; camera.orthographicSize = 8.4f; camera.backgroundColor = new Color(.075f, .09f, .13f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.15f; light.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientLight = new Color(.65f, .65f, .7f);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var builds = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!builds.Exists(s => s.path == ScenePath)) { builds.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = builds.ToArray(); }
            AssetDatabase.SaveAssets(); Selection.activeGameObject = root;
        }

        static void Check(bool condition, string message, List<string> results)
        {
            if (!condition) throw new Exception("FAILED: " + message);
            results.Add("PASS " + message); Debug.Log("PASS " + message);
        }

        [MenuItem("工具/CubeWorld/运行逻辑验收")]
        public static void RunValidation()
        {
            if (Application.isPlaying) return;
            // 验收需要重新打开场景，先保存用户正在编辑的内容。
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var results = new List<string>();
            var obj = new GameObject("Validation temporary map");
            var tile = ScriptableObject.CreateInstance<TileDefinition>();
            var wall = ScriptableObject.CreateInstance<TileDefinition>(); wall.groundPassable = false;
            try
            {
                var map = obj.AddComponent<GridMap>(); map.Resize(8, 6); foreach (var c in map.cells) c.tile = tile; map.ResetState();
                var route = new RouteDefinition { spawn = new Vector2Int(0, 1), goal = new Vector2Int(7, 1) };
                map.routes.Add(route);
                var original = new List<Vector2Int> {
                    new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1), new Vector2Int(3,1),
                    new Vector2Int(3,2), new Vector2Int(2,2), new Vector2Int(2,1), new Vector2Int(3,1),
                    new Vector2Int(4,1), new Vector2Int(5,1), new Vector2Int(6,1), new Vector2Int(7,1) };
                var search = new PathService(map, new Vector2Int(3, 1));
                var repaired = search.Repair(original, 0, route.goal);
                Check(search.RequiredConnected() && search.Valid(repaired), "blocked route repaired with legal directed edges", results);
                Check(repaired.FindAll(p => p == new Vector2Int(2,1)).Count >= 2, "loop retains separate repeated visits", results);
                Check(original.Count == 12 && original[3] == new Vector2Int(3,1), "shared preset remains unchanged", results);
                Check(repaired[0] == original[0] && repaired[1] == original[1] && repaired[2] == original[2], "longest valid old prefix retained", results);
                var afterFirstVisit = search.Repair(original, 4, route.goal);
                Check(search.Valid(afterFirstVisit) && afterFirstVisit[0] == original[4], "future repeated blockage repaired from current progress", results);

                var directed = new Vector2Int(1, 4); map.Cell(directed).exits = Directions.East;
                var directionSearch = new PathService(map);
                Check(!map.Edge(directed, directed + Vector2Int.up) && map.Edge(directed + Vector2Int.up, directed), "one-way edges are asymmetric", results);
                foreach (var p in directionSearch.To(new Vector2Int(7,4)).Path(directed)) Check(map.Inside(p), "reverse BFS path in bounds", results);
                Check(directionSearch.Valid(directionSearch.To(new Vector2Int(7,4)).Path(directed)), "reverse BFS respects original edge direction", results);
                var field = directionSearch.To(route.goal); Check(ReferenceEquals(field, directionSearch.To(route.goal)), "same destination shares one BFS field", results);

                // 人工单向口袋：个体被困不等于出怪口被封死。
                var pocket = new Vector2Int(3, 4); var block = new Vector2Int(4, 4); map.Cell(pocket).exits = Directions.East;
                var blockerObject = new GameObject("Validation obstacle"); blockerObject.transform.SetParent(obj.transform);
                var obstacle = blockerObject.AddComponent<Obstacle>(); map.State(block).obstacle = obstacle;
                var trapped = new PathService(map);
                Check(trapped.RequiredConnected() && !trapped.To(route.goal).Reaches(pocket), "individual trapped while required spawn stays connected", results);
                float hp = obstacle.health;
                Check(!obstacle.ReceiveDamage(10, DamageKind.NormalSingle, null) && !obstacle.ReceiveDamage(10, DamageKind.Area, null) &&
                    !obstacle.ReceiveDamage(10, DamageKind.BreachSingle, null) && obstacle.health == hp, "normal area and forged breach rejected by damage receiver", results);
                map.State(block).obstacle = null;
                Check(new PathService(map).To(route.goal).Reaches(pocket), "destroyed obstacle restores pocket reachability", results);
                for (int y = 0; y < map.height; y++) map.Cell(new Vector2Int(4,y)).tile = wall;
                Check(!new PathService(map).RequiredConnected(), "sealed required spawn rejected", results);
                map.State(new Vector2Int(1,1)).burnRemaining = 5;
                Check(map.State(new Vector2Int(2,1)).burnRemaining == 0, "per-cell fire does not mutate shared tile state", results);
            }
            finally { UnityEngine.Object.DestroyImmediate(obj); UnityEngine.Object.DestroyImmediate(tile); UnityEngine.Object.DestroyImmediate(wall); }

            // 真实场景重新从磁盘读取，验证序列化后的地块引用和重复路点。
            var saved = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(ScenePath);
                var map = UnityEngine.Object.FindObjectOfType<GridMap>(); map.ResetState();
                Check(map.cells.Count == map.width * map.height && map.width <= 20 && map.height <= 20, "demo row-major cell count and dimensions", results);
                Check(map.routes.Count >= 2 && map.routes[0].goal != map.routes[1].goal, "demo has distinct goals", results);
                Check(map.routes[0].nodes[0] == map.routes[0].nodes[4], "scene reload preserves repeated route node", results);
                var paths = new PathService(map);
                foreach (var route in map.routes) Check(paths.Valid(route.Expand(map, paths)), "demo route valid: " + route.name, results);
                Check(!map.Open(new Vector2Int(9,4)) && map.Cell(new Vector2Int(7,9)).tile.speedMultiplier < 1 && map.Cell(new Vector2Int(10,2)).tile.flammable, "water swamp grass configuration", results);
                var before = map.Cell(new Vector2Int(2,2)).tile;
                Undo.RecordObject(map, "Validation paint"); map.Cell(new Vector2Int(2,2)).tile = map.Cell(new Vector2Int(9,4)).tile;
                Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
                Check(map.Cell(new Vector2Int(2,2)).tile == before, "map painting supports Undo", results);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(saved); }
            Directory.CreateDirectory("Logs"); File.WriteAllLines("Logs/CubeDefense-validation.txt", results);
            Debug.Log("CUBE_DEFENSE_VALIDATION_OK: " + results.Count + " assertions");
        }
    }
}
