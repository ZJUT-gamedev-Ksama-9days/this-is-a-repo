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
    }
}
