using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using GravityPrototype.Progress;
using GravityPrototype.GameFlow;
namespace GravityPrototype.Editor
{
    public static class Pr7LevelsBuilder
    {
        public const string Folder="Assets/GravityPrototype/Scenes/PR7";
        public static string PathFor(int i)=>Folder+"/GridLevel"+i.ToString("00")+".unity";
        public static string IdFor(int i)=>"GridLevel"+i.ToString("00");
        private static readonly Vector2[] Spawns={new Vector2(-8.5f,1f),new Vector2(-8.5f,-1f),new Vector2(-8.5f,-1f),new Vector2(-8.5f,-3f),new Vector2(-8.5f,0f)};
        [MenuItem("Tools/Gravity Prototype/Integrate PR7 Playable Levels")]
        public static void Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required.");
            for(int j=0;j<SceneManager.sceneCount;j++)if(SceneManager.GetSceneAt(j).isDirty)throw new InvalidOperationException("Save scene changes first.");
            for(int i=1;i<=5;i++)if(System.IO.File.Exists(PathFor(i)))throw new InvalidOperationException("Refusing to overwrite existing scene: "+PathFor(i));
            var setup=EditorSceneManager.GetSceneManagerSetup();
            PrototypeAssetFactory.EnsureFolder(Folder);
            try
            {
                for(int i=1;i<=5;i++)Build(i);
                var catalog=AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/GravityPrototype/Data/LevelCatalog.asset");
                var definitions=new List<LevelDefinition>{new LevelDefinition("Level01","试验航道 / ORIGINAL","PlatformLevel")};
                for(int i=1;i<=5;i++)definitions.Add(new LevelDefinition(IdFor(i),"第 "+i+" 关 / SECTOR "+i,IdFor(i)));
                catalog.SetLevels(definitions.ToArray());EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssets();
                var builds=EditorBuildSettings.scenes.ToList();
                for(int i=1;i<=5;i++)if(!builds.Any(s=>s.path==PathFor(i)))builds.Add(new EditorBuildSettingsScene(PathFor(i),true));
                EditorBuildSettings.scenes=builds.ToArray();
                Patch4IntegrationBuilder.Apply();
            }
            finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
        private static void Build(int index)
        {
            // Reuse tested player, camera and astronaut device wiring; never overwrite the template.
            var scene=EditorSceneManager.OpenScene(PlatformLevelBuilder.LevelScenePath,OpenSceneMode.Single);
            foreach(var r in scene.GetRootGameObjects())
                if(r.name=="Environment"||r.name=="Level HUD")UnityEngine.Object.DestroyImmediate(r);
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Camera>()).Single();
            camera.transform.position=new Vector3(0f,-.5f,-10f);camera.orthographicSize=7.5f;
            var player=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerController2D>()).Single();
            player.transform.position=Spawns[index-1];
            var frame=scene.GetRootGameObjects().Single(r=>r.name=="Gravity Frame");frame.transform.position=new Vector3(-5.5f,1f,0f);
            var grid=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GravityPrototype/Prefabs/PR7Grid.prefab"),scene);
            PrefabUtility.UnpackPrefabInstance(grid,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            foreach(Transform child in grid.transform.Cast<Transform>().ToArray())
                if(child.name!="level "+index&&child.name!="level "+index+" overlay")UnityEngine.Object.DestroyImmediate(child.gameObject);
            var map=grid.transform.Find("level "+index).GetComponent<Tilemap>();
            var circuit=grid.transform.Find("level "+index+" overlay").GetComponent<Tilemap>();
            // Keep the circuit art one layer beneath the terrain tilemap (level = 0).
            circuit.GetComponent<TilemapRenderer>().sortingOrder=-1;
            var terrain=new GameObject("Terrain Collisions");terrain.transform.SetParent(grid.transform,false);
            var props=new GameObject("Mechanisms");props.transform.SetParent(grid.transform,false);
            var platforms=new List<Transform>();var levers=new List<LeverMechanism2D>();var circuitDoors=new List<CircuitGate2D>();var anomaly=new List<AnomalyGate2D>();
            LevelGoal2D goal=null;
            foreach(var cell in map.cellBounds.allPositionsWithin)
            {
                var tile=map.GetTile<Tile>(cell);if(tile==null)continue;
                var name=tile.name;var position=map.GetCellCenterWorld(cell);var matrix=map.GetTransformMatrix(cell);
                if(name.StartsWith("瓦片"))
                {var collider=terrain.AddComponent<BoxCollider2D>();collider.offset=position;collider.size=Vector2.one;collider.sharedMaterial=Ground();continue;}
                var go=new GameObject(name+" "+cell.x+","+cell.y);go.transform.SetParent(props.transform,false);go.transform.position=position;
                go.transform.rotation=matrix.rotation;
                var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=tile.sprite;renderer.sortingOrder=3;
                // Keep the artist's per-cell orientation; move interactive tiles to real objects.
                go.transform.localScale=matrix.lossyScale;map.SetTile(cell,null);
                if(name.StartsWith("目标门"))
                {var c=go.AddComponent<BoxCollider2D>();c.size=new Vector2(.75f,1.15f);c.isTrigger=true;goal=go.AddComponent<LevelGoal2D>();}
                else if(name.StartsWith("尖刺"))
                {var c=go.AddComponent<BoxCollider2D>();c.size=new Vector2(.78f,.72f);c.isTrigger=true;go.AddComponent<SpikeHazard2D>();}
                else if(name.StartsWith("旋转平台"))
                {
                    // Diagonal artwork is a pose of the same two-metre platform, not a solid square.
                    if(name.Contains("倾斜")){renderer.sprite=SpriteFor("旋转平台.png");go.transform.Rotate(0,0,45f);}
                    var body=go.AddComponent<Rigidbody2D>();body.bodyType=RigidbodyType2D.Kinematic;
                    var c=go.AddComponent<BoxCollider2D>();c.size=new Vector2(2f,.25f);c.sharedMaterial=Ground();platforms.Add(go.transform);
                }
                else if(name.StartsWith("杠杆"))
                {var c=go.AddComponent<BoxCollider2D>();c.size=Vector2.one*.8f;c.isTrigger=true;levers.Add(go.AddComponent<LeverMechanism2D>());}
                else if(name.StartsWith("电路门"))
                {var c=go.AddComponent<BoxCollider2D>();c.size=new Vector2(.7f,1.15f);circuitDoors.Add(go.AddComponent<CircuitGate2D>());}
                // 电闸是装饰：没有交互也没有碰撞体，电路是否通路与它无关。
                else if(name.StartsWith("异常门"))
                {var c=go.AddComponent<BoxCollider2D>();c.size=new Vector2(.8f,1f);c.isTrigger=true;anomaly.Add(go.AddComponent<AnomalyGate2D>());}
                else if(name.StartsWith("陨石"))
                {var c=go.AddComponent<BoxCollider2D>();c.size=new Vector2(.8f,.6f);c.sharedMaterial=Ground();}
            }
            foreach(var lever in levers)
            {
                if(platforms.Count==0)throw new InvalidOperationException("Lever has no platform.");
                var platform=platforms.OrderBy(t=>(t.position-lever.transform.position).sqrMagnitude).First();
                Set(lever,"platform",platform);Set(lever,"platformBody",platform.GetComponent<Rigidbody2D>());
            }
            // 2.5.1：第 1 关没有小球，第 2 关 1 个，第 3 关 2 个，第 4/5 关 3 个。
            int ballCount=index==1?0:index==2?1:index==3?2:3;
            var balls=new List<Rigidbody2D>();
            for(int b=0;b<ballCount;b++)balls.Add(CreateBall(props.transform,new Vector2(-7.5f+b*.7f,Spawns[index-1].y+.5f+b*.6f)));
            var ball=balls.Count>0?balls[0]:null;
            foreach(var door in circuitDoors)
            {
                var so=new SerializedObject(door);
                so.FindProperty("circuit").objectReferenceValue=circuit;
                foreach(var pair in new[]{("emptyStraight","空直电路"),("poweredStraight","通直电路"),("emptyCorner","空弯电路"),("poweredCorner","通弯电路")})
                    so.FindProperty(pair.Item1).objectReferenceValue=AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tile/"+pair.Item2+"_0.asset");
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // 2.3.6.3：没有电路门的关卡（第一、二关）一开始就通路，贴图直接用通电的蓝色版本。
            if(circuitDoors.Count==0)
            {
                circuit.SwapTile(AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tile/空直电路_0.asset"),AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tile/通直电路_0.asset"));
                circuit.SwapTile(AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tile/空弯电路_0.asset"),AssetDatabase.LoadAssetAtPath<Tile>("Assets/Tile/通弯电路_0.asset"));
            }
            foreach(var gate in anomaly)if(ball!=null)Set(gate,"ball",ball);
            if(goal==null)throw new InvalidOperationException("Goal missing in PR7 level "+index);
            // 2.3.6.1 + 2.5.1：第 1 关目标门常开；第 2–5 关由小球到达后才打开。
            var goalProperties=new SerializedObject(goal);goalProperties.FindProperty("opensWithBall").boolValue=ballCount>0;goalProperties.ApplyModifiedPropertiesWithoutUndo();
            var level=scene.GetRootGameObjects().Single(r=>r.name=="Level").GetComponent<LevelController>();
            var levelProperties=new SerializedObject(level);levelProperties.FindProperty("levelId").stringValue=IdFor(index);
            levelProperties.FindProperty("goal").objectReferenceValue=goal;levelProperties.ApplyModifiedPropertiesWithoutUndo();
            Set(level.GetComponent<LevelFlowController>(),"catalog",AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/GravityPrototype/Data/LevelCatalog.asset"));
            // The drafts have open lower edges. An invisible catch boundary prevents unrecoverable void falls.
            var boundary=new GameObject("Lower Safety Boundary");boundary.transform.SetParent(grid.transform,false);boundary.transform.position=new Vector3(0,-6f,0);
            var floor=boundary.AddComponent<BoxCollider2D>();floor.size=new Vector2(22f,1f);floor.sharedMaterial=Ground();
            EditorSceneManager.SaveScene(scene,PathFor(index));
        }
        private static PhysicsMaterial2D Ground()=>AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/GravityPrototype/Physics/GroundContact.physicsMaterial2D");
        private static Sprite SpriteFor(string file)=>AssetDatabase.LoadAllAssetsAtPath("Assets/GravityPrototype/Art/Images/"+file).OfType<Sprite>().First();
        private static Rigidbody2D CreateBall(Transform parent,Vector2 position)
        {
            var go=new GameObject("Physics Ball");go.transform.SetParent(parent,false);go.transform.position=position;
            go.AddComponent<SpriteRenderer>().sprite=SpriteFor("小球.PNG");
            var body=go.AddComponent<Rigidbody2D>();body.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>();go.AddComponent<GravityBody2D>();var material=go.AddComponent<BallMaterial2D>();
            var profile=AssetDatabase.FindAssets("t:BallPhysicsProfile").Select(AssetDatabase.GUIDToAssetPath).First(p=>p.Contains("Wood"));
            material.ApplyProfile(AssetDatabase.LoadAssetAtPath<BallPhysicsProfile>(profile));
            go.GetComponent<SpriteRenderer>().color=Color.white;return body;
        }
        private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
        {var so=new SerializedObject(target);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    }
}
