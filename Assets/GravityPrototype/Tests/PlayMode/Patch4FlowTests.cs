using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GravityPrototype.GameFlow;
using GravityPrototype.Progress;

namespace GravityPrototype.Tests
{
    public sealed class Patch4FlowTests
    {
        [UnityTest] public IEnumerator Menu_UsesUguiAndSharedPopupsWithoutConsumingSaveIntent()
        {
            const string path="Assets/GravityPrototype/Scenes/LevelMenu.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
            var scene=SceneManager.GetSceneByPath(path);
            try
            {
                GravityPrototype.LevelMenuUI menu=null;
                foreach(var root in scene.GetRootGameObjects())
                    if((menu=root.GetComponentInChildren<GravityPrototype.LevelMenuUI>(true))!=null)break;
                Assert.That(menu,Is.Not.Null);
                var catalog=(LevelCatalog)typeof(GravityPrototype.LevelMenuUI).GetField("catalog",
                    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(menu);
                Assert.That(catalog,Is.Not.Null);
                Assert.That(catalog.Count,Is.GreaterThan(0));
                yield return null;
                Canvas canvas=null;
                foreach(var root in scene.GetRootGameObjects())
                    if((canvas=root.GetComponentInChildren<Canvas>(true))!=null)break;
                Assert.That(canvas,Is.Not.Null);
                var rootUi=canvas.transform.Find("Level Select UI");
                Assert.That(rootUi,Is.Not.Null);
                var settings=rootUi.GetComponentInChildren<GravityPrototype.UI.SettingsPopupView>(true);
                Assert.That(settings,Is.Not.Null,"开始界面要有设置弹窗");
                var buttons=rootUi.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                // 设计文档 2.8.2.1：开始 / 继续 / 设置 / 退出游戏
                foreach(var required in new[]{"Start Button","Continue Button","Settings Button","Quit Button"})
                    Assert.That(System.Array.Exists(buttons,b=>b.name==required),Is.True,required+" missing");
                Assert.That(System.Array.Exists(buttons,b=>b.name=="Records Button"),Is.False,"开始界面不再放记录入口");
                foreach(var b in buttons)if(b.name=="Settings Button")b.onClick.Invoke();
                Assert.That(settings.IsVisible,Is.True);
                foreach(var slider in settings.GetComponentsInChildren<UnityEngine.UI.Slider>(true))
                    Assert.That(slider.handleRect.rect.height,Is.LessThanOrEqualTo(48f),
                        "Slider handle must not stretch into the adjacent control.");
                // 设置：窗口大小 3 档、音量 5 档
                var resolutionSlider=System.Array.Find(settings.GetComponentsInChildren<UnityEngine.UI.Slider>(true),s=>s.name=="Resolution Slider");
                Assert.That(resolutionSlider,Is.Not.Null,"缺少窗口大小调节条");
                Assert.That(resolutionSlider.maxValue,Is.EqualTo(GravityPrototype.Progress.GameSettingsStore.Resolutions.Length-1));
                foreach(var b in settings.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if(b.name=="Close Button")b.onClick.Invoke();
                Assert.That(settings.IsVisible,Is.False);
                var continueButton=System.Array.Find(buttons,b=>b.name=="Continue Button");
                bool expected=SaveGameStore.TryLoad(out var save)&&catalog.TryGetIndex(save.levelId,out int index)
                    &&SceneLoadUtility.CanLoadScene(catalog.GetLevel(index).SceneName);
                Assert.That(continueButton.interactable,Is.EqualTo(expected));
                Assert.That(Time.timeScale,Is.EqualTo(1f));
            }
            finally { if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene); }
        }

        [UnityTest] public IEnumerator TimerPauseResume_DoesNotCountMenuTime()
        {
            var go=new GameObject("Timer test");var t=go.AddComponent<LevelTimer>();
            yield return null;t.ResumeRunAt(12f);t.Pause();float before=t.ElapsedSeconds;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(t.ElapsedSeconds,Is.EqualTo(before).Within(.001f));
            t.Resume();yield return new WaitForSecondsRealtime(.06f);
            Assert.That(t.ElapsedSeconds,Is.GreaterThan(before));
            Object.DestroyImmediate(go);
        }
        [Test] public void SaveValidation_RejectsNonFiniteElapsed()
        {
            Assert.That(SaveGameData.Create(0,"Level01",5f,0).IsUsable,Is.True);
            var d=new SaveGameData{levelId="Level01",elapsedSeconds=float.PositiveInfinity};
            Assert.That(d.IsUsable,Is.False);
        }
        [UnityTest] public IEnumerator Gate_AutoResolveNeverSelectsPlayerAndRespawnsBall()
        {
            var scene=SceneManager.CreateScene("GateTest");
            var playerGo=new GameObject("Player decoy");SceneManager.MoveGameObjectToScene(playerGo,scene);
            playerGo.AddComponent<Rigidbody2D>().gravityScale=0f;playerGo.AddComponent<GravityBody2D>();playerGo.AddComponent<PlayerController2D>();
            var ballGo=new GameObject("Actual ball");ballGo.SetActive(false);SceneManager.MoveGameObjectToScene(ballGo,scene);
            ballGo.AddComponent<CircleCollider2D>();var body=ballGo.AddComponent<Rigidbody2D>();body.gravityScale=0f;
            ballGo.AddComponent<BallMaterial2D>();ballGo.transform.position=new Vector3(20,0,0);ballGo.SetActive(true);
            var gateGo=new GameObject("Gate");gateGo.SetActive(false);SceneManager.MoveGameObjectToScene(gateGo,scene);
            gateGo.transform.position=new Vector3(20,0,0);gateGo.AddComponent<BoxCollider2D>().isTrigger=true;
            var gate=gateGo.AddComponent<AnomalyGate2D>();gateGo.SetActive(true);
            try
            {
                Assert.That(gate.Ball,Is.SameAs(body));
                int respawns=0;gate.BallRespawned+=(_,__)=>respawns++;
                for(int i=0;i<35&&respawns==0;i++)yield return new WaitForFixedUpdate();
                Assert.That(respawns,Is.GreaterThan(0));
                Assert.That(body.bodyType,Is.EqualTo(RigidbodyType2D.Dynamic));
                Assert.That(Vector2.Distance(body.position,new Vector2(20,0)),Is.LessThan(.1f));
            }
            finally
            {
                Object.DestroyImmediate(gateGo);Object.DestroyImmediate(ballGo);Object.DestroyImmediate(playerGo);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
        [UnityTest] public IEnumerator PlatformFlow_CreatesPixelUiAndNestedPauseRestoresInput()
        {
            const string path="Assets/GravityPrototype/Scenes/PlatformLevel.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
            var scene=SceneManager.GetSceneByPath(path);
            LevelController level=null;
            foreach(var root in scene.GetRootGameObjects())if(root.name=="Level")level=root.GetComponent<LevelController>();
            try
            {
                Assert.That(level.GetComponent<LevelFlowController>(),Is.Not.Null);
                var catalog=(LevelCatalog)typeof(LevelFlowController).GetField("catalog",
                    System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                    .GetValue(level.GetComponent<LevelFlowController>());
                Assert.That(catalog,Is.Not.Null,"Platform scene must retain its catalog reference.");
                Assert.That(catalog.TryGetIndex(level.LevelId,out _),Is.True);
                var pause=level.GetComponent<GamePauseController>();pause.Initialize(level);
                pause.SetPaused(PauseReason.SideMenu,true);pause.SetPaused(PauseReason.SettingsPopup,true);
                Assert.That(Time.timeScale,Is.Zero);
                Assert.That(level.PlayerMovement.InputBlocked,Is.True);
                Assert.That(level.PlayerMovement.GetComponent<AstronautToolController>().InputBlocked,Is.True);
                float elapsed=level.Timer.ElapsedSeconds;
                yield return new WaitForSecondsRealtime(.06f);
                Assert.That(level.Timer.ElapsedSeconds,Is.EqualTo(elapsed).Within(.001f));
                pause.SetPaused(PauseReason.SettingsPopup,false);Assert.That(pause.IsPaused,Is.True);
                pause.ClearAll();Assert.That(Time.timeScale,Is.EqualTo(1f));
                Assert.That(level.PlayerMovement.InputBlocked,Is.False);
            }
            finally
            {
                Time.timeScale=1f;
                if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
            }
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach(var root in scene.GetRootGameObjects())
            {
                var found=root.GetComponentInChildren<T>(true);
                if(found!=null)return found;
            }
            return null;
        }

        // 2.8.2.1 设置：窗口大小 3 档、音量 5 档
        [Test] public void Settings_HaveThreeResolutionsAndFiveVolumeSteps()
        {
            Assert.That(GameSettingsStore.Resolutions.Length,Is.EqualTo(3));
            Assert.That(GameSettingsStore.VolumeSteps.Length,Is.EqualTo(5));
            int before=GameSettingsStore.MasterStep;
            try
            {
                GameSettingsStore.MasterVolume=0.6f;
                Assert.That(GameSettingsStore.MasterStep,Is.EqualTo(2),"0.6 应吸附到 50% 档");
                Assert.That(GameSettingsStore.MasterVolume,Is.EqualTo(0.5f).Within(0.001f));
                GameSettingsStore.EffectStep=99;
                Assert.That(GameSettingsStore.EffectStep,Is.EqualTo(4),"档位封顶在 4");
            }
            finally { GameSettingsStore.MasterStep=before; }
        }

        // 2.3.2 虚空死亡 + 2.8.2.3 失败弹窗
        [UnityTest] public IEnumerator VoidDeath_ShowsDefeatPopupAndRetryUnfreezes()
        {
            const string path="Assets/GravityPrototype/Scenes/PlatformLevel.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
            var scene=SceneManager.GetSceneByPath(path);
            try
            {
                var level=FindInScene<LevelController>(scene);
                var popup=FindInScene<GravityPrototype.UI.DefeatPopupView>(scene);
                Assert.That(level,Is.Not.Null);
                Assert.That(popup,Is.Not.Null,"关卡界面应带失败弹窗");
                yield return null;
                Assert.That(level.AutoRestartOnDeath,Is.False,"流程层接管后不应自动重开");
                level.PlayerMovement.transform.position=new Vector3(600f,600f,0f);
                for(int i=0;i<20&&!level.IsDeathPending;i++)yield return null;
                Assert.That(level.IsDeathPending,Is.True,"视野外应判定虚空死亡");
                yield return null;
                Assert.That(popup.IsVisible,Is.True,"死亡后应弹失败弹窗");
                Assert.That(Time.timeScale,Is.EqualTo(0f).Within(0.001f));
                foreach(var b in popup.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if(b.name=="Retry Button")b.onClick.Invoke();
                yield return null;
                Assert.That(Time.timeScale,Is.EqualTo(1f).Within(0.001f),"再来一次应解除冻结");
            }
            finally
            {
                Time.timeScale=1f;
                if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
