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
                    LeverMechanism2D lever=null;
                    foreach(var root in scene.GetRootGameObjects())
                    {
                        if(lever==null)lever=root.GetComponentInChildren<LeverMechanism2D>();
                    }
                    // 2.3.6.3: 电闸是装饰（没有交互、没有碰撞体），电路只由电路门接通。
                    foreach(var root in scene.GetRootGameObjects())
                        foreach(var node in root.GetComponentsInChildren<Transform>(true))
                            if(node.name.StartsWith("电闸"))
                            {
                                Assert.That(node.GetComponents<Collider2D>().Length,Is.Zero,node.name+" must have no collider");
                                Assert.That(node.GetComponent<CircuitGate2D>(),Is.Null,node.name);
                            }
                    var gates=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.SelectMany(scene.GetRootGameObjects(),r=>r.GetComponentsInChildren<CircuitGate2D>(true)));
                    var balls=System.Linq.Enumerable.ToArray(System.Linq.Enumerable.SelectMany(scene.GetRootGameObjects(),r=>r.GetComponentsInChildren<BallMaterial2D>(true)));
                    int[] ballsPerLevel={0,0,1,2,3,3};
                    LevelGoal2D goal=level.Goal;
                    Assert.That(goal,Is.Not.Null,path);
                    Assert.That(goal.IsOpen,Is.False,path);
                    // 2.5.1：每关小球数量与目标门开门方式。
                    Assert.That(balls.Length,Is.EqualTo(ballsPerLevel[i]),path+" ball count");
                    Assert.That(goal.OpensWithBall,Is.EqualTo(i>1),path+" goal mode");
                    if(i<=2)
                    {
                        // 第一、二关没有电路门，开局就通路。
                        Assert.That(gates.Length,Is.Zero,path);
                        Assert.That(level.IsCircuitConnected,Is.True,path);
                    }
                    else
                    {
                        Assert.That(gates.Length,Is.GreaterThan(0),path);
                        Assert.That(level.IsCircuitConnected,Is.False,path);
                        foreach(var gate in gates)Assert.That(gate.IsConnected,Is.False,path);
                    }
                    if(i>1)
                    {
                        // L2–L5: 只有电路接通且小球到达，目标门才打开。
                        foreach(var gate in gates)gate.Connect();
                        foreach(var gate in gates)Assert.That(gate.IsConnected,Is.True,path);
                        Assert.That(level.IsCircuitConnected,Is.True,path);
                        goal.OpenWithBall();Assert.That(goal.IsOpen,Is.True,path);
                    }
                    else
                    {
                        // L1: 常开门，小球到达不影响开门状态。
                        goal.OpenWithBall();Assert.That(goal.IsOpen,Is.False,path);
                    }
                    if(lever!=null)
                    {
                        float home=lever.Platform.eulerAngles.z;
                        // 2.3.3：横平竖直平台一次 90°，倾斜平台一次 45°。
                        float offset=Mathf.Abs(Mathf.DeltaAngle(home,Mathf.Round(home/90f)*90f));
                        Assert.That(lever.StepDegrees,Is.EqualTo(offset<1f?90f:45f),path+" lever step");
                        lever.Trigger();for(int k=0;k<5;k++)yield return new WaitForFixedUpdate();
                        lever.Trigger();for(int k=0;k<24;k++)yield return new WaitForFixedUpdate();
                        Assert.That(Mathf.Abs(Mathf.DeltaAngle(home,lever.Platform.eulerAngles.z)),Is.LessThan(.1f),"Interrupted rotation must return to exact home");
                    }
                    level.ResetLevelInPlace();
                    foreach(var gate in gates)Assert.That(gate.IsConnected,Is.False,path);
                    Assert.That(goal.IsOpen,Is.False,path);
                    if(i>2)Assert.That(level.IsCircuitConnected,Is.False,path);
                    if(lever!=null)Assert.That(lever.TriggerCount,Is.Zero);
                    // 重力框方向切换：上 → 下 → 左 → 右 → 上；只有第 4/5 关允许右键切换（文档：可变向重力框）。
                    var frame=System.Linq.Enumerable.FirstOrDefault(System.Linq.Enumerable.SelectMany(scene.GetRootGameObjects(),r=>r.GetComponentsInChildren<GravityFrameDrag2D>(true)));
                    Assert.That(frame,Is.Not.Null,path);
                    Assert.That(frame.AllowsDirectionChange,Is.EqualTo(i>=4),path+" frame mode");
                    var region=frame.GetComponent<GravityRegion2D>();
                    region.SetDirection(Vector2.up);
                    foreach(var expected in new[]{Vector2.down,Vector2.left,Vector2.right,Vector2.up})
                    {
                        frame.CycleGravityDirection();
                        Assert.That(Vector2.Dot(region.Direction,expected),Is.GreaterThan(.99f),path+" gravity direction order");
                    }
                    Assert.That(SceneLoadUtility.CanLoadScene(level.gameObject.scene.name),Is.True);
                }
                finally {Time.timeScale=1f;if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);}
                yield return null;
            }
        }
    }
}
