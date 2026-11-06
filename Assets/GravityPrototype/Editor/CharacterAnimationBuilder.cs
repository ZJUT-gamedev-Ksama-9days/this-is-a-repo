using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype.Editor
{
    public static class CharacterAnimationBuilder
    {
        public const string Folder = "Assets/GravityPrototype/Art/CharacterPSDs/Animated";
        public static bool IsAvailable => File.Exists(Folder + "/Idle/Idle_00.png");
        private static readonly string[] States = { "Idle", "Walk", "Jump", "Float", "Death" };
        private static readonly int[] Counts = { 4, 8, 4, 8, 7 };

        public static void ImportFrames()
        {
            for (int s = 0; s < States.Length; s++)
                for (int i = 0; i < Counts[s]; i++)
                {
                    string path = FramePath(States[s], i);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = PixelMetrics.PixelsPerUnit;
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.alphaIsTransparency = true;
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spriteGenerateFallbackPhysicsShape = false;
                    importer.SetTextureSettings(settings);
                    importer.SaveAndReimport();
                }
        }

        [MenuItem("Tools/Gravity Prototype/Apply PSD Character Animations")]
        public static void ApplyAll()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
            ImportFrames();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string path in new[] { PhysicsSandboxBuilder.ScenePath, PlatformLevelBuilder.LevelScenePath })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var player in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerController2D>(true))) Apply(player);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        public static void Apply(PlayerController2D player)
        {
            Transform visual = player.transform.Find("Visuals/Pixel Astronaut");
            if (visual == null) throw new InvalidOperationException("Expected existing pixel artwork.");
            var artwork = visual.GetComponent<SpriteRenderer>();
            var idle = Sequence("Idle", 4); var walk = Sequence("Walk", 8);
            var jump = Sequence("Jump", 4); var floating = Sequence("Float", 8); var death = Sequence("Death", 7);
            artwork.sprite = idle[0]; artwork.color = Color.white; artwork.enabled = true;
            visual.localScale = Vector3.one;
            visual.localPosition = PixelMetrics.PlayerArtworkOffset;
            var animator = player.GetComponent<CharacterPixelAnimator>();
            if (animator == null) animator = player.gameObject.AddComponent<CharacterPixelAnimator>();
            var so = new SerializedObject(animator);
            so.FindProperty("artwork").objectReferenceValue = artwork;
            foreach (var pair in new[] { ("standing",idle[0]), ("floatFrame",floating[0]), ("walkA",walk[0]), ("walkB",walk[4]), ("jump",jump[0]), ("death",death[0]) })
                so.FindProperty(pair.Item1).objectReferenceValue = pair.Item2;
            SetArray(so, "idleFrames", idle); SetArray(so, "walkFrames", walk); SetArray(so, "jumpFrames", jump);
            SetArray(so, "floatFrames", floating); SetArray(so, "deathFrames", death);
            so.FindProperty("frameSeconds").floatValue = 0.1f;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (player.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(player)) EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }
        private static string FramePath(string state, int i) => Folder + "/" + state + "/" + state + "_" + i.ToString("00") + ".png";
        private static Sprite[] Sequence(string state, int count) => Enumerable.Range(0, count).Select(i =>
            AssetDatabase.LoadAssetAtPath<Sprite>(FramePath(state, i)) ?? throw new InvalidOperationException("Missing frame: " + FramePath(state, i))).ToArray();
        private static void SetArray(SerializedObject so, string name, Sprite[] frames)
        {
            var p = so.FindProperty(name); p.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
        }
    }
}
