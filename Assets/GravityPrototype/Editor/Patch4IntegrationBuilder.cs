using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using GravityPrototype.GameFlow;
using GravityPrototype.Progress;

namespace GravityPrototype.Editor
{
    public static class Patch4IntegrationBuilder
    {
        [MenuItem("Tools/Gravity Prototype/Integrate Patch4 Level Flow")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Edit Mode required.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new System.InvalidOperationException("Save scene changes first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                PrototypeAssetFactory.EnsureFolder("Assets/GravityPrototype/Data");
                const string path="Assets/GravityPrototype/Data/LevelCatalog.asset";
                var catalog=AssetDatabase.LoadAssetAtPath<LevelCatalog>(path);
                if(catalog==null){catalog=ScriptableObject.CreateInstance<LevelCatalog>();catalog.SetLevels(new[]{new LevelDefinition("Level01","第 1 关 · 尖刺航道","PlatformLevel")});AssetDatabase.CreateAsset(catalog,path);}
                var scene=EditorSceneManager.OpenScene(PlatformLevelBuilder.LevelScenePath,OpenSceneMode.Single);
                catalog=AssetDatabase.LoadAssetAtPath<LevelCatalog>(path);
                var level=GameObject.Find("Level").GetComponent<LevelController>();
                var flow=level.GetComponent<LevelFlowController>();if(flow==null)flow=level.gameObject.AddComponent<LevelFlowController>();
                if(level.GetComponent<GamePauseController>()==null)level.gameObject.AddComponent<GamePauseController>();
                var so=new SerializedObject(flow);so.FindProperty("level").objectReferenceValue=level;so.FindProperty("catalog").objectReferenceValue=catalog;
                so.FindProperty("pauseController").objectReferenceValue=level.GetComponent<GamePauseController>();
                so.ApplyModifiedPropertiesWithoutUndo();
                var oldHud=GameObject.Find("Level HUD").GetComponent<LevelHudUI>();oldHud.enabled=false;
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                var menuScene=EditorSceneManager.OpenScene(PlatformLevelBuilder.MenuScenePath,OpenSceneMode.Single);
                catalog=AssetDatabase.LoadAssetAtPath<LevelCatalog>(path);
                LevelMenuUI menu=null;
                foreach(var root in menuScene.GetRootGameObjects())
                    if((menu=root.GetComponentInChildren<LevelMenuUI>(true))!=null)break;
                if(menu==null)throw new System.InvalidOperationException("Level menu component is missing.");
                var menuProperties=new SerializedObject(menu);
                menuProperties.FindProperty("catalog").objectReferenceValue=catalog;
                menuProperties.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(menuScene);EditorSceneManager.SaveScene(menuScene);AssetDatabase.SaveAssets();
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
    }
}
