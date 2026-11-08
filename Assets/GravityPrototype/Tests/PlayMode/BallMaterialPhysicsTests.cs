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
    public sealed class BallMaterialPhysicsTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float StepDuration = 0.02f;
        private Scene sandbox;
        private Scene isolated;
        private PhysicsScene2D physics;
        private PhysicsSandboxController controls;
        private Rigidbody2D ball;
        private PlayerHealth health;
        private GravityRegion2D region;
        private BallPhysicsProfile[] profiles;
        private readonly List<GameObject> scratch = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            const string path = "Assets/GravityPrototype/Scenes/PhysicsSandbox.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            sandbox = SceneManager.GetSceneByPath(path);
            foreach (GameObject root in sandbox.GetRootGameObjects())
            {
                if (root.name == "Ball") ball = root.GetComponent<Rigidbody2D>();
                if (root.name == "Sandbox Controls") controls = root.GetComponent<PhysicsSandboxController>();
                if (root.name == "Player") health = root.GetComponent<PlayerHealth>();
                if (root.name == "Gravity Frame") region = root.GetComponent<GravityRegion2D>();
            }
            profiles = (BallPhysicsProfile[])typeof(PhysicsSandboxController).GetField("ballProfiles", PrivateInstance).GetValue(controls);
            Assert.That(profiles, Has.Length.EqualTo(3));
            typeof(PhysicsSandboxController).GetField("readBallSelectionInput", PrivateInstance).SetValue(controls, false);
            typeof(PlayerController2D).GetField("readKeyboardInput", PrivateInstance).SetValue(health.GetComponent<PlayerController2D>(), false);
            isolated = SceneManager.CreateScene("MaterialPhysics_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = isolated.GetPhysicsScene2D();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in scratch) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            scratch.Clear();
            if (isolated.IsValid() && isolated.isLoaded) yield return SceneManager.UnloadSceneAsync(isolated);
            if (sandbox.IsValid() && sandbox.isLoaded) yield return SceneManager.UnloadSceneAsync(sandbox);
        }

        [Test]
        public void Catalog_HasDistinctMassFrictionBounceAndProfiles()
        {
            float[] density = { 1.225f, 600f, 7850f };
            float[] thickness = { 0f, 0.002f, 0.0003f };
            float radius = 0.42f;
            float volume = 4f / 3f * Mathf.PI * radius * radius * radius;
            float[] friction = { 0.8f, 0.35f, 0.15f };
            float[] bounce = { 0.5f, 0.12f, 0.03f };
            for (int i = 0; i < profiles.Length; i++)
            {
                Assert.That((int)profiles[i].Kind, Is.EqualTo(i));
                double innerRadius = radius - thickness[i];
                double innerVolume = 4.0 / 3.0 * System.Math.PI * innerRadius * innerRadius * innerRadius;
                double outerVolume = 4.0 / 3.0 * System.Math.PI * radius * radius * radius;
                float expectedMass = i == 0 ? volume * density[0] + 0.015f
                    : (float)((outerVolume - innerVolume) * density[i] + innerVolume * 1.225);
                Assert.That(profiles[i].UseReferencePhysics, Is.True);
                Assert.That(profiles[i].Mass, Is.EqualTo(expectedMass).Within(0.002f));
                Assert.That(profiles[i].ContactMaterial.friction, Is.EqualTo(friction[i]).Within(0.001f));
                Assert.That(profiles[i].ContactMaterial.bounciness, Is.EqualTo(bounce[i]).Within(0.001f));
            }
            Assert.That(profiles[0].GravityScale, Is.EqualTo(1f));
            Assert.That(profiles[1].GravityScale, Is.EqualTo(profiles[2].GravityScale));
        }

        [Test]
        public void Selection_AppliesActualPhysicsAndDoesNotHealPlayer()
        {
            health.TakeDamage(20f);
            for (int i = 0; i < 3; i++)
            {
                ball.linearVelocity = Vector2.one * 4f;
                ball.angularVelocity = 100f;
                Assert.That(controls.SelectBallType(i), Is.True);
                Assert.That(controls.CurrentBallProfile, Is.SameAs(profiles[i]));
                Assert.That(ball.mass, Is.EqualTo(profiles[i].Mass));
                Assert.That(ball.gravityScale, Is.EqualTo(profiles[i].GravityScale));
                Assert.That(ball.linearDamping, Is.EqualTo(profiles[i].LinearDamping));
                Assert.That(ball.GetComponent<CircleCollider2D>().sharedMaterial, Is.SameAs(profiles[i].ContactMaterial));
                Assert.That(ball.GetComponent<SpriteRenderer>().color, Is.EqualTo(profiles[i].Color));
                Assert.That(ball.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(ball.angularVelocity, Is.Zero);
                Assert.That(health.IsDead, Is.True);
            }
            Assert.That(controls.SelectBallType(-1), Is.False);
            Assert.That(controls.SelectBallType(3), Is.False);
        }

        [Test]
        public void Reset_RetainsSelectedMaterialAndRestoresWholeSimulation()
        {
            controls.SelectBallType(2);
            health.TakeDamage(30f);
            ball.position = Vector2.up * 3f;
            controls.ResetSimulation();
            Assert.That(controls.CurrentBallProfile, Is.SameAs(profiles[2]));
            Assert.That(ball.mass, Is.EqualTo(profiles[2].Mass));
            Assert.That(ball.linearVelocity, Is.EqualTo(Vector2.zero));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void EqualImpulse_MovesLightBallMoreThanHeavyBall()
        {
            float[] speeds = new float[3];
            for (int i = 0; i < 3; i++)
            {
                Rigidbody2D body = CreateBall(profiles[i], new Vector2(i * 3f, 5f));
                body.gravityScale = 0f;
                body.linearDamping = 0f;
                body.AddForce(Vector2.right, ForceMode2D.Impulse);
                speeds[i] = body.linearVelocity.x;
            }
            Assert.That(speeds[0], Is.GreaterThan(speeds[1]));
            Assert.That(speeds[1], Is.GreaterThan(speeds[2]));
        }

        [Test]
        public void ContactFriction_ChangesActualSlidingDeceleration()
        {
            GameObject floor = CreateObject("Friction Floor");
            floor.transform.position = new Vector2(0f, -1f);
            BoxCollider2D collider = floor.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(100f, 1f);
            Rigidbody2D[] bodies = new Rigidbody2D[3];
            for (int i = 0; i < 3; i++)
            {
                bodies[i] = CreateBall(profiles[i], new Vector2(-20f + i * 15f, 0.2f));
                // Isolate friction from mass, aerodynamic damping and free rolling.
                bodies[i].gravityScale = 1f;
                bodies[i].linearDamping = 0f;
                bodies[i].constraints = RigidbodyConstraints2D.FreezeRotation;
            }
            Physics2D.SyncTransforms();
            for (int i = 0; i < 180; i++) physics.Simulate(StepDuration);
            foreach (Rigidbody2D body in bodies) { body.linearVelocity = Vector2.right * 5f; body.WakeUp(); }
            for (int i = 0; i < 10; i++) physics.Simulate(StepDuration);
            Assert.That(bodies[0].linearVelocity.x, Is.LessThan(bodies[1].linearVelocity.x));
            Assert.That(bodies[1].linearVelocity.x, Is.LessThan(bodies[2].linearVelocity.x));
        }

        [UnityTest]
        public IEnumerator EveryMaterial_RespondsToGravityFrameAndRestoresWorldGravity()
        {
            for (int i = 0; i < 3; i++)
            {
                controls.SelectBallType(i);
                ball.position = new Vector2(-3.8f, 0f);
                ball.transform.position = new Vector3(-3.8f, 0f, 0f);
                ball.linearVelocity = Vector2.zero;
                region.transform.position = ball.position;
                Physics2D.SyncTransforms();
                for (int step = 0; step < 5; step++) yield return new WaitForFixedUpdate();
                Assert.That(ball.GetComponent<GravityBody2D>().CurrentRegion, Is.SameAs(region));
                Assert.That(ball.linearVelocity.y, Is.GreaterThan(0f));
                float before = ball.linearVelocity.y;
                region.transform.position = Vector3.one * 100f;
                for (int step = 0; step < 3; step++) yield return new WaitForFixedUpdate();
                Assert.That(ball.GetComponent<GravityBody2D>().CurrentRegion, Is.Null);
                Assert.That(ball.linearVelocity.y, Is.LessThan(before));
            }
        }

        private Rigidbody2D CreateBall(BallPhysicsProfile profile, Vector2 position)
        {
            GameObject item = CreateObject(profile.Kind.ToString());
            item.transform.position = position;
            CircleCollider2D collider = item.AddComponent<CircleCollider2D>();
            collider.radius = 0.42f;
            Rigidbody2D body = item.AddComponent<Rigidbody2D>();
            item.AddComponent<SpriteRenderer>();
            item.AddComponent<BallMaterial2D>().ApplyProfile(profile);
            return body;
        }

        private GameObject CreateObject(string name)
        {
            GameObject item = new GameObject(name);
            SceneManager.MoveGameObjectToScene(item, isolated);
            scratch.Add(item);
            return item;
        }
    }
}
