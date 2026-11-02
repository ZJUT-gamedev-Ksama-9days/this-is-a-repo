using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype.Editor
{
    /// <summary>Creates the sandbox using Unity APIs, never handwritten asset YAML.</summary>
    public static class PhysicsSandboxBuilder
    {
        public const string ScenePath = "Assets/GravityPrototype/Scenes/PhysicsSandbox.unity";
        private const string ArtPath = "Assets/GravityPrototype/Art";
        private const string PrefabPath = "Assets/GravityPrototype/Prefabs";

        [MenuItem("Tools/Gravity Prototype/Open or Create Physics Sandbox")]
        public static void OpenOrCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Create();
        }

        // Unity command line: -executeMethod GravityPrototype.Editor.PhysicsSandboxBuilder.CreateBatch
        public static void CreateBatch()
        {
            Create();
            Debug.Log("GRAVITY_SANDBOX_CREATED " + ScenePath);
        }

        public static void ValidateAndPreviewBatch()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            GravityRegion2D region = null;
            Rigidbody2D ball = null;
            Camera camera = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Gravity Frame") region = root.GetComponent<GravityRegion2D>();
                if (root.name == "Ball") ball = root.GetComponent<Rigidbody2D>();
                if (root.name == "Main Camera") camera = root.GetComponent<Camera>();
            }

            if (region == null || ball == null || camera == null)
            {
                throw new InvalidOperationException("Sandbox is missing its frame, ball or camera.");
            }

            Debug.Log("SANDBOX_REGION " + EditorJsonUtility.ToJson(region));
            Debug.Log($"SANDBOX_BALL mass={ball.mass} gravityScale={ball.gravityScale} "
                + $"collision={ball.collisionDetectionMode} material={ball.GetComponent<CircleCollider2D>().sharedMaterial.name}");
            Debug.Log($"SANDBOX_SCENE roots={scene.rootCount} engine={Application.unityVersion}");

            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture target = RenderTexture.GetTemporary(1280, 800, 24);
            Texture2D image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Library/Locus/tmp");
                File.WriteAllBytes("Library/Locus/tmp/physics-sandbox-preview.png", image.EncodeToPNG());
                Debug.Log("SANDBOX_PREVIEW_SAVED Library/Locus/tmp/physics-sandbox-preview.png");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("Existing sandbox preserved; no assets regenerated.");
                return;
            }

            Debug.Log($"Before sandbox setup: mode={EditorSettings.defaultBehaviorMode}, "
                + $"gravity={Physics2D.gravity}, fixedStep={Time.fixedDeltaTime}, "
                + $"buildScenes={EditorBuildSettings.scenes.Length}");
            EnsureFolder("Assets/GravityPrototype/Scenes");
            EnsureFolder(ArtPath);
            EnsureFolder(PrefabPath);

            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.productName = "Gravity Frame Prototype";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.defaultIsNativeResolution = false;
            Time.fixedDeltaTime = 0.02f;

            Sprite square = GetOrCreateSprite("Square", false);
            Sprite circle = GetOrCreateSprite("Circle", true);
            Material lineMaterial = GetOrCreateLineMaterial();
            PhysicsMaterial2D contactMaterial = GetOrCreateContactMaterial();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.09f);
            cameraObject.AddComponent<AudioListener>();

            GameObject environment = new GameObject("Environment");
            CreatePlatform("Floor", environment.transform, new Vector2(0f, -4.6f),
                new Vector2(18.8f, 1f), square, contactMaterial);
            CreatePlatform("Ceiling", environment.transform, new Vector2(0f, 4.6f),
                new Vector2(18.8f, 1f), square, contactMaterial);
            CreatePlatform("Left Wall", environment.transform, new Vector2(-9.4f, 0f),
                new Vector2(0.6f, 8.2f), square, contactMaterial);
            CreatePlatform("Right Wall", environment.transform, new Vector2(9.4f, 0f),
                new Vector2(0.6f, 8.2f), square, contactMaterial);
            GameObject ramp = CreatePlatform("Test Ramp", environment.transform,
                new Vector2(4.2f, -3.7f), new Vector2(3.5f, 0.4f), square, contactMaterial);
            ramp.transform.rotation = Quaternion.Euler(0f, 0f, 14f);

            GameObject ballObject = new GameObject("Ball");
            ballObject.transform.position = new Vector3(-3.8f, -3f, 0f);
            ballObject.transform.localScale = Vector3.one * 0.84f;
            SpriteRenderer ballRenderer = ballObject.AddComponent<SpriteRenderer>();
            ballRenderer.sprite = circle;
            ballRenderer.color = new Color(1f, 0.72f, 0.25f);
            ballRenderer.sortingOrder = 2;
            CircleCollider2D ballCollider = ballObject.AddComponent<CircleCollider2D>();
            ballCollider.radius = 0.5f;
            ballCollider.sharedMaterial = contactMaterial;
            Rigidbody2D ball = ballObject.AddComponent<Rigidbody2D>();
            ball.mass = 1f;
            ball.gravityScale = 1f;
            ball.linearDamping = 0f;
            ball.angularDamping = 0.05f;
            ball.interpolation = RigidbodyInterpolation2D.Interpolate;
            ball.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            ballObject.AddComponent<GravityBody2D>();
            GameObject marker = new GameObject("Rotation Marker");
            marker.transform.SetParent(ballObject.transform, false);
            marker.transform.localPosition = new Vector3(0.16f, 0.16f, 0f);
            marker.transform.localScale = Vector3.one * 0.18f;
            SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
            markerRenderer.sprite = circle;
            markerRenderer.color = new Color(0.3f, 0.18f, 0.08f);
            markerRenderer.sortingOrder = 3;
            PrefabUtility.SaveAsPrefabAssetAndConnect(ballObject, PrefabPath + "/Ball.prefab", InteractionMode.AutomatedAction);

            GameObject frameObject = new GameObject("Gravity Frame");
            frameObject.transform.position = new Vector3(1f, -1.6f, 0f);
            frameObject.AddComponent<GravityRegion2D>();
            GravityFrameDrag2D drag = frameObject.AddComponent<GravityFrameDrag2D>();
            GameObject fillObject = new GameObject("Fill");
            fillObject.transform.SetParent(frameObject.transform, false);
            fillObject.transform.localScale = new Vector3(4f, 4f, 1f);
            SpriteRenderer fill = fillObject.AddComponent<SpriteRenderer>();
            fill.sprite = square;
            fill.color = new Color(0.1f, 0.75f, 0.95f, 0.1f);
            fill.sortingOrder = -1;
            LineRenderer outline = CreateLine("Outline", frameObject.transform, lineMaterial, 4, true, 0.05f);
            outline.SetPosition(0, new Vector3(-2f, -2f, 0f));
            outline.SetPosition(1, new Vector3(-2f, 2f, 0f));
            outline.SetPosition(2, new Vector3(2f, 2f, 0f));
            outline.SetPosition(3, new Vector3(2f, -2f, 0f));
            LineRenderer arrow = CreateLine("Gravity Direction", frameObject.transform, lineMaterial, 5, false, 0.07f);
            arrow.SetPositions(new[]
            {
                new Vector3(0f, -0.56f, 0f), new Vector3(0f, 0.56f, 0f),
                new Vector3(-0.28f, 0.168f, 0f), new Vector3(0f, 0.56f, 0f),
                new Vector3(0.28f, 0.168f, 0f)
            });
            GravityFrameView view = frameObject.AddComponent<GravityFrameView>();
            SerializedObject viewProperties = new SerializedObject(view);
            viewProperties.FindProperty("outline").objectReferenceValue = outline;
            viewProperties.FindProperty("arrow").objectReferenceValue = arrow;
            viewProperties.FindProperty("fill").objectReferenceValue = fill;
            viewProperties.ApplyModifiedPropertiesWithoutUndo();
            // The prefab deliberately resolves Camera.main at runtime, so it stays scene-independent.
            PrefabUtility.SaveAsPrefabAssetAndConnect(frameObject, PrefabPath + "/GravityFrame.prefab", InteractionMode.AutomatedAction);

            GameObject controlsObject = new GameObject("Sandbox Controls");
            PhysicsSandboxController controller = controlsObject.AddComponent<PhysicsSandboxController>();
            SerializedObject controllerProperties = new SerializedObject(controller);
            controllerProperties.FindProperty("ball").objectReferenceValue = ball;
            controllerProperties.FindProperty("frame").objectReferenceValue = drag;
            controllerProperties.ApplyModifiedPropertiesWithoutUndo();

            EnsurePlayerInScene(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var buildScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!buildScenes.Exists(item => item.path == ScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Sandbox saved: rootObjects={scene.rootCount}, "
                + $"ballMass={ball.mass}, ballGravityScale={ball.gravityScale}, "
                + $"gravity={Physics2D.gravity}, fixedStep={Time.fixedDeltaTime}");
        }

        [MenuItem("Tools/Gravity Prototype/Add Player to Current Sandbox")]
        public static void AddPlayerToActiveSandbox()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                throw new InvalidOperationException("Open the physics sandbox before adding its player.");
            }

            EnsurePlayerInScene(scene);
        }

        private static PlayerController2D EnsurePlayerInScene(Scene scene)
        {
            PlayerController2D[] existing = Array.FindAll(
                UnityEngine.Object.FindObjectsByType<PlayerController2D>(FindObjectsInactive.Include, FindObjectsSortMode.None),
                item => item.gameObject.scene == scene);
            if (existing.Length > 1)
            {
                throw new InvalidOperationException("More than one player in sandbox; refusing an ambiguous update.");
            }

            PlayerController2D player;
            if (existing.Length == 1)
            {
                player = existing[0];
            }
            else
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Player")
                    {
                        throw new InvalidOperationException("A Player object already exists without PlayerController2D.");
                    }
                }

                EnsureFolder(PrefabPath);
                EnsureFolder(ArtPath);
                const string playerPath = PrefabPath + "/Player.prefab";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
                if (prefab != null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    player = instance.GetComponent<PlayerController2D>();
                    if (player == null)
                    {
                        throw new InvalidOperationException("Existing Player prefab has no PlayerController2D.");
                    }
                }
                else
                {
                    Sprite square = GetOrCreateSprite("Square", false);
                    const string materialPath = ArtPath + "/PlayerContact.physicsMaterial2D";
                    PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(materialPath);
                    if (material == null)
                    {
                        material = new PhysicsMaterial2D("Player Contact") { friction = 0f, bounciness = 0f };
                        AssetDatabase.CreateAsset(material, materialPath);
                    }

                    GameObject character = new GameObject("Player");
                    SceneManager.MoveGameObjectToScene(character, scene);
                    character.tag = "Player";
                    character.transform.position = new Vector3(-6.2f, -3.32f, 0f);
                    Rigidbody2D body = character.AddComponent<Rigidbody2D>();
                    body.mass = 2f;
                    body.gravityScale = 2.4f;
                    body.constraints = RigidbodyConstraints2D.FreezeRotation;
                    body.interpolation = RigidbodyInterpolation2D.Interpolate;
                    body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                    CapsuleCollider2D capsule = character.AddComponent<CapsuleCollider2D>();
                    capsule.size = new Vector2(0.78f, 1.45f);
                    capsule.sharedMaterial = material;
                    player = character.AddComponent<PlayerController2D>();
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
                    PrefabUtility.SaveAsPrefabAssetAndConnect(character, playerPath, InteractionMode.AutomatedAction);
                }

                Undo.RegisterCreatedObjectUndo(player.gameObject, "Add sandbox player");
            }

            GameObject controlsObject = Array.Find(scene.GetRootGameObjects(), item => item.name == "Sandbox Controls");
            PhysicsSandboxController controls = controlsObject != null
                ? controlsObject.GetComponent<PhysicsSandboxController>() : null;
            if (controls == null)
            {
                throw new InvalidOperationException("Sandbox Controls component was not found.");
            }

            SerializedObject controlProperties = new SerializedObject(controls);
            SerializedProperty playerReference = controlProperties.FindProperty("player");
            if (playerReference.objectReferenceValue != player)
            {
                Undo.RecordObject(controls, "Connect sandbox player reset");
                playerReference.objectReferenceValue = player;
                controlProperties.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            return player;
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

        private static LineRenderer CreateLine(string name, Transform parent, Material material,
            int pointCount, bool loop, float width)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.loop = loop;
            line.positionCount = pointCount;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = new Color(0.15f, 0.85f, 1f);
            line.endColor = line.startColor;
            line.sortingOrder = 4;
            line.numCapVertices = 3;
            line.numCornerVertices = 3;
            return line;
        }

        private static Sprite GetOrCreateSprite(string name, bool circular)
        {
            string path = ArtPath + "/" + name + ".asset";
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is Sprite sprite)
                {
                    return sprite;
                }
            }

            if (File.Exists(path))
            {
                throw new InvalidOperationException("Existing sprite asset has no Sprite subasset: " + path);
            }

            const int resolution = 64;
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.name = name + " Texture";
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[resolution * resolution];
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), Vector2.one * resolution * 0.5f);
                    float alpha = circular ? Mathf.Clamp01(resolution * 0.5f - distance) : 1f;
                    pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            Sprite result = Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), Vector2.one * 0.5f, resolution);
            result.name = name;
            AssetDatabase.AddObjectToAsset(result, texture);
            return result;
        }

        private static Material GetOrCreateLineMaterial()
        {
            const string path = ArtPath + "/FrameLines.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException("Sprites/Default shader was not found.");
            }

            Material material = new Material(shader) { name = "Frame Lines" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static PhysicsMaterial2D GetOrCreateContactMaterial()
        {
            const string path = ArtPath + "/BallContact.physicsMaterial2D";
            PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material != null)
            {
                return material;
            }

            material = new PhysicsMaterial2D("Ball Contact") { friction = 0.35f, bounciness = 0.12f };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
