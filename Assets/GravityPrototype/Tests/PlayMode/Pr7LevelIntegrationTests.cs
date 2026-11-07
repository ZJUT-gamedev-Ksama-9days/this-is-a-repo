using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GravityPrototype.GameFlow;
using GravityPrototype.Progress;
using GravityPrototype.UI;
namespace GravityPrototype.Tests
{
    public sealed class Pr7LevelIntegrationTests
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        [UnityTest] public IEnumerator FiveLevels_LoadWithValidReferencesAndResetMechanisms()
        {
            for(int i=1;i<=5;i++)
            {
                string path="Assets/GravityPrototype/Scenes/PR7/GridLevel"+i.ToString("00")+".unity";
                yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
                var scene=SceneManager.GetSceneByPath(path);
                try
                {
                    LevelController level=null;
                    foreach(var root in scene.GetRootGameObjects())if(root.name=="Level")level=root.GetComponent<LevelController>();
                    Assert.That(level,Is.Not.Null,path);
                    Assert.That(level.LevelId,Is.EqualTo("GridLevel"+i.ToString("00")));
                    var flow=level.GetComponent<LevelFlowController>();
                    var catalog=(LevelCatalog)typeof(LevelFlowController).GetField("catalog",Private).GetValue(flow);
                    Assert.That(catalog,Is.Not.Null);Assert.That(catalog.Count,Is.EqualTo(6));
                    Assert.That(flow.CurrentLevelIndex,Is.EqualTo(i));
                    foreach(var root in scene.GetRootGameObjects())
                    {
                        foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))Assert.That(component,Is.Not.Null,"Missing script");
                        foreach(var map in root.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true))
                            Assert.That(map.name,Does.StartWith("level "+i));
                    }
                    level.PlayerMovement.InputBlocked=false;
                    typeof(PlayerController2D).GetField("readKeyboardInput",Private).SetValue(level.PlayerMovement,false);
                    for(int k=0;k<12;k++)yield return new WaitForFixedUpdate();
                    Assert.That(level.Player.IsDead,Is.False,"Spawn must be safe");
                    Assert.That(level.PlayerMovement.IsGrounded,Is.True,"Spawn must have support");
                    CircuitSwitch2D circuit=null;LeverMechanism2D lever=null;
                    foreach(var root in scene.GetRootGameObjects())
                    {
                        if(circuit==null)circuit=root.GetComponentInChildren<CircuitSwitch2D>();
                        if(lever==null)lever=root.GetComponentInChildren<LeverMechanism2D>();
                    }
                    Assert.That(circuit,Is.Not.Null);circuit.PowerOn();Assert.That(circuit.IsPowered,Is.True);
                    var doors=(Collider2D[])typeof(CircuitSwitch2D).GetField("barriers",Private).GetValue(circuit);
                    foreach(var door in doors)Assert.That(door.enabled,Is.False);
                    if(lever!=null)
                    {
                        float home=lever.Platform.eulerAngles.z;
                        lever.Trigger();for(int k=0;k<5;k++)yield return new WaitForFixedUpdate();
                        lever.Trigger();for(int k=0;k<24;k++)yield return new WaitForFixedUpdate();
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(home,lever.Platform.eulerAngles.z)),Is.LessThan(.1f),"Interrupted rotation must return to exact home");
                    }
                    level.ResetLevelInPlace();Assert.That(circuit.IsPowered,Is.False);
                    foreach(var door in doors)Assert.That(door.enabled,Is.True);
                    if(lever!=null)Assert.That(lever.TriggerCount,Is.Zero);
                    var pause=level.GetComponent<GamePauseController>();pause.SetPaused(PauseReason.PausePopup,true);
                    circuit.PowerOn();Assert.That(circuit.IsPowered,Is.False);pause.ClearAll();
                    Assert.That(SceneLoadUtility.CanLoadScene(level.gameObject.scene.name),Is.True);
                }
                finally {Time.timeScale=1f;if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);}
                yield return null;
            }
        }
    }
}
