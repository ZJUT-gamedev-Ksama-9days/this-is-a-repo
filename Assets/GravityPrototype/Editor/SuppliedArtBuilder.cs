using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype.Editor
{
    /// <summary>Integrates supplied art at 32 PPU without changing sphere profiles or hazard geometry.</summary>
    public static class SuppliedArtBuilder
    {
        public const string Folder = "Assets/GravityPrototype/Art/Supplied";
        public const string NativeFolder = Folder + "/Native32";
        public static bool IsAvailable => AssetDatabase.LoadAssetAtPath<Sprite>(NativeFolder + "/SteelTile.png") != null;
        public static Sprite Load(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(NativeFolder + "/" + name + ".png");

        [MenuItem("Tools/Gravity Prototype/Integrate Supplied Art at 32 PPU")]
        public static void ApplyAll()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode required.");
            for (int i=0;i<SceneManager.sceneCount;i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits first.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                Generate();
                foreach (string path in new[]{PhysicsSandboxBuilder.ScenePath, PlatformLevelBuilder.LevelScenePath, PlatformLevelBuilder.MenuScenePath})
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    ApplyScene(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); AssetDatabase.SaveAssets(); }
        }

        public static void Generate()
        {
            PrototypeAssetFactory.EnsureFolder(NativeFolder);
            foreach (string name in new[]{"Meteor01","Orb","Spikes","SteelTile","Meteor02"})
            {
                string sourcePath=Folder+"/"+name+".png";
                var importer=(TextureImporter)AssetImporter.GetAtPath(sourcePath);
                if (importer==null) throw new InvalidOperationException("Missing supplied image: "+sourcePath);
                bool readable=importer.isReadable;
                importer.isReadable=true;importer.SaveAndReimport();
                var source=AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                var pixels=new Color32[32*32];
                try
                {
                    for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                        pixels[y*32+x]=source.GetPixel(Mathf.Min(source.width-1,(2*x+1)*source.width/64),
                            Mathf.Min(source.height-1,(2*y+1)*source.height/64));
                    Save(name,32,32,pixels);
                }
                finally { importer.isReadable=readable;importer.SaveAndReimport(); }
            }
            MakeAstronaut();
        }

        private static void Save(string name,int width,int height,Color32[] pixels)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            texture.SetPixels32(pixels);texture.Apply();
            string path=NativeFolder+"/"+name+".png";
            File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite;
            importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=PixelMetrics.PixelsPerUnit;
            importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;
            importer.npotScale=TextureImporterNPOTScale.None;
            importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency=true;
            importer.wrapMode=name=="SteelTile"?TextureWrapMode.Repeat:TextureWrapMode.Clamp;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
            settings.spriteMeshType=SpriteMeshType.FullRect;settings.spriteGenerateFallbackPhysicsShape=false;
            importer.SetTextureSettings(settings);importer.SaveAndReimport();
        }

        private static void MakeAstronaut()
        {
            var pixels=new Color32[32*48];
            Action<int,int,int,int,string> rect=(x,y,w,h,hex)=>{Color c=PixelUI.Hex(hex);
                for(int j=y;j<y+h;j++)for(int i=x;i<x+w;i++)if(i>=0&&i<32&&j>=0&&j<48)pixels[j*32+i]=c;};
            rect(7,0,7,6,"323846");rect(18,0,7,6,"323846");
            rect(8,1,6,2,"050505");rect(18,1,6,2,"050505");
            rect(9,6,14,10,"653791");rect(10,7,5,9,"926EB4");rect(17,7,5,9,"76499E");
            rect(6,16,20,17,"653791");rect(8,17,16,15,"926EB4");rect(8,18,5,13,"BCA5D5");
            rect(3,17,5,15,"76499E");rect(24,17,5,15,"76499E");
            rect(3,16,5,5,"C3C7D0");rect(24,16,5,5,"C3C7D0");
            rect(8,16,16,2,"454B57");rect(13,19,9,9,"454B57");
            rect(14,20,7,7,"323846");rect(15,23,4,3,"20AF8D");rect(20,21,1,2,"FCAD4E");
            rect(6,33,20,12,"323846");rect(8,32,16,15,"C3C7D0");rect(9,34,14,12,"E1E3E2");
            rect(10,46,12,2,"C3C7D0");rect(8,33,16,1,"8E959F");
            rect(9,36,15,8,"07418A");rect(10,39,13,4,"2B6AAF");
            rect(11,42,7,1,"5391CE");rect(20,39,2,2,"FDEBD3");
            rect(7,35,2,3,"5F6975");rect(24,35,2,3,"5F6975");
            Save("Astronaut32x48",32,48,pixels);
        }

        private static GameObject Child(Transform parent,string name)
        {var t=parent.Find(name);if(t!=null)return t.gameObject;var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
        private static SpriteRenderer Visual(Transform parent,string name)
        {var go=Child(parent,name);var r=go.GetComponent<SpriteRenderer>();return r!=null?r:go.AddComponent<SpriteRenderer>();}

        public static void ApplyPlayerVisual(PlayerController2D player)
        {
            Transform visuals=player.transform.Find("Visuals");if(visuals==null)return;
            foreach(var old in visuals.GetComponentsInChildren<SpriteRenderer>(true))
                if(old.name!="Gravity Device"&&old.name!="Mechanical Arm Device"&&old.name!="Pixel Astronaut")old.enabled=false;
            var art=Visual(visuals,"Pixel Astronaut");art.sprite=Load("Astronaut32x48");
            art.enabled=true;art.color=Color.white;art.sortingOrder=4;
            art.transform.localScale=Vector3.one;art.transform.localPosition=PixelMetrics.PlayerArtworkOffset;
            if (CharacterAnimationBuilder.IsAvailable) CharacterAnimationBuilder.Apply(player);
        }

        public static void ApplyScene(Scene scene)
        {
            var roots=scene.GetRootGameObjects();
            foreach(var player in roots.SelectMany(r=>r.GetComponentsInChildren<PlayerController2D>(true)))
            {
                var capsule=player.GetComponent<CapsuleCollider2D>();
                float oldBottom=capsule.bounds.min.y;
                bool needsSize=(capsule.size-PixelMetrics.PlayerColliderSize).sqrMagnitude>0.000001f;
                capsule.size=PixelMetrics.PlayerColliderSize;capsule.offset=Vector2.zero;
                if(needsSize)
                {
                    float newBottom=player.transform.TransformPoint(new Vector3(0,-capsule.size.y*0.5f,0)).y;
                    player.transform.position+=Vector3.up*(oldBottom-newBottom);
                }
                ApplyPlayerVisual(player);
                PrefabUtility.RecordPrefabInstancePropertyModifications(capsule);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
            }
            foreach(var ball in roots.SelectMany(r=>r.GetComponentsInChildren<PixelBallView>(true)))
            {
                var so=new SerializedObject(ball);so.FindProperty("wood").objectReferenceValue=Load("Orb");so.ApplyModifiedPropertiesWithoutUndo();
            }
            var environment=roots.FirstOrDefault(r=>r.name=="Environment");
            if(environment!=null)
            {
                foreach(var hull in environment.GetComponentsInChildren<SpriteRenderer>(true).Where(r=>r.name=="Pixel Hull"))
                {hull.sprite=Load("SteelTile");hull.color=Color.white;hull.drawMode=SpriteDrawMode.Tiled;}
                foreach(var hazard in environment.GetComponentsInChildren<SpikeHazard2D>(true))
                {
                    foreach(var old in hazard.GetComponentsInChildren<SpriteRenderer>(true))
                        if(old.name!="Supplied Spike Strip")old.enabled=false;
                    var box=hazard.GetComponent<BoxCollider2D>();
                    var spikes=Visual(hazard.transform,"Supplied Spike Strip");
                    spikes.sprite=Load("Spikes");spikes.color=Color.white;spikes.sortingOrder=2;spikes.enabled=true;
                    spikes.drawMode=SpriteDrawMode.Tiled;
                    // Source art has transparent margins; size visible art to the existing trigger height.
                    const float visibleHeight=23f/32f;
                    float cell=box.size.y/visibleHeight;
                    spikes.transform.localScale=new Vector3(cell,cell,1);
                    spikes.size=new Vector2(box.size.x/cell,1);
                    spikes.transform.localPosition=new Vector3(box.offset.x,box.offset.y-box.size.y*0.5f+(0.5f-0.1875f)*cell,0);
                }
            }
            // Decorative meteor props are behind gameplay, not obstacles or new hazards.
            var backdrop=roots.FirstOrDefault(r=>r.name=="Pixel Backdrop");
            if(backdrop!=null)
            {
                for(int i=0;i<2;i++)
                {
                    var meteor=Visual(backdrop.transform,i==0?"Meteor01 Decor":"Meteor02 Decor");
                    meteor.sprite=Load(i==0?"Meteor01":"Meteor02");meteor.sortingOrder=-19;meteor.color=new Color(0.8f,0.8f,0.8f,1);
                    // Parent backdrop is scaled: set world size and world position explicitly.
                    meteor.transform.localScale=new Vector3((i==0?1.25f:0.9f)/backdrop.transform.lossyScale.x,(i==0?1.25f:0.9f)/backdrop.transform.lossyScale.y,1);
                    meteor.transform.position=i==0?new Vector3(4.9f,2.15f,0):new Vector3(7.2f,-0.5f,0);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
