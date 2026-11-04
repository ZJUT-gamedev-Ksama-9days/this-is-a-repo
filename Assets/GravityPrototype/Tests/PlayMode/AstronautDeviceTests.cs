using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GravityPrototype.Tests
{
    public sealed class AstronautDeviceTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene scene;
        private AstronautToolController tools;
        private MechanicalArm2D arm;
        private PlayerController2D player;
        private Rigidbody2D owner;
        private Rigidbody2D ball;
        private GravityRegion2D region;
        private GravityFrameDrag2D drag;
        private PhysicsSandboxController controls;
        private GameObject blocker;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            const string path = "Assets/GravityPrototype/Scenes/PhysicsSandbox.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(path);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Player") player = root.GetComponent<PlayerController2D>();
                if (root.name == "Ball") ball = root.GetComponent<Rigidbody2D>();
                if (root.name == "Gravity Frame") { region = root.GetComponent<GravityRegion2D>(); drag = root.GetComponent<GravityFrameDrag2D>(); }
                if (root.name == "Sandbox Controls") controls = root.GetComponent<PhysicsSandboxController>();
            }
            tools = player.GetComponent<AstronautToolController>();
            arm = player.GetComponent<MechanicalArm2D>();
            owner = player.GetComponent<Rigidbody2D>();
            Assert.That(tools, Is.Not.Null);
            Assert.That(arm, Is.Not.Null);
            Set(player, "readKeyboardInput", false);
            Set(tools, "readKeyboardInput", false);
            Set(arm, "readMouseInput", false);
            controls.ResetSimulation();
            region.transform.position = Vector3.one * 100f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (blocker != null) Object.DestroyImmediate(blocker);
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Test]
        public void DeviceSelection_IsMutuallyExclusiveAndWraps()
        {
            Assert.That(tools.Equipped, Is.EqualTo(AstronautTool.GravityDevice));
            Assert.That(drag.CanDrag, Is.True);
            Assert.That(arm.CanUse, Is.False);
            tools.Cycle(1);
            Assert.That(tools.Equipped, Is.EqualTo(AstronautTool.MechanicalArm));
            Assert.That(drag.CanDrag, Is.False);
            Assert.That(arm.CanUse, Is.True);
            tools.Cycle(1);
            Assert.That(tools.Equipped, Is.EqualTo(AstronautTool.EmptyHands));
            Assert.That(arm.CanUse, Is.False);
            tools.Cycle(1);
            Assert.That(tools.Equipped, Is.EqualTo(AstronautTool.GravityDevice));
            tools.Cycle(-1);
            Assert.That(tools.Equipped, Is.EqualTo(AstronautTool.EmptyHands));
        }

        [UnityTest]
        public IEnumerator Stow_HoldsFor1Point5SecondsThenFadesToZeroAt3()
        {
            tools.Equip(AstronautTool.MechanicalArm);
            Set(tools, "stowedAt", Time.time - 1.49f);
            Invoke(tools, "Update");
            Assert.That(region.Influence, Is.EqualTo(1f));
            Set(tools, "stowedAt", Time.time - 2.25f);
            Invoke(tools, "Update");
            Assert.That(region.Influence, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(tools.FieldVisibility, Is.EqualTo(0.5f).Within(0.01f));
            tools.Equip(AstronautTool.EmptyHands);
            Assert.That(region.Influence, Is.EqualTo(0.5f).Within(0.01f), "Non-gravity switching must not extend the timer.");
            Set(tools, "stowedAt", Time.time - 3.01f);
            Invoke(tools, "Update");
            Assert.That(region.Influence, Is.Zero);
            tools.Equip(AstronautTool.GravityDevice);
            Assert.That(region.Influence, Is.EqualTo(1f));
            Assert.That(drag.CanDrag, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fade_InterpolatesToWorldGravityAndStopsInfluence()
        {
            PlaceBodies(new Vector2(-5f, 0f), new Vector2(-3.5f, 0f));
            player.enabled = false;
            ball.gravityScale = 1f;
            region.transform.position = ball.position;
            tools.Equip(AstronautTool.MechanicalArm);
            Set(tools, "stowedAt", Time.time - 2.25f);
            Invoke(tools, "Update");
            ball.GetComponent<GravityBody2D>().SendMessage("FixedUpdate");
            Assert.That(ball.GetComponent<GravityBody2D>().CurrentAcceleration.y, Is.EqualTo(0f).Within(0.1f));
            Set(tools, "stowedAt", Time.time - 3.1f);
            Invoke(tools, "Update");
            ball.GetComponent<GravityBody2D>().SendMessage("FixedUpdate");
            Assert.That(ball.GetComponent<GravityBody2D>().CurrentRegion, Is.Null);
            Assert.That(ball.GetComponent<GravityBody2D>().CurrentAcceleration, Is.EqualTo(Physics2D.gravity));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArmCannotWorkWithoutEquippingAndCannotReachThroughWalls()
        {
            PlaceBodies(new Vector2(-5f, 0f), new Vector2(-3.5f, 0f));
            region.transform.position = ball.position;
            Physics2D.SyncTransforms();
            arm.SetAim(ball.position);
            Assert.That(arm.TryGrab(), Is.False);
            tools.Equip(AstronautTool.MechanicalArm);
            Assert.That(arm.TryGrab(), Is.True);
            arm.Release();
            blocker = new GameObject("Test occluder");
            SceneManager.MoveGameObjectToScene(blocker, scene);
            blocker.transform.position = new Vector2(-4.25f, 0f);
            blocker.AddComponent<BoxCollider2D>().size = new Vector2(0.15f, 2f);
            Physics2D.SyncTransforms();
            Assert.That(arm.TryGrab(), Is.False);
            Vector2 before = ball.position;
            arm.SetPushing(true);
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            Assert.That(ball.position.x, Is.EqualTo(before.x).Within(0.02f));
            Object.DestroyImmediate(blocker);
            blocker = null;
            ball.position = new Vector2(-1f, 0f);
            Physics2D.SyncTransforms();
            arm.SetAim(ball.position);
            Assert.That(arm.TryGrab(), Is.False, "Out-of-reach objects must not be grabbed.");
        }

        [UnityTest]
        public IEnumerator Push_AppliesContinuousForceAndOppositeReaction()
        {
            PlaceBodies(new Vector2(-5f, 0f), new Vector2(-3.5f, 0f));
            tools.Equip(AstronautTool.MechanicalArm);
            arm.SetAim(ball.position);
            arm.SetPushing(true);
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            Assert.That(ball.linearVelocity.x, Is.GreaterThan(0.5f));
            Assert.That(owner.linearVelocity.x, Is.LessThan(0f));
            Assert.That(ball.position.x, Is.GreaterThan(-3.5f));
            tools.Equip(AstronautTool.EmptyHands);
            Assert.That(arm.GrabbedBody, Is.Null);
        }

        [UnityTest]
        public IEnumerator Grab_FollowsAimByForceAndReleasesOnSwitchDeathOrOcclusion()
        {
            PlaceBodies(new Vector2(-5f, 0f), new Vector2(-3.5f, 0f));
            region.transform.position = Vector3.one * 100f;
            Physics2D.SyncTransforms();
            tools.Equip(AstronautTool.MechanicalArm);
            arm.SetAim(ball.position);
            Assert.That(arm.TryGrab(), Is.False, "A body outside gravity must not be draggable.");
            region.transform.position = ball.position;
            Physics2D.SyncTransforms();
            Assert.That(arm.TryGrab(), Is.True, "A body in gravity may be dragged.");
            arm.SetAim(new Vector2(-3.3f, 0.5f));
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            Assert.That(ball.position.y, Is.GreaterThan(0.01f));
            region.transform.position = Vector3.one * 100f;
            Physics2D.SyncTransforms();
            Invoke(arm, "FixedUpdate");
            Assert.That(arm.GrabbedBody, Is.Null, "Leaving the gravity field during a drag must release the target.");
            region.transform.position = ball.position;
            Physics2D.SyncTransforms();
            tools.Equip(AstronautTool.GravityDevice);
            Assert.That(arm.GrabbedBody, Is.Null);
            tools.Equip(AstronautTool.MechanicalArm);
            arm.SetAim(ball.position);
            Assert.That(arm.TryGrab(), Is.True);
            player.GetComponent<PlayerHealth>().TakeDamage(100f);
            yield return null;
            Assert.That(arm.GrabbedBody, Is.Null);
            Assert.That(arm.CanUse, Is.False);
            controls.ResetSimulation();
            Assert.That(tools.Equipped, Is.EqualTo(AstronautTool.GravityDevice));
            Assert.That(player.GetComponent<PlayerHealth>().CurrentHealth, Is.EqualTo(100f));
            Assert.That(region.Influence, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator Grab_ReleasesIfWallAppearsOrDistanceBecomesTooLarge()
        {
            PlaceBodies(new Vector2(-5f, 0f), new Vector2(-3.5f, 0f));
            region.transform.position = ball.position;
            Physics2D.SyncTransforms();
            tools.Equip(AstronautTool.MechanicalArm);
            arm.SetAim(ball.position);
            Assert.That(arm.TryGrab(), Is.True);
            blocker = new GameObject("New wall");
            SceneManager.MoveGameObjectToScene(blocker, scene);
            blocker.transform.position = new Vector2(-4.25f, 0f);
            blocker.AddComponent<BoxCollider2D>().size = new Vector2(0.15f, 2f);
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(arm.GrabbedBody, Is.Null);
            Object.DestroyImmediate(blocker);
            blocker = null;
            arm.SetAim(ball.position);
            Assert.That(arm.TryGrab(), Is.True);
            ball.position = Vector2.one * 10f;
            Physics2D.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(arm.GrabbedBody, Is.Null);
        }

        [UnityTest]
        public IEnumerator BodyContact_CapsSustainedDriveWithoutDisablingCollision()
        {
            PlaceBodies(new Vector2(-5f, 0f), new Vector2(-4.2f, 0f));
            ball.constraints = RigidbodyConstraints2D.FreezeAll;
            for (int i = 0; i < 2; i++) yield return new WaitForFixedUpdate();
            Assert.That(owner.GetComponent<Collider2D>().IsTouching(ball.GetComponent<Collider2D>()), Is.True);
            owner.linearVelocity = Vector2.zero;
            owner.totalForce = Vector2.zero;
            player.SetInput(1f, false);
            Invoke(player, "FixedUpdate");
            Assert.That(owner.totalForce.x, Is.EqualTo(0.5f).Within(0.01f),
                "Continuous body drive into a movable body is capped at 0.5 N; use the arm for moving objects.");
            Assert.That(owner.GetComponent<Collider2D>().enabled, Is.True);
            Assert.That(ball.GetComponent<Collider2D>().enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator ArmLosingFocusOrDisabling_ReleasesAndBlocksFurtherForce()
        {
            PlaceBodies(new Vector2(-5f, 0f), new Vector2(-3.5f, 0f));
            region.transform.position = ball.position;
            Physics2D.SyncTransforms();
            tools.Equip(AstronautTool.MechanicalArm);
            arm.SetAim(ball.position);
            Assert.That(arm.TryGrab(), Is.True);
            typeof(MechanicalArm2D).GetMethod("OnApplicationFocus", PrivateInstance).Invoke(arm, new object[] { false });
            Assert.That(arm.GrabbedBody, Is.Null);
            Assert.That(arm.CanUse, Is.False);
            arm.SetPushing(true);
            Assert.That(arm.TryGrab(), Is.False);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(ball.linearVelocity.sqrMagnitude, Is.LessThan(0.001f));
            typeof(MechanicalArm2D).GetMethod("OnApplicationFocus", PrivateInstance).Invoke(arm, new object[] { true });
            Assert.That(arm.TryGrab(), Is.True);
            arm.enabled = false;
            Assert.That(arm.GrabbedBody, Is.Null);
            Assert.That(arm.TryGrab(), Is.False);
        }

        [UnityTest]
        public IEnumerator DeathWhileStowed_DoesNotRestartFieldFade()
        {
            tools.Equip(AstronautTool.EmptyHands);
            Set(tools, "stowedAt", Time.time - 2.25f);
            player.GetComponent<PlayerHealth>().TakeDamage(100f);
            Invoke(tools, "Update");
            Assert.That(region.Influence, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(drag.CanDrag, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Stow_ActualPlayerLoopCompletesFadeAndHidesRenderers()
        {
            tools.Equip(AstronautTool.EmptyHands);
            yield return new WaitForSeconds(1.2f);
            Assert.That(region.Influence, Is.EqualTo(1f));
            yield return new WaitForSeconds(0.8f);
            Assert.That(region.Influence, Is.InRange(0.1f, 0.9f));
            yield return new WaitForSeconds(1.1f);
            Assert.That(region.Influence, Is.Zero);
            foreach (Renderer renderer in region.GetComponentsInChildren<Renderer>())
                Assert.That(renderer.enabled, Is.False, renderer.name);
            tools.Equip(AstronautTool.GravityDevice);
            yield return null;
            foreach (Renderer renderer in region.GetComponentsInChildren<Renderer>())
                Assert.That(renderer.enabled, Is.True, renderer.name);
        }

        [UnityTest]
        public IEnumerator Grab_AllThreeMaterialsStayStableInActiveField()
        {
            // Isolate spring stability from the separately tested three-second field lifetime.
            Set(tools, "fieldHoldSeconds", 30f);
            for (int material = 0; material < 3; material++)
            {
                controls.SelectBallType(material);
                PlaceBodies(new Vector2(-5f, 0f), new Vector2(-3.5f, 0f));
                region.transform.position = ball.position;
                region.transform.SetPositionAndRotation(ball.position, Quaternion.identity);
                region.SetInfluence(1f);
                // Refresh the lifetime once for each material instead of sharing the 9-second loop timer.
                tools.Equip(AstronautTool.GravityDevice);
                owner.constraints = RigidbodyConstraints2D.FreezeAll;
                Physics2D.SyncTransforms();
                tools.Equip(AstronautTool.MechanicalArm);
                arm.SetAim(ball.position);
                Assert.That(arm.TryGrab(), Is.True, $"Material {material} should be grabbed inside the field.");
                arm.SetAim(new Vector2(-3.5f, 0.5f));
                for (int i = 0; i < 150; i++)
                {
                    // The Editor may lose OS focus during automated runs; focus release has its own test.
                    typeof(MechanicalArm2D).GetMethod("OnApplicationFocus", PrivateInstance).Invoke(arm, new object[] { true });
                    yield return new WaitForFixedUpdate();
                }
                Assert.That(arm.GrabbedBody, Is.SameAs(ball), $"material={material}, canUse={arm.CanUse}, influence={region.Influence}, contains={region.Contains(ball.worldCenterOfMass)}, pos={ball.position}, owner={owner.position}, dead={player.GetComponent<PlayerHealth>().IsDead}, equipped={tools.Equipped}, focus={typeof(MechanicalArm2D).GetField("hasFocus", PrivateInstance).GetValue(arm)}");
                Assert.That(ball.linearVelocity.magnitude, Is.LessThan(2f));
                Assert.That(ball.position.y, Is.GreaterThan(0.1f));
                arm.Release();
            }
        }

        private void PlaceBodies(Vector2 playerPosition, Vector2 ballPosition)
        {
            region.SetInfluence(1f);
            player.enabled = false;
            owner.position = playerPosition;
            owner.transform.SetPositionAndRotation(playerPosition, Quaternion.identity);
            owner.gravityScale = 0f;
            owner.linearVelocity = Vector2.zero;
            ball.position = ballPosition;
            ball.transform.SetPositionAndRotation(ballPosition, Quaternion.identity);
            ball.angularVelocity = 0f;
            ball.gravityScale = 0f;
            ball.linearVelocity = Vector2.zero;
            ball.GetComponent<BallMaterial2D>().enabled = false;
            Physics2D.SyncTransforms();
        }

        private static void Set(object target, string field, object value) => target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        private static void Invoke(object target, string method) => target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);
    }
}
