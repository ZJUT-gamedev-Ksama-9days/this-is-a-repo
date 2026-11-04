using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype.Editor
{
    /// <summary>
    /// Creates the placeholder protagonist through Unity APIs and reuses the saved Player prefab when
    /// the sandbox builder already produced one, so both scenes share the same character.
    /// </summary>
    public static class PrototypePlayerFactory
    {
        public const string PlayerPrefabPath = PrototypeAssetFactory.PrefabPath + "/Player.prefab";

        public static PlayerController2D CreatePlayer(Scene scene, Vector3 position)
        {
            PrototypeAssetFactory.EnsureFolder(PrototypeAssetFactory.PrefabPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.position = position;
                PlayerController2D existing = instance.GetComponent<PlayerController2D>();
                if (existing == null)
                {
                    throw new InvalidOperationException("Existing Player prefab has no PlayerController2D.");
                }

                return existing;
            }

            Sprite square = PrototypeAssetFactory.GetOrCreateSprite("Square", false);
            PhysicsMaterial2D material = GetOrCreatePlayerMaterial();
            GameObject character = new GameObject("Player");
            SceneManager.MoveGameObjectToScene(character, scene);
            character.tag = "Player";
            character.transform.position = position;
            Rigidbody2D body = character.AddComponent<Rigidbody2D>();
            body.mass = 2f;
            body.gravityScale = 2.4f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            CapsuleCollider2D capsule = character.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.78f, 1.45f);
            capsule.sharedMaterial = material;
            PlayerController2D player = character.AddComponent<PlayerController2D>();
            GameObject visuals = new GameObject("Visuals");
            visuals.transform.SetParent(character.transform, false);
            CreatePlayerPart("Suit", visuals.transform, square, new Vector2(0f, -0.16f),
                new Vector2(0.6f, 0.72f), new Color(0.2f, 0.85f, 0.68f));
            CreatePlayerPart("Helmet", visuals.transform, square, new Vector2(0f, 0.37f),
                new Vector2(0.72f, 0.58f), new Color(0.88f, 0.95f, 1f));
            CreatePlayerPart("Visor", visuals.transform, square, new Vector2(0.04f, 0.38f),
                new Vector2(0.49f, 0.24f), new Color(0.06f, 0.13f, 0.23f), 4);
            CreatePlayerPart("Visor Glint", visuals.transform, square, new Vector2(0.17f, 0.42f),
                new Vector2(0.09f, 0.07f), new Color(0.25f, 0.85f, 1f), 5);
            CreatePlayerPart("Left Boot", visuals.transform, square, new Vector2(-0.19f, -0.6f),
                new Vector2(0.26f, 0.2f), new Color(0.12f, 0.38f, 0.42f));
            CreatePlayerPart("Right Boot", visuals.transform, square, new Vector2(0.19f, -0.6f),
                new Vector2(0.26f, 0.2f), new Color(0.12f, 0.38f, 0.42f));
            SerializedObject properties = new SerializedObject(player);
            properties.FindProperty("visuals").objectReferenceValue = visuals.transform;
            properties.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAssetAndConnect(character, PlayerPrefabPath, InteractionMode.AutomatedAction);
            return player;
        }

        private static PhysicsMaterial2D GetOrCreatePlayerMaterial()
        {
            const string path = PrototypeAssetFactory.ArtPath + "/PlayerContact.physicsMaterial2D";
            PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material != null)
            {
                return material;
            }

            PrototypeAssetFactory.EnsureFolder(PrototypeAssetFactory.ArtPath);
            material = new PhysicsMaterial2D("Player Contact") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CreatePlayerPart(string name, Transform parent, Sprite sprite,
            Vector2 position, Vector2 size, Color color, int sortingOrder = 3)
        {
            GameObject part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }
    }
}
