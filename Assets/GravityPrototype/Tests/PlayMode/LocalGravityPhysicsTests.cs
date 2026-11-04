using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GravityPrototype.Tests
{
    public sealed class LocalGravityPhysicsTests
    {
        private const float StepDuration = 0.02f;
        private Scene testScene;
        private PhysicsScene2D physicsScene;
        private readonly List<GameObject> objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            testScene = SceneManager.CreateScene("LocalGravityTest_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physicsScene = testScene.GetPhysicsScene2D();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in objects)
            {
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }

            objects.Clear();
            if (testScene.IsValid() && testScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(testScene);
            }
        }

        [Test]
        public void OutsideFrame_UsesAuthoredWorldGravity()
        {
            Rigidbody2D body = CreateBody(Vector2.zero);
            body.gravityScale = 1.5f;
            Step(body, 5);
            AssertVector(body.linearVelocity, Physics2D.gravity * 1.5f * StepDuration * 5);
            Assert.That(body.GetComponent<GravityBody2D>().CurrentRegion, Is.Null);
        }

        [Test]
        public void InsideFrame_ReplacesGravityRatherThanAddingIt()
        {
            GravityRegion2D region = CreateRegion(Vector2.zero);
            Rigidbody2D body = CreateBody(Vector2.zero);
            Step(body, 5);
            AssertVector(body.linearVelocity, Vector2.up * PhysicsEnvironment.Gravity * StepDuration * 5);
            Assert.That(body.GetComponent<GravityBody2D>().CurrentRegion, Is.SameAs(region));
            Assert.That(body.gravityScale, Is.EqualTo(1f));
        }

        [Test]
        public void DifferentMasses_HaveEqualGravityAcceleration()
        {
            CreateRegion(Vector2.zero);
            Rigidbody2D light = CreateBody(Vector2.left * 0.8f);
            Rigidbody2D heavy = CreateBody(Vector2.right * 0.8f);
            light.mass = 0.5f;
            heavy.mass = 8f;
            Step(new[] { light, heavy }, 5);
            AssertVector(light.linearVelocity, heavy.linearVelocity);
        }

        [Test]
        public void LeavingFrame_PreservesMomentumAndRestoresWorldGravity()
        {
            GravityRegion2D region = CreateRegion(Vector2.zero);
            Rigidbody2D body = CreateBody(Vector2.zero);
            body.linearVelocity = new Vector2(2f, 3f);
            Step(body, 1);
            Vector2 previousVelocity = body.linearVelocity;
            region.transform.position = new Vector3(100f, 100f, 0f);
            Step(body, 1);
            AssertVector(body.linearVelocity, previousVelocity + Physics2D.gravity * StepDuration);
            Assert.That(body.GetComponent<GravityBody2D>().CurrentRegion, Is.Null);
        }

        [Test]
        public void MovingFrameOntoSleepingBody_WakesIt()
        {
            GravityRegion2D region = CreateRegion(Vector2.one * 100f);
            Rigidbody2D body = CreateBody(Vector2.zero);
            body.Sleep();
            Assert.That(body.IsSleeping(), Is.True);
            region.transform.position = Vector3.zero;
            Step(body, 1);
            Assert.That(body.IsAwake(), Is.True);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }

        [Test]
        public void DisablingFrame_RestoresWorldGravityWithoutStaleMembership()
        {
            GravityRegion2D region = CreateRegion(Vector2.zero);
            Rigidbody2D body = CreateBody(Vector2.zero);
            Step(body, 1);
            Vector2 previousVelocity = body.linearVelocity;
            region.enabled = false;
            Step(body, 1);
            AssertVector(body.linearVelocity, previousVelocity + Physics2D.gravity * StepDuration);
            Assert.That(body.GetComponent<GravityBody2D>().CurrentRegion, Is.Null);
        }

        [Test]
        public void FrameBoundary_UsesCenterAndIncludesBoundary()
        {
            GravityRegion2D region = CreateRegion(Vector2.zero);
            Assert.That(region.Contains(new Vector2(2f, 0f)), Is.True);
            Assert.That(region.Contains(new Vector2(2.001f, 0f)), Is.False);
            Rigidbody2D body = CreateBody(new Vector2(2.1f, 0f));
            Step(body, 1);
            Assert.That(body.GetComponent<GravityBody2D>().CurrentRegion, Is.Null,
                "The collider touching the frame is not enough; its center must be inside.");
        }

        [Test]
        public void MovingFrameAwayAndBack_UpdatesMembershipAtNextPhysicsStep()
        {
            GravityRegion2D region = CreateRegion(Vector2.one * 100f);
            Rigidbody2D body = CreateBody(Vector2.zero);
            Step(body, 1);
            region.transform.position = Vector3.zero;
            Step(body, 1);
            Assert.That(body.GetComponent<GravityBody2D>().CurrentRegion, Is.SameAs(region));
            region.transform.position = Vector2.one * 100f;
            Step(body, 1);
            Assert.That(body.GetComponent<GravityBody2D>().CurrentRegion, Is.Null);
        }

        [Test]
        public void HorizontalAndZeroGravity_AreSupported()
        {
            GravityRegion2D region = CreateRegion(Vector2.zero);
            SetField(region, "direction", Vector2.right);
            Rigidbody2D body = CreateBody(Vector2.zero);
            Step(body, 1);
            AssertVector(body.linearVelocity, Vector2.right * PhysicsEnvironment.Gravity * StepDuration);
            Vector2 previousVelocity = body.linearVelocity;
            SetField(region, "direction", Vector2.zero);
            Step(body, 1);
            AssertVector(body.linearVelocity, previousVelocity);
        }

        [Test]
        public void OverlappingFrames_UsePriorityWithoutStackingForces()
        {
            GravityRegion2D primary = CreateRegion(Vector2.zero);
            SetField(primary, "priority", 10);
            GravityRegion2D secondary = CreateRegion(Vector2.zero);
            SetField(secondary, "direction", Vector2.right);
            Rigidbody2D body = CreateBody(Vector2.zero);
            Step(body, 1);
            Assert.That(body.GetComponent<GravityBody2D>().CurrentRegion, Is.SameAs(primary));
            AssertVector(body.linearVelocity, Vector2.up * PhysicsEnvironment.Gravity * StepDuration);
        }

        [Test]
        public void BodyWithoutGravityComponent_IsUnaffectedByFrame()
        {
            CreateRegion(Vector2.zero);
            Rigidbody2D body = CreateBody(Vector2.zero, false);
            Physics2D.SyncTransforms();
            physicsScene.Simulate(StepDuration);
            AssertVector(body.linearVelocity, Physics2D.gravity * StepDuration);
        }

        [Test]
        public void BodyWithZeroGravityScale_RemainsWeightlessInsideAndOutside()
        {
            GravityRegion2D region = CreateRegion(Vector2.zero);
            Rigidbody2D body = CreateBody(Vector2.zero);
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.right;
            Step(body, 1);
            AssertVector(body.linearVelocity, Vector2.right);
            region.enabled = false;
            Step(body, 1);
            AssertVector(body.linearVelocity, Vector2.right);
        }

        [Test]
        public void NativeCollision_KeepsBallAboveFloorAndAllowsRest()
        {
            GameObject floor = CreateObject("Floor");
            floor.transform.position = new Vector3(0f, -1f, 0f);
            BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(20f, 1f);
            Rigidbody2D body = CreateBody(Vector2.up * 2f);
            Step(body, 240);
            Assert.That(body.position.y, Is.GreaterThan(-0.07f), "Ball must not pass through the floor.");
            Assert.That(Mathf.Abs(body.linearVelocity.y), Is.LessThan(0.05f));
        }

        [Test]
        public void LocalGravity_PressesBallAgainstCeilingWithoutTunneling()
        {
            CreateRegion(Vector2.zero);
            GameObject ceiling = CreateObject("Ceiling");
            ceiling.transform.position = new Vector3(0f, 1.6f, 0f);
            BoxCollider2D collider = ceiling.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(10f, 0.4f);
            Rigidbody2D body = CreateBody(Vector2.zero);
            Step(body, 240);
            Assert.That(body.position.y, Is.InRange(0.85f, 1.05f));
            Assert.That(Mathf.Abs(body.linearVelocity.y), Is.LessThan(0.05f));
        }

        [Test]
        public void DisablingGravityComponent_RestoresNativeBehavior()
        {
            CreateRegion(Vector2.zero);
            Rigidbody2D body = CreateBody(Vector2.zero);
            Step(body, 1);
            Vector2 previousVelocity = body.linearVelocity;
            body.GetComponent<GravityBody2D>().enabled = false;
            physicsScene.Simulate(StepDuration);
            AssertVector(body.linearVelocity, previousVelocity + Physics2D.gravity * StepDuration);
            Assert.That(body.gravityScale, Is.EqualTo(1f));
        }

        private GameObject CreateObject(string name)
        {
            GameObject result = new GameObject(name);
            SceneManager.MoveGameObjectToScene(result, testScene);
            objects.Add(result);
            return result;
        }

        private GravityRegion2D CreateRegion(Vector2 position)
        {
            GameObject frame = CreateObject("Frame");
            frame.transform.position = position;
            return frame.AddComponent<GravityRegion2D>();
        }

        private Rigidbody2D CreateBody(Vector2 position, bool participates = true)
        {
            GameObject item = CreateObject("Ball");
            item.transform.position = position;
            CircleCollider2D collider = item.AddComponent<CircleCollider2D>();
            collider.radius = 0.45f;
            Rigidbody2D body = item.AddComponent<Rigidbody2D>();
            body.linearDamping = 0f;
            body.gravityScale = 1f;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (participates)
            {
                item.AddComponent<GravityBody2D>();
            }

            return body;
        }

        private void Step(Rigidbody2D body, int count)
        {
            Step(new[] { body }, count);
        }

        private void Step(Rigidbody2D[] bodies, int count)
        {
            Physics2D.SyncTransforms();
            for (int i = 0; i < count; i++)
            {
                // A local physics scene is stepped deterministically without advancing the global scene.
                foreach (Rigidbody2D body in bodies)
                {
                    body.GetComponent<GravityBody2D>().SendMessage("FixedUpdate");
                }

                Assert.That(physicsScene.Simulate(StepDuration), Is.True);
            }
        }

        private static void SetField(GravityRegion2D target, string fieldName, object value)
        {
            FieldInfo field = typeof(GravityRegion2D).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.002f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.002f));
        }
    }
}
