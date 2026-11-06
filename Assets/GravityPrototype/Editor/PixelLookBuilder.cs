using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityPrototype.Editor
{
    /// <summary>Presentation-only authoring. Never changes colliders, body mass, physics profiles or camera projection.</summary>
    public static class PixelLookBuilder
    {
        private const string Folder = "Assets/GravityPrototype/Art/Pixel";
        private static Color C(string hex) => PixelUI.Hex(hex);
        private static readonly Color[] SampledPalette = {
            C("050505"),C("323846"),C("454B57"),C("5F6975"),C("8E959F"),C("C3C7D0"),C("E1E3E2"),C("FEFFFA"),
            C("653791"),C("76499E"),C("926EB4"),C("BCA5D5"),C("07418A"),C("2B6AAF"),C("FDEBD3"),C("5391CE"),
            C("EF6C28"),C("DE4039"),C("F78E3D"),C("FCAD4E"),C("894D29"),C("20AF8D"),C("D09E3B"),C("DBC480") };
        private static Sprite Palette() => Sprite("SampledPalette",8,3,p=> {for(int i=0;i<24;i++)p.Dot(i%8,2-i/8,SampledPalette[i]);});

        private sealed class Canvas
        {
            public readonly int W, H;
            public readonly Color[] Pixels;
            public Canvas(int w, int h) { W=w; H=h; Pixels=new Color[w*h]; }
            public void Rect(int x,int y,int w,int h,Color c)
            { for(int j=Math.Max(y,0);j<Math.Min(y+h,H);j++) for(int i=Math.Max(x,0);i<Math.Min(x+w,W);i++) Pixels[j*W+i]=c; }
            public void Dot(int x,int y,Color c) => Rect(x,y,1,1,c);
        }
        private static Sprite Sprite(string name,int w,int h,Action<Canvas> paint)
        {
            string path=Folder+"/"+name+".asset";
            Sprite existing=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if(existing!=null) return existing;
            var canvas=new Canvas(w,h); paint(canvas);
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false) { name=name+" Pixels",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Repeat };
            texture.SetPixels(canvas.Pixels);texture.Apply();
            AssetDatabase.CreateAsset(texture,path);
            var sprite=UnityEngine.Sprite.Create(texture,new Rect(0,0,w,h),Vector2.one*0.5f,w,0,SpriteMeshType.FullRect);
            sprite.name=name;AssetDatabase.AddObjectToAsset(sprite,texture);
            return sprite;
        }
        private static Sprite Suit() => Sprite("Astronaut",16,28,p=> {
            p.Rect(3,1,4,4,C("323846"));p.Rect(9,1,4,4,C("323846"));
            p.Rect(3,1,4,1,C("050505"));p.Rect(9,1,4,1,C("050505"));
            p.Rect(3,5,10,13,C("653791"));p.Rect(4,6,8,12,C("926EB4"));p.Rect(4,7,3,10,C("BCA5D5"));
            p.Rect(1,7,3,10,C("76499E"));p.Rect(12,7,3,10,C("76499E"));
            p.Rect(1,7,3,3,C("C3C7D0"));p.Rect(12,7,3,3,C("C3C7D0"));
            p.Rect(6,10,5,5,C("454B57"));p.Rect(7,12,2,2,C("20AF8D"));p.Dot(10,11,C("FCAD4E"));
            p.Rect(2,18,12,8,C("323846"));p.Rect(3,18,10,8,C("E1E3E2"));p.Rect(5,26,6,1,C("C3C7D0"));
            p.Rect(4,20,9,4,C("07418A"));p.Rect(5,22,7,2,C("2B6AAF"));p.Rect(5,23,3,1,C("5391CE"));
            p.Dot(11,22,C("FDEBD3"));p.Rect(4,18,8,1,C("8E959F"));
        });
        private static Sprite Orb(string name,string mid,string light,string dark) => Sprite(name,16,16,p=> {
            for(int y=0;y<16;y++)for(int x=0;x<16;x++) {
                int dx=x*2-15,dy=y*2-15; if(dx*dx+dy*dy>225)continue;
                p.Dot(x,y,C(dx*dx+dy*dy>169?dark:(y>9&&x<8?light:mid)));
            }
            p.Rect(4,10,2,2,C("FDEBD3"));p.Rect(10,3,2,2,C(dark));
            if(name=="WoodOrb") {p.Rect(8,5,1,6,C(dark));p.Rect(9,4,2,1,C(dark));p.Rect(5,6,2,1,C(dark));}
            if(name=="IronOrb") {p.Rect(3,7,10,1,C(dark));p.Dot(4,5,C("E1E3E2"));p.Dot(11,10,C("E1E3E2"));}
        });
        private static Sprite Tile() => Sprite("HullTile",16,16,p=> {
            p.Rect(0,0,16,16,C("323846"));p.Rect(1,1,14,14,C("454B57"));
            p.Rect(1,14,14,1,C("5F6975"));p.Rect(1,1,14,1,C("222429"));
            p.Dot(2,2,C("8E959F"));p.Dot(13,13,C("8E959F"));p.Rect(4,5,8,1,C("323846"));
            p.Rect(4,7,8,1,C("323846"));
        });
        private static Sprite Tooth() => Sprite("SpikeTooth",12,12,p=> {
            for(int y=0;y<12;y++) {int half=(11-y)/2;for(int x=5-half;x<=6+half;x++)p.Dot(x,y,C(x<6?"F78E3D":"DE4039"));}
            p.Rect(0,0,12,2,C("894D29"));
        });
        private static Sprite Pad() => Sprite("LaunchPad",32,8,p=> {
            p.Rect(0,0,32,7,C("323846"));p.Rect(1,2,30,4,C("07418A"));p.Rect(0,7,32,1,C("20AF8D"));
            p.Rect(1,6,30,1,C("20AF8D"));for(int x=3;x<30;x+=6){p.Rect(x,2,3,3,C("D09E3B"));p.Dot(x+1,5,C("FDEBD3"));}
        });
        private static Sprite Device(bool arm) => Sprite(arm?"ArmDevice":"GravityDevice",12,12,p=> {
            p.Rect(1,1,10,10,C("323846"));p.Rect(2,2,8,8,C(arm?"894D29":"07418A"));
            p.Rect(3,3,6,6,C(arm?"FCAD4E":"20AF8D"));p.Rect(4,5,4,2,C("FDEBD3"));
            p.Rect(5,4,2,4,C("FDEBD3"));p.Dot(8,8,C("FEFFFA"));
        });
        private static Sprite Background() => Sprite("OrbitalBackdrop",384,216,p=> {
            p.Rect(0,0,384,216,C("323846"));
            for(int y=0;y<216;y+=32)for(int x=0;x<384;x+=48) {
                p.Rect(x,y,47,31,C("454B57"));p.Rect(x+1,y+1,45,29,C("323846"));
            }
            var random=new System.Random(41);
            for(int i=0;i<95;i++){int x=random.Next(384),y=random.Next(216);p.Dot(x,y,C(i%4==0?"BCA5D5":"5F6975"));}
            p.Rect(246,70,76,102,C("050505"));p.Rect(249,73,70,96,C("07418A"));
            p.Rect(253,77,62,88,C("323846"));
            for(int i=0;i<26;i++)p.Dot(255+random.Next(57),79+random.Next(83),C("8E959F"));
            p.Rect(246,118,76,3,C("454B57"));p.Rect(282,70,3,102,C("454B57"));
            p.Rect(29,86,30,45,C("454B57"));p.Rect(32,89,24,39,C("050505"));
            p.Rect(36,119,16,2,C("20AF8D"));p.Rect(36,113,11,2,C("2B6AAF"));
            p.Rect(36,103,3,3,C("FCAD4E"));p.Rect(43,103,3,3,C("DE4039"));
            p.Rect(30,34,75,3,C("653791"));p.Rect(33,35,24,1,C("926EB4"));
        });
        private static void Renderer(SpriteRenderer r,Sprite sprite,Vector2 size,int order)
        {
            r.sprite=sprite;r.color=Color.white;r.sortingOrder=order;
            r.transform.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);
        }
        private static GameObject Child(Transform parent,string name)
        {
            var t=parent.Find(name);if(t!=null)return t.gameObject;
            var go=new GameObject(name);go.transform.SetParent(parent,false);return go;
        }

        [MenuItem("Tools/Gravity Prototype/Apply Palette Pixel Look")]
        public static void ApplyAll()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Edit Mode required.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene changes first.");
            PrototypeAssetFactory.EnsureFolder(Folder);
            Suit();Tile();Pad();Tooth();Palette();Device(false);Device(true);Background();
            Orb("BalloonOrb","BCA5D5","FEFFFA","76499E");Orb("WoodOrb","F78E3D","FCAD4E","894D29");Orb("IronOrb","8E959F","E1E3E2","454B57");
            try {
                foreach(string path in new[]{PhysicsSandboxBuilder.ScenePath,PlatformLevelBuilder.LevelScenePath,PlatformLevelBuilder.MenuScenePath}) {
                    var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);ApplyScene(scene);EditorSceneManager.SaveScene(scene);
                }
            } finally {EditorSceneManager.RestoreSceneManagerSetup(setup);AssetDatabase.SaveAssets();}
        }
        private static Sprite Load(string name)=>AssetDatabase.LoadAllAssetsAtPath(Folder+"/"+name+".asset").OfType<Sprite>().Single();
        public static void ApplyScene(Scene scene)
        {
            var roots=scene.GetRootGameObjects();
            var camera=roots.SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();
            camera.backgroundColor=C("050505");camera.allowMSAA=false;
            if(camera.GetComponent<PixelViewport>()==null)camera.gameObject.AddComponent<PixelViewport>();
            var background=roots.FirstOrDefault(g=>g.name=="Pixel Backdrop")??new GameObject("Pixel Backdrop");
            SceneManager.MoveGameObjectToScene(background,scene);background.transform.position=Vector3.zero;
            var bg=background.GetComponent<SpriteRenderer>();if(bg==null)bg=background.AddComponent<SpriteRenderer>();Renderer(bg,Load("OrbitalBackdrop"),new Vector2(24,13.5f),-20);
            var player=roots.Select(g=>g.GetComponent<PlayerController2D>()).FirstOrDefault(p=>p!=null);
            if(player!=null) {
                var visuals=player.transform.Find("Visuals");
                foreach(var old in visuals.GetComponentsInChildren<SpriteRenderer>(true))
                    if(old.name!="Gravity Device"&&old.name!="Mechanical Arm Device"&&old.name!="Pixel Astronaut") old.enabled=false;
                var suit=Child(visuals,"Pixel Astronaut");var sr=suit.GetComponent<SpriteRenderer>();if(sr==null)sr=suit.AddComponent<SpriteRenderer>();
                Renderer(sr,Load("Astronaut"),new Vector2(0.82f,1.45f),4);
                foreach(bool arm in new[]{false,true}) {
                    var tool=visuals.Find(arm?"Mechanical Arm Device":"Gravity Device");if(tool==null)continue;
                    var tr=tool.GetComponent<SpriteRenderer>();Renderer(tr,Load(arm?"ArmDevice":"GravityDevice"),arm?new Vector2(0.42f,0.28f):new Vector2(0.3f,0.36f),6);
                }
            }
            foreach(var ball in roots.SelectMany(g=>g.GetComponentsInChildren<BallMaterial2D>(true))) {
                var baseRenderer=ball.GetComponent<SpriteRenderer>();baseRenderer.enabled=false;
                var marker=ball.transform.Find("Rotation Marker");if(marker!=null)marker.gameObject.SetActive(false);
                var art=Child(ball.transform,"Pixel Orb");var r=art.GetComponent<SpriteRenderer>();if(r==null)r=art.AddComponent<SpriteRenderer>();
                Renderer(r,Load("WoodOrb"),Vector2.one,3);
                var view=ball.GetComponent<PixelBallView>();if(view==null)view=ball.gameObject.AddComponent<PixelBallView>();
                var so=new SerializedObject(view);so.FindProperty("balloon").objectReferenceValue=Load("BalloonOrb");so.FindProperty("wood").objectReferenceValue=Load("WoodOrb");
                so.FindProperty("iron").objectReferenceValue=Load("IronOrb");so.FindProperty("artwork").objectReferenceValue=r;so.ApplyModifiedPropertiesWithoutUndo();
            }
            var env=roots.FirstOrDefault(g=>g.name=="Environment");
            if(env!=null)foreach(var renderer in env.GetComponentsInChildren<SpriteRenderer>()) {
                var pad=renderer.GetComponent<GravityBouncePad2D>();
                if(pad!=null){
                    renderer.enabled=false;
                    var art=Child(renderer.transform,"Pixel Launch Pad");var pr=art.GetComponent<SpriteRenderer>();if(pr==null)pr=art.AddComponent<SpriteRenderer>();
                    Renderer(pr,Load("LaunchPad"),Vector2.one,renderer.sortingOrder);
                    continue;
                }
                if(renderer.GetComponent<BoxCollider2D>()!=null && renderer.GetComponent<SpikeHazard2D>()==null && renderer.GetComponent<LevelGoal2D>()==null) {
                    if(renderer.name=="Pixel Hull")continue;
                    renderer.enabled=false;
                    var art=Child(renderer.transform,"Pixel Hull");
                    var hull=art.GetComponent<SpriteRenderer>();if(hull==null)hull=art.AddComponent<SpriteRenderer>();
                    hull.sprite=Load("HullTile");hull.color=Color.white;hull.sortingOrder=renderer.sortingOrder;
                    hull.drawMode=SpriteDrawMode.Tiled;
                    Vector3 scale=renderer.transform.lossyScale;
                    hull.size=new Vector2(Mathf.Abs(scale.x),Mathf.Abs(scale.y));
                    art.transform.localScale=new Vector3(1f/Mathf.Abs(scale.x),1f/Mathf.Abs(scale.y),1);
                    art.transform.localPosition=Vector3.zero;
                }
                else if(renderer.name=="Tooth"){renderer.sprite=Load("SpikeTooth");renderer.color=Color.white;renderer.transform.localRotation=Quaternion.identity;}
                else if(renderer.name=="Base")renderer.color=C("894D29");
                else if(renderer.name=="Flag")renderer.color=C("20AF8D");
                else if(renderer.name=="Pole")renderer.color=C("C3C7D0");
            }
            foreach(var frame in roots.SelectMany(g=>g.GetComponentsInChildren<GravityRegion2D>())) {
                foreach(var line in frame.GetComponentsInChildren<LineRenderer>()) {line.startColor=line.endColor=C("20AF8D");line.numCapVertices=0;line.numCornerVertices=0;}
                var fill=frame.transform.Find("Fill").GetComponent<SpriteRenderer>();fill.color=new Color(0.125f,0.686f,0.553f,0.09f);
            }
            if (SuppliedArtBuilder.IsAvailable) SuppliedArtBuilder.ApplyScene(scene);
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
