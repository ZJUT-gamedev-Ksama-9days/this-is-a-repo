using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype.Editor
{
    /// <summary>
    /// Builds the playable spike / bounce-pad level and its level-select menu through Unity APIs.
    /// Like the sandbox builder, existing scenes are preserved and never regenerated in place.
    /// </summary>
    public static class PlatformLevelBuilder
    {
        public const string LevelScenePath = "Assets/GravityPrototype/Scenes/PlatformLevel.unity";
        public const string MenuScenePath = "Assets/GravityPrototype/Scenes/LevelMenu.unity";
        public const string LevelId = "Level01";
        public const string LevelDisplayName = "Level 01 - Spike Ascent";
        public const string LevelSceneName = "PlatformLevel";

        [MenuItem("Tools/Gravity Prototype/Open or Create Platform Level")]
        public static void OpenOrCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Create();
        }

        // Unity command line: -executeMethod GravityPrototype.Editor.PlatformLevelBuilder.CreateBatch
        public static void CreateBatch()
        {
            Create();
            Debug.Log("PLATFORM_LEVEL_CREATED " + LevelScenePath + " / " + MenuScenePath);
        }

        public static void ValidateBatch()
        {
            Scene level = EditorSceneManager.OpenScene(LevelScenePath);
            int pads = CountInScene<GravityBouncePad2D>(level);
            int spikes = CountInScene<SpikeHazard2D>(level);
            int goals = CountInScene<LevelGoal2D>(level);
            int players = CountInScene<PlayerHealth>(level);
            int controllers = CountInScene<LevelController>(level);
            if (pads < 2 || spikes < 1 || goals != 1 || players != 1 || controllers != 1)
            {
                throw new InvalidOperationException(
                    $"Platform level validation failed: pads={pads} spikes={spikes} goals={goals} "
                    + $"players={players} controllers={controllers}");
            }

            Debug.Log($"PLATFORM_LEVEL_OK pads={pads} spikes={spikes} roots={level.rootCount}");
        }

        private static void Create()
        {
            PrototypeAssetFactory.EnsureFolder(PrototypeAssetFactory.ScenesPath);
            PrototypeAssetFactory.EnsureFolder(PrototypeAssetFactory.ArtPath);
            PrototypeAssetFactory.EnsureFolder(PrototypeAssetFactory.PrefabPath);

            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.productName = "Gravity Frame Prototype";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.defaultIsNativeResolution = false;
            Time.fixedDeltaTime = 0.02f;
            Physics2D.gravity = Vector2.down * PhysicsEnvironment.Gravity;

            bool levelExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelScenePath) != null;
            bool menuExists = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) != null;
            if (levelExists && menuExists)
            {
                EditorSceneManager.OpenScene(MenuScenePath);
                Debug.Log("Existing platform level and menu preserved; no assets regenerated.");
                return;
            }

            if (!levelExists)
            {
                BuildLevelScene();
            }

            if (!menuExists)
            {
                BuildMenuScene();
            }

            RegisterScene(MenuScenePath, true);
            RegisterScene(LevelScenePath, false);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(MenuScenePath);
        }

        private static void BuildLevelScene()
        {
            Sprite square = PrototypeAssetFactory.GetOrCreateSprite("Square", false);
            PhysicsMaterial2D contactMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(
                "Assets/GravityPrototype/Physics/GroundContact.physicsMaterial2D")
                ?? PrototypeAssetFactory.GetOrCreateContactMaterial();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(scene, new Vector3(0.4f, 0.2f, -10f), 6.4f);

            GameObject environment = new GameObject("Environment");
            CreatePlatform("Floor", environment.transform, new Vector2(0f, -4.7f),
                new Vector2(20f, 1f), square, contactMaterial);
            CreatePlatform("Left Wall", environment.transform, new Vector2(-10f, 0.8f),
                new Vector2(0.8f, 11f), square, contactMaterial);
            CreatePlatform("Right Wall", environment.transform, new Vector2(10f, 0.8f),
                new Vector2(0.8f, 11f), square, contactMaterial);
            CreatePlatform("Ceiling", environment.transform, new Vector2(0f, 6.4f),
                new Vector2(20f, 0.8f), square, contactMaterial);
            CreatePlatform("Mid Platform", environment.transform, new Vector2(2.4f, -1.2f),
                new Vector2(4.8f, 0.5f), square, contactMaterial);
            // The upper platform starts right of the ascent pad so a launch never hits its underside.
            CreatePlatform("Upper Platform", environment.transform, new Vector2(7.4f, 1.2f),
                new Vector2(4f, 0.5f), square, contactMaterial);

            CreateSpikeHazard(environment.transform, "Floor Spikes", new Vector2(-1.2f, -4.05f),
                new Vector2(3f, 0.3f), square, SpikeDamageMode.Linear, 30f, 0f, 0f);
            CreateSpikeHazard(environment.transform, "Delayed Spikes", new Vector2(6.2f, -4.05f),
                new Vector2(2.4f, 0.3f), square, SpikeDamageMode.DelayedBurst, 30f, 0.5f, 90f);

            CreateBouncePad(environment.transform, "Tutorial Pad", new Vector2(-6.8f, -3.5f),
                new Vector2(1.4f, 0.32f), new Vector2(0f, 1f), 11f, square, contactMaterial);
            CreateBouncePad(environment.transform, "Pit Pad", new Vector2(-4.1f, -3.5f),
                new Vector2(1.6f, 0.34f), new Vector2(0.6f, 1f), 14f, square, contactMaterial);
            CreateBouncePad(environment.transform, "Ascent Pad", new Vector2(4.2f, -0.78f),
                new Vector2(1.8f, 0.34f), new Vector2(0.3f, 1f), 13f, square, contactMaterial);

            LevelGoal2D goal = CreateGoal(environment.transform, new Vector2(8.4f, 2.35f),
                new Vector2(0.9f, 1.5f), square);

            PlayerController2D player = PrototypePlayerFactory.CreatePlayer(scene, new Vector3(-8.2f, -3.7375f, 0f));
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null)
            {
                health = player.gameObject.AddComponent<PlayerHealth>();
            }

            if (player.GetComponent<PlayerFallDamage2D>() == null) player.gameObject.AddComponent<PlayerFallDamage2D>();
            PlayerHealthHUD sandboxHud = player.GetComponent<PlayerHealthHUD>();
            if (sandboxHud != null) sandboxHud.enabled = false;
            GameObject framePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/GravityPrototype/Prefabs/GravityFrame.prefab");
            if (framePrefab == null) throw new InvalidOperationException("Generate the physics sandbox first to create its gravity frame.");
            GameObject frame = (GameObject)PrefabUtility.InstantiatePrefab(framePrefab, scene);
            frame.name = "Gravity Frame";
            frame.transform.position = new Vector3(-4.1f, -1.6f, 0f);
            AstronautToolsBuilder.ConfigureScene(scene);

            GameObject levelObject = new GameObject("Level");
            LevelTimer timer = levelObject.AddComponent<LevelTimer>();
            // The level controller starts the clock itself, so the timer must not also auto-start.
            SerializedObject timerProperties = new SerializedObject(timer);
            timerProperties.FindProperty("beginAutomatically").boolValue = false;
            timerProperties.ApplyModifiedPropertiesWithoutUndo();
            LevelController controller = levelObject.AddComponent<LevelController>();
            SerializedObject controllerProperties = new SerializedObject(controller);
            controllerProperties.FindProperty("levelId").stringValue = LevelId;
            controllerProperties.FindProperty("player").objectReferenceValue = health;
            controllerProperties.FindProperty("playerController").objectReferenceValue = player;
            controllerProperties.FindProperty("timer").objectReferenceValue = timer;
            controllerProperties.FindProperty("goal").objectReferenceValue = goal;
            controllerProperties.FindProperty("deathRestartDelay").floatValue = 0.7f;
            controllerProperties.ApplyModifiedPropertiesWithoutUndo();

            GameObject hudObject = new GameObject("Level HUD");
            LevelHudUI hud = hudObject.AddComponent<LevelHudUI>();
            SerializedObject hudProperties = new SerializedObject(hud);
            hudProperties.FindProperty("level").objectReferenceValue = controller;
            hudProperties.FindProperty("health").objectReferenceValue = health;
            hudProperties.ApplyModifiedPropertiesWithoutUndo();

            if (SuppliedArtBuilder.IsAvailable) SuppliedArtBuilder.ApplyScene(scene);
            EditorSceneManager.SaveScene(scene, LevelScenePath);
            Debug.Log($"Platform level saved: {LevelScenePath}, rootObjects={scene.rootCount}, "
                + $"gravity={Physics2D.gravity}, fixedStep={Time.fixedDeltaTime}");
        }

        private static void BuildMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera(scene, new Vector3(0f, 0f, -10f), 5f);

            GameObject menuObject = new GameObject("Level Menu");
            LevelMenuUI menu = menuObject.AddComponent<LevelMenuUI>();
            SerializedObject menuProperties = new SerializedObject(menu);
            SerializedProperty levels = menuProperties.FindProperty("levels");
            levels.arraySize = 1;
            SerializedProperty entry = levels.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("levelId").stringValue = LevelId;
            entry.FindPropertyRelative("displayName").stringValue = LevelDisplayName;
            entry.FindPropertyRelative("sceneName").stringValue = LevelSceneName;
            menuProperties.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            Debug.Log("Level select saved: " + MenuScenePath);
        }

        private static Camera CreateCamera(Scene scene, Vector3 position, float orthographicSize)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = position;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.09f);
            cameraObject.AddComponent<AudioListener>();
            return camera;
        }

        private static GameObject CreatePlatform(string name, Transform parent, Vector2 position,
            Vector2 size, Sprite sprite, PhysicsMaterial2D contactMaterial)
        {
            GameObject platform = new GameObject(name);
            platform.transform.SetParent(parent, false);
            platform.transform.position = position;
            platform.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.19f, 0.26f, 0.35f);
            BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.sharedMaterial = contactMaterial;
            return platform;
        }

        private static SpikeHazard2D CreateSpikeHazard(Transform parent, string name, Vector2 position,
            Vector2 size, Sprite sprite, SpikeDamageMode mode, float damagePerSecond, float delaySeconds,
            float burstDamagePerSecond)
        {
            GameObject hazardObject = new GameObject(name);
            hazardObject.transform.SetParent(parent, false);
            hazardObject.transform.position = position;
            BoxCollider2D collider = hazardObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.isTrigger = true;

            GameObject baseBar = new GameObject("Base");
            baseBar.transform.SetParent(hazardObject.transform, false);
            baseBar.transform.localScale = new Vector3(size.x, size.y * 0.6f, 1f);
            SpriteRenderer baseRenderer = baseBar.AddComponent<SpriteRenderer>();
            baseRenderer.sprite = sprite;
            baseRenderer.color = new Color(0.55f, 0.16f, 0.2f);
            baseRenderer.sortingOrder = 1;

            int toothCount = Mathf.Max(3, Mathf.RoundToInt(size.x / 0.35f));
            for (int i = 0; i < toothCount; i++)
            {
                float offset = ((i + 0.5f) / toothCount - 0.5f) * size.x;
                GameObject tooth = new GameObject("Tooth");
                tooth.transform.SetParent(hazardObject.transform, false);
                tooth.transform.localPosition = new Vector3(offset, size.y * 0.35f, 0f);
                tooth.transform.localScale = Vector3.one * Mathf.Min(size.y * 1.1f, 0.34f);
                tooth.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                SpriteRenderer toothRenderer = tooth.AddComponent<SpriteRenderer>();
                toothRenderer.sprite = sprite;
                toothRenderer.color = new Color(0.95f, 0.42f, 0.4f);
                toothRenderer.sortingOrder = 2;
            }

            SpikeHazard2D hazard = hazardObject.AddComponent<SpikeHazard2D>();
            SerializedObject properties = new SerializedObject(hazard);
            properties.FindProperty("damageProfile.mode").enumValueIndex = (int)mode;
            properties.FindProperty("damageProfile.damagePerSecond").floatValue = damagePerSecond;
            properties.FindProperty("damageProfile.delaySeconds").floatValue = delaySeconds;
            properties.FindProperty("damageProfile.burstDamagePerSecond").floatValue = burstDamagePerSecond;
            properties.ApplyModifiedPropertiesWithoutUndo();
            return hazard;
        }

        private static GravityBouncePad2D CreateBouncePad(Transform parent, string name, Vector2 position,
            Vector2 size, Vector2 launchDirection, float launchSpeed, Sprite sprite,
            PhysicsMaterial2D contactMaterial)
        {
            GameObject padObject = new GameObject(name);
            padObject.transform.SetParent(parent, false);
            padObject.transform.position = position;
            padObject.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = padObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.35f, 0.95f, 0.7f);
            renderer.sortingOrder = 2;
            BoxCollider2D collider = padObject.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.sharedMaterial = contactMaterial;

            // A floating dynamic body: the player can shove it into a better route, and it wakes up
            // when pushed because Unity keeps the contact response.
            Rigidbody2D body = padObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.mass = 1.2f;
            body.gravityScale = 0f;
            body.linearDamping = 1.2f;
            body.angularDamping = 2f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            GravityBouncePad2D pad = padObject.AddComponent<GravityBouncePad2D>();
            SerializedObject padProperties = new SerializedObject(pad);
            padProperties.FindProperty("launchDirection").vector2Value = launchDirection;
            padProperties.FindProperty("launchSpeed").floatValue = launchSpeed;
            padProperties.FindProperty("horizontalInherit").floatValue = 1f;
            padProperties.ApplyModifiedPropertiesWithoutUndo();
            padObject.AddComponent<BouncePadMouseDrag2D>();
            return pad;
        }

        private static LevelGoal2D CreateGoal(Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            GameObject goalObject = new GameObject("Level Goal");
            goalObject.transform.SetParent(parent, false);
            goalObject.transform.position = position;
            BoxCollider2D collider = goalObject.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.isTrigger = true;

            GameObject visual = new GameObject("Goal Visual");
            visual.transform.SetParent(goalObject.transform, false);
            GameObject pole = new GameObject("Pole");
            pole.transform.SetParent(visual.transform, false);
            pole.transform.localPosition = new Vector3(-0.25f, 0f, 0f);
            pole.transform.localScale = new Vector3(0.08f, 1.5f, 1f);
            SpriteRenderer poleRenderer = pole.AddComponent<SpriteRenderer>();
            poleRenderer.sprite = sprite;
            poleRenderer.color = new Color(0.85f, 0.95f, 1f);
            poleRenderer.sortingOrder = 3;

            GameObject flag = new GameObject("Flag");
            flag.transform.SetParent(visual.transform, false);
            flag.transform.localPosition = new Vector3(0.16f, 0.5f, 0f);
            flag.transform.localScale = new Vector3(0.72f, 0.42f, 1f);
            SpriteRenderer flagRenderer = flag.AddComponent<SpriteRenderer>();
            flagRenderer.sprite = sprite;
            flagRenderer.color = new Color(0.4f, 1f, 0.6f);
            flagRenderer.sortingOrder = 3;

            LevelGoal2D goal = goalObject.AddComponent<LevelGoal2D>();
            SerializedObject properties = new SerializedObject(goal);
            properties.FindProperty("visualOnReached").objectReferenceValue = visual;
            properties.ApplyModifiedPropertiesWithoutUndo();
            return goal;
        }

        private static int CountInScene<T>(Scene scene) where T : Component
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<T>(true).Length;
            }

            return count;
        }

        private static void RegisterScene(string path, bool first)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(item => item.path == path);
            EditorBuildSettingsScene entry = new EditorBuildSettingsScene(path, true);
            if (first)
            {
                scenes.Insert(0, entry);
            }
            else
            {
                scenes.Add(entry);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
