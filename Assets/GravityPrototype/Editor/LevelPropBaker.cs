using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace GravityPrototype.Editor
{
    /// <summary>
    /// Tile Palette 工作流：关卡里用调色板画的道具 tile（U 型管、小球）先只作为“摆位标记”，
    /// 执行本命令后烘焙成 Mechanisms 下的真实实体，并删掉那些 tile。
    /// U 型管是 4x3、26 个碰撞体的空心管（碰撞体由立绘不透明像素生成）；
    /// 小球复用场景里已有小球的组件配置（刚体/碰撞体/材质/伤害），保证物理手感一致。
    /// </summary>
    public static class LevelPropBaker
    {
        public const string PrefabFolder = "Assets/GravityPrototype/Prefabs";
        public const string TileFolder = "Assets/Tile";
        public const string TubePrefabPath = PrefabFolder + "/U型管.prefab";
        public const string TubeTilePath = TileFolder + "/u型管_0.asset";
        public const string BallTilePath = TileFolder + "/小球_0.asset";
        public const string TubeName = "U型管";
        public const string BallName = "Physics Ball";

        [MenuItem("Tools/Gravity Prototype/道具：把关卡里的 tile 烘成 Mechanisms 实体", false, 20)]
        public static void Bake()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");

            var tubeTile = Require<TileBase>(TubeTilePath);
            var ballTile = Require<TileBase>(BallTilePath);
            var tubePrefab = Require<GameObject>(TubePrefabPath);

            var scene = SceneManager.GetActiveScene();
            var parent = FindMechanisms(scene);
            if (parent == null) throw new InvalidOperationException("当前场景找不到 PR7Grid/Mechanisms。");

            Rigidbody2D ballTemplate = UnityEngine.Object.FindObjectsByType<BallMaterial2D>(FindObjectsSortMode.None)
                .Where(b => b.gameObject.scene == scene)
                .Select(b => b.GetComponent<Rigidbody2D>())
                .FirstOrDefault(b => b != null);

            var createdTubes = new List<GameObject>();
            var createdBalls = new List<GameObject>();
            foreach (var map in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Tilemap>(true)))
            {
                var cells = new List<Vector3Int>();
                foreach (var cell in map.cellBounds.allPositionsWithin)
                {
                    var tile = map.GetTile(cell);
                    if (tile != null && (tile == tubeTile || tile == ballTile)) cells.Add(cell);
                }

                if (cells.Count == 0) continue;
                Undo.RecordObject(map, "Bake level props");
                foreach (var cell in cells)
                {
                    var tile = map.GetTile(cell);
                    var world = map.GetCellCenterWorld(cell);
                    if (tile == tubeTile)
                    {
                        var tube = (GameObject)PrefabUtility.InstantiatePrefab(tubePrefab, parent);
                        Undo.RegisterCreatedObjectUndo(tube, "Bake level props");
                        tube.transform.position = world;
                        tube.transform.localRotation = Quaternion.identity;
                        tube.transform.localScale = Vector3.one;
                        createdTubes.Add(tube);
                    }
                    else if (tile == ballTile)
                    {
                        if (ballTemplate == null)
                            throw new InvalidOperationException("场景里没有可用作模板的小球，无法从 tile 生成小球。");
                        var clone = UnityEngine.Object.Instantiate(ballTemplate.gameObject, parent);
                        Undo.RegisterCreatedObjectUndo(clone, "Bake level props");
                        clone.name = NextBallName(parent);
                        clone.transform.position = world;
                        clone.transform.localRotation = Quaternion.identity;
                        clone.transform.localScale = Vector3.one;
                        var body = clone.GetComponent<Rigidbody2D>();
                        body.linearVelocity = Vector2.zero;
                        body.angularVelocity = 0f;
                        createdBalls.Add(clone);
                    }

                    map.SetTile(cell, null);
                }

                EditorUtility.SetDirty(map);
            }

            if (createdTubes.Count == 0 && createdBalls.Count == 0)
            {
                Debug.Log("没有需要烘焙的道具 tile（" + TubeTilePath + " / " + BallTilePath + "）。");
                return;
            }

            var all = createdTubes.Concat(createdBalls).ToArray();
            Selection.objects = all;
            EditorGUIUtility.PingObject(all[0]);
            if (SceneView.lastActiveSceneView != null) SceneView.FrameLastActiveSceneView();
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"已烘焙：U 型管 {createdTubes.Count} 个、小球 {createdBalls.Count} 个（父物体 {parent.name}）。用移动工具微调后 Ctrl+S 保存场景。");
        }

        [MenuItem("Tools/Gravity Prototype/道具：移除当前场景的 U 型管实体", false, 21)]
        public static void RemoveTubes()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            var scene = SceneManager.GetActiveScene();
            var tubes = FindAll(scene, TubeName);
            if (tubes.Count == 0)
            {
                Debug.Log("当前场景没有 U 型管实体。");
                return;
            }

            foreach (var tube in tubes) Undo.DestroyObjectImmediate(tube.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("已移除 " + tubes.Count + " 个 U 型管实体，记得 Ctrl+S 保存场景。");
        }

        [MenuItem("Tools/Gravity Prototype/道具：把关卡里的 tile 烘成 Mechanisms 实体", true)]
        [MenuItem("Tools/Gravity Prototype/道具：移除当前场景的 U 型管实体", true)]
        private static bool ValidateEditMode() => !Application.isPlaying;

        public static List<Transform> FindAll(Scene scene, string objectName)
        {
            if (!scene.IsValid()) return new List<Transform>();
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == objectName)
                .ToList();
        }

        private static Transform FindMechanisms(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "PR7Grid") continue;
                var mechanisms = root.transform.Find("Mechanisms");
                return mechanisms != null ? mechanisms : root.transform;
            }

            return null;
        }

        private static string NextBallName(Transform parent)
        {
            var used = new HashSet<string>(parent.GetComponentsInChildren<BallMaterial2D>(true).Select(b => b.name));
            if (!used.Contains(BallName)) return BallName;
            for (int i = 2; i < 100; i++)
            {
                var candidate = BallName + " " + i;
                if (!used.Contains(candidate)) return candidate;
            }

            return BallName + " " + Guid.NewGuid().ToString("N").Substring(0, 4);
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing asset: " + path);
            return asset;
        }
    }
}
