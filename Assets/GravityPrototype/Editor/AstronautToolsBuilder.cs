using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype.Editor
{
    public static class AstronautToolsBuilder
    {
        [MenuItem("Tools/Gravity Prototype/Add Astronaut Devices to Sandbox")]
        public static void ConfigureActive()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != PhysicsSandboxBuilder.ScenePath)
                throw new InvalidOperationException("Open sandbox in Edit Mode first.");
            ConfigureScene(scene);
        }

        public static void ConfigureScene(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject player = roots.Single(g => g.name == "Player");
            GravityFrameDrag2D frame = roots.Single(g => g.name == "Gravity Frame").GetComponent<GravityFrameDrag2D>();
            Camera camera = roots.Single(g => g.name == "Main Camera").GetComponent<Camera>();
            PhysicsSandboxController controls = roots.FirstOrDefault(g => g.name == "Sandbox Controls")?.GetComponent<PhysicsSandboxController>();
            AstronautToolController tools = player.GetComponent<AstronautToolController>();
            if (tools == null) tools = Undo.AddComponent<AstronautToolController>(player);
            MechanicalArm2D arm = player.GetComponent<MechanicalArm2D>();
            if (arm == null) arm = Undo.AddComponent<MechanicalArm2D>(player);
            Transform visuals = player.transform.Find("Visuals");
            Sprite square = AssetDatabase.LoadAllAssetsAtPath("Assets/GravityPrototype/Art/Square.asset").OfType<Sprite>().Single();
            GameObject gravityVisual = FindOrCreateVisual(visuals, "Gravity Device", square,
                new Vector2(0.42f, -0.08f), new Vector2(0.27f, 0.34f), new Color(0.15f, 0.85f, 1f));
            GameObject armVisual = FindOrCreateVisual(visuals, "Mechanical Arm Device", square,
                new Vector2(0.48f, -0.1f), new Vector2(0.5f, 0.16f), new Color(1f, 0.75f, 0.2f));
            LineRenderer beam = player.GetComponentsInChildren<LineRenderer>(true).FirstOrDefault(l => l.name == "Mechanical Arm Beam");
            if (beam == null)
            {
                GameObject beamObject = new GameObject("Mechanical Arm Beam");
                Undo.RegisterCreatedObjectUndo(beamObject, "Add mechanical arm beam");
                beamObject.transform.SetParent(player.transform, false);
                beam = beamObject.AddComponent<LineRenderer>();
                beam.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/GravityPrototype/Art/FrameLines.mat");
                beam.positionCount = 2;
                beam.useWorldSpace = true;
                beam.startWidth = beam.endWidth = 0.035f;
                beam.sortingOrder = 5;
                beam.enabled = false;
            }
            SerializedObject so = new SerializedObject(tools);
            so.FindProperty("gravityFrame").objectReferenceValue = frame;
            so.FindProperty("mechanicalArm").objectReferenceValue = arm;
            so.FindProperty("gravityDeviceVisual").objectReferenceValue = gravityVisual;
            so.FindProperty("mechanicalArmVisual").objectReferenceValue = armVisual;
            so.ApplyModifiedProperties();
            so = new SerializedObject(arm);
            so.FindProperty("inputCamera").objectReferenceValue = camera;
            so.FindProperty("beam").objectReferenceValue = beam;
            so.ApplyModifiedProperties();
            if (controls != null)
            {
                so = new SerializedObject(controls);
                so.FindProperty("readBallSelectionInput").boolValue = false;
                so.ApplyModifiedProperties();
            }
            gravityVisual.SetActive(true);
            armVisual.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static GameObject FindOrCreateVisual(Transform parent, string name, Sprite sprite,
            Vector2 position, Vector2 size, Color color)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;
            GameObject result = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(result, "Add astronaut tool visual");
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = result.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 5;
            return result;
        }
    }
}
