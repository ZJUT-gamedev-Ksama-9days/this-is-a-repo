using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype.Editor
{
    /// <summary>Authors new material assets/prefabs without overwriting existing designer tuning.</summary>
    public static class BallMaterialLibraryBuilder
    {
        public const string PhysicsPath = "Assets/GravityPrototype/Physics";
        private const string PrefabPath = "Assets/GravityPrototype/Prefabs";

        [MenuItem("Tools/Gravity Prototype/Add Three Ball Materials to Sandbox")]
        public static void AddToActiveSandbox()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != PhysicsSandboxBuilder.ScenePath)
                throw new InvalidOperationException("Open the physics sandbox in Edit Mode first.");
            ConfigureScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
        }

        public static void ConfigureScene(Scene scene)
        {
            GameObject ball = scene.GetRootGameObjects().Single(item => item.name == "Ball");
            PhysicsSandboxController controls = scene.GetRootGameObjects()
                .Single(item => item.name == "Sandbox Controls").GetComponent<PhysicsSandboxController>();
            BallPhysicsProfile[] profiles = EnsureLibrary();
            BallMaterial2D material = ball.GetComponent<BallMaterial2D>();
            if (material == null) material = Undo.AddComponent<BallMaterial2D>(ball);
            if (material.Profile == null)
            {
                Undo.RecordObjects(new UnityEngine.Object[] { material, ball.GetComponent<Rigidbody2D>(),
                    ball.GetComponent<CircleCollider2D>(), ball.GetComponent<SpriteRenderer>() }, "Set initial wood ball profile");
                material.ApplyProfile(profiles[1]);
                PrefabUtility.RecordPrefabInstancePropertyModifications(material);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ball.GetComponent<Rigidbody2D>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(ball.GetComponent<CircleCollider2D>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(ball.GetComponent<SpriteRenderer>());
            }
            if (ball.GetComponent<BallImpactDamage2D>() == null) Undo.AddComponent<BallImpactDamage2D>(ball);

            SerializedObject properties = new SerializedObject(controls);
            SerializedProperty array = properties.FindProperty("ballProfiles");
            array.arraySize = profiles.Length;
            for (int i = 0; i < profiles.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = profiles[i];
            properties.ApplyModifiedProperties();
        }

        public static BallPhysicsProfile[] EnsureLibrary()
        {
            EnsureFolder(PhysicsPath);
            EnsureFolder(PrefabPath);
            BallPhysicsProfile[] profiles =
            {
                GetOrCreateProfile("Balloon", BallMaterialKind.Balloon, 1.225f, 0.015f,
                    0.8f, 0.5f, new Color(1f, 0.35f, 0.55f), 0.1f),
                GetOrCreateProfile("Wood", BallMaterialKind.Wood, 600f, 0f,
                    0.35f, 0.12f, new Color(1f, 0.72f, 0.25f), 1f),
                GetOrCreateProfile("Iron", BallMaterialKind.Iron, 7850f, 0f,
                    0.15f, 0.03f, new Color(0.55f, 0.65f, 0.76f), 2f)
            };
            foreach (BallPhysicsProfile profile in profiles) EnsurePrefab(profile);
            AssetDatabase.SaveAssets();
            return profiles;
        }

        private static BallPhysicsProfile GetOrCreateProfile(string name, BallMaterialKind kind, float density,
            float envelopeMass, float friction, float bounce, Color color, float impact)
        {
            string path = PhysicsPath + "/" + name + ".asset";
            BallPhysicsProfile existing = AssetDatabase.LoadAssetAtPath<BallPhysicsProfile>(path);
            if (existing != null) return existing;
            if (File.Exists(path)) throw new InvalidOperationException("Unexpected existing asset: " + path);

            string materialPath = PhysicsPath + "/" + name + "Contact.physicsMaterial2D";
            PhysicsMaterial2D contact = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
            if (contact == null)
            {
                if (File.Exists(materialPath)) throw new InvalidOperationException("Unexpected existing asset: " + materialPath);
                contact = new PhysicsMaterial2D(name + " Contact") { friction = friction, bounciness = bounce };
                AssetDatabase.CreateAsset(contact, materialPath);
            }
            BallPhysicsProfile profile = ScriptableObject.CreateInstance<BallPhysicsProfile>();
            profile.name = name;
            SerializedObject properties = new SerializedObject(profile);
            properties.FindProperty("kind").enumValueIndex = (int)kind;
            properties.FindProperty("useReferencePhysics").boolValue = true;
            properties.FindProperty("diameterMeters").floatValue = 0.84f;
            properties.FindProperty("densityKgPerCubicMeter").floatValue = density;
            properties.FindProperty("envelopeMassKg").floatValue = envelopeMass;
            properties.FindProperty("shellThicknessMeters").floatValue = kind == BallMaterialKind.Iron ? 0.0003f : 0.002f;
            properties.FindProperty("internalGasDensityKgPerCubicMeter").floatValue = PhysicsEnvironment.AirDensity;
            properties.FindProperty("airDensityKgPerCubicMeter").floatValue = PhysicsEnvironment.AirDensity;
            properties.FindProperty("dragCoefficient").floatValue = PhysicsEnvironment.SphereDragCoefficient;
            properties.FindProperty("gravityScale").floatValue = 1f;
            properties.FindProperty("linearDamping").floatValue = 0f;
            properties.FindProperty("angularDamping").floatValue = 0f;
            properties.FindProperty("contactMaterial").objectReferenceValue = contact;
            properties.FindProperty("color").colorValue = color;
            properties.FindProperty("impactDamageMultiplier").floatValue = impact;
            properties.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static void EnsurePrefab(BallPhysicsProfile profile)
        {
            string path = PrefabPath + "/" + profile.Kind + "Ball.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            if (File.Exists(path)) throw new InvalidOperationException("Unexpected existing asset: " + path);
            Sprite circle = AssetDatabase.LoadAllAssetsAtPath("Assets/GravityPrototype/Art/Circle.asset").OfType<Sprite>().Single();
            Sprite square = AssetDatabase.LoadAllAssetsAtPath("Assets/GravityPrototype/Art/Square.asset").OfType<Sprite>().Single();
            GameObject ball = new GameObject(profile.Kind + " Ball");
            try
            {
                ball.transform.localScale = Vector3.one * 0.84f;
                SpriteRenderer renderer = ball.AddComponent<SpriteRenderer>();
                renderer.sprite = circle;
                renderer.sortingOrder = 2;
                CircleCollider2D collider = ball.AddComponent<CircleCollider2D>();
                collider.radius = 0.5f;
                Rigidbody2D body = ball.AddComponent<Rigidbody2D>();
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                ball.AddComponent<GravityBody2D>();
                BallMaterial2D material = ball.AddComponent<BallMaterial2D>();
                material.ApplyProfile(profile);
                ball.AddComponent<BallImpactDamage2D>();
                AddDetail(ball.transform, circle, "Highlight", new Vector2(-0.16f, 0.18f),
                    new Vector2(0.14f, 0.2f), new Color(1f, 1f, 1f, 0.7f));
                if (profile.Kind == BallMaterialKind.Balloon)
                    AddDetail(ball.transform, square, "Knot", new Vector2(0f, -0.49f),
                        new Vector2(0.13f, 0.1f), profile.Color);
                if (profile.Kind == BallMaterialKind.Wood)
                {
                    AddDetail(ball.transform, square, "Wood Grain", new Vector2(0f, 0.02f),
                        new Vector2(0.57f, 0.035f), new Color(0.6f, 0.32f, 0.1f));
                    AddDetail(ball.transform, square, "Wood Grain 2", new Vector2(0.03f, -0.16f),
                        new Vector2(0.46f, 0.03f), new Color(0.6f, 0.32f, 0.1f));
                }
                if (profile.Kind == BallMaterialKind.Iron)
                    AddDetail(ball.transform, circle, "Rivet", new Vector2(0.2f, -0.18f),
                        Vector2.one * 0.12f, new Color(0.25f, 0.3f, 0.38f));
                PrefabUtility.SaveAsPrefabAsset(ball, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ball);
            }
        }

        private static void AddDetail(Transform parent, Sprite sprite, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject detail = new GameObject(name);
            detail.transform.SetParent(parent, false);
            detail.transform.localPosition = position;
            detail.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = detail.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 3;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
