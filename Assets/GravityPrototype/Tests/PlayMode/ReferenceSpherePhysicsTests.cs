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
    public sealed class ReferenceSpherePhysicsTests
    {
        private const float Dt = 0.02f;
        private Scene scene;
        private PhysicsScene2D physics;
        private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            scene = SceneManager.CreateScene("SphereSI_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = scene.GetPhysicsScene2D();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (UnityEngine.Object item in objects)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            objects.Clear();
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Test]
        public void ReferenceData_ComputesSphereMassAreaVolumeAndInertia()
        {
            BallPhysicsProfile wood = Profile(BallMaterialKind.Wood, 600f);
            BallPhysicsProfile iron = Profile(BallMaterialKind.Iron, 7850f);
            BallPhysicsProfile balloon = Profile(BallMaterialKind.Balloon, 1.225f);
            Assert.That(wood.Volume, Is.EqualTo(0.3103390887f).Within(0.000001f));
            Assert.That(wood.FrontalArea, Is.EqualTo(0.5541769441f).Within(0.000001f));
            Assert.That(wood.Mass, Is.EqualTo(3.02216f).Within(0.003f));
            Assert.That(iron.Mass, Is.EqualTo(5.5957f).Within(0.005f));
            Assert.That(balloon.Mass, Is.EqualTo(0.39516538f).Within(0.00001f));
            Rigidbody2D body = Body(wood, Vector2.zero);
            Assert.That(body.inertia, Is.GreaterThan(0.6f * wood.Mass * 0.42f * 0.42f));
            Assert.That(body.inertia, Is.EqualTo(wood.MomentOfInertia).Within(0.001f));
            Assert.That(body.gravityScale, Is.EqualTo(1f));
            Assert.That(body.linearDamping, Is.Zero);
            Assert.That(body.angularDamping, Is.Zero);
        }

        [Test]
        public void NoAir_SeventyPercentGReversalMatchesVelocityAndDiscreteBallisticPositionForBothMasses()
        {
            Frame();
            BallPhysicsProfile wood = Profile(BallMaterialKind.Wood, 600f, 0f);
            BallPhysicsProfile iron = Profile(BallMaterialKind.Iron, 7850f, 0f);
            Rigidbody2D a = Body(wood, Vector2.left * 2f);
            Rigidbody2D b = Body(iron, Vector2.right * 2f);
            Step(new[] { a, b }, 25);
            float expectedSpeed = PhysicsEnvironment.Gravity * 0.5f;
            // Unity uses semi-implicit Euler: y_N = a * dt² * N(N+1)/2.
            float expectedPosition = PhysicsEnvironment.Gravity * Dt * Dt * 25f * 26f / 2f;
            Assert.That(a.linearVelocity.y, Is.EqualTo(expectedSpeed).Within(0.002f));
            Assert.That(b.linearVelocity.y, Is.EqualTo(expectedSpeed).Within(0.002f));
            Assert.That(a.position.y, Is.EqualTo(expectedPosition).Within(0.002f));
            Assert.That(b.position.y, Is.EqualTo(expectedPosition).Within(0.002f));
            TestContext.WriteLine($"70%g, t=0.5s: wood v={a.linearVelocity.y:F6}, iron v={b.linearVelocity.y:F6}; y={a.position.y:F6}, discrete theory={expectedPosition:F6}");
        }

        [Test]
        public void StandardAir_HollowWoodAndIronDifferOnlyByCalculatedBuoyancyAndDrag()
        {
            Frame();
            BallPhysicsProfile wood = Profile(BallMaterialKind.Wood, 600f);
            BallPhysicsProfile iron = Profile(BallMaterialKind.Iron, 7850f);
            Rigidbody2D a = Body(wood, Vector2.left * 2f);
            Rigidbody2D b = Body(iron, Vector2.right * 2f);
            Step(new[] { a, b }, 25);
            float ExpectedSpeed(BallPhysicsProfile profile)
            {
                float netAcceleration = PhysicsEnvironment.Gravity * (1f - profile.AirDensity * profile.Volume / profile.Mass);
                float dragPerMass = 0.5f * profile.AirDensity * profile.DragCoefficient * profile.FrontalArea / profile.Mass;
                float terminal = Mathf.Sqrt(netAcceleration / dragPerMass);
                return terminal * (float)Math.Tanh(netAcceleration * 0.5f / terminal);
            }
            Assert.That(a.linearVelocity.y, Is.EqualTo(ExpectedSpeed(wood)).Within(0.02f));
            Assert.That(b.linearVelocity.y, Is.EqualTo(ExpectedSpeed(iron)).Within(0.02f));
            Assert.That(b.linearVelocity.y, Is.GreaterThan(a.linearVelocity.y));
            TestContext.WriteLine($"Standard air t=0.5s: wood v={a.linearVelocity.y:F6}m/s y={a.position.y:F6}m; iron v={b.linearVelocity.y:F6}m/s y={b.position.y:F6}m");
        }

        [Test]
        public void DragAndBuoyancy_UseSIEquationsAndOpposeCorrectDirections()
        {
            BallPhysicsProfile p = Profile(BallMaterialKind.Wood, 600f);
            Vector2 atRest = p.CalculateAirForce(Vector2.zero, Vector2.down * PhysicsEnvironment.Gravity);
            Assert.That(atRest.y, Is.EqualTo(p.AirDensity * p.Volume * PhysicsEnvironment.Gravity).Within(0.0001f));
            Vector2 drag = p.CalculateAirForce(Vector2.right * 10f, Vector2.zero);
            Assert.That(drag.x, Is.EqualTo(-0.5f * 1.225f * 0.47f * p.FrontalArea * 100f).Within(0.0001f));
            Assert.That(drag.y, Is.Zero);
            Assert.That(p.CalculateAirForce(Vector2.zero, Vector2.up * PhysicsEnvironment.Gravity).y, Is.LessThan(0f));
        }

        [Test]
        public void AirFilledBalloon_SinksSlowlyDueToBuoyancyAndDragRatherThanReducedGravity()
        {
            BallPhysicsProfile p = Profile(BallMaterialKind.Balloon, 1.225f);
            Rigidbody2D body = Body(p, Vector2.zero);
            Step(new[] { body }, 1500);
            float netWeight = (p.Mass - p.AirDensity * p.Volume) * PhysicsEnvironment.Gravity;
            float terminal = Mathf.Sqrt(2f * netWeight / (p.AirDensity * p.DragCoefficient * p.FrontalArea));
            Assert.That(body.gravityScale, Is.EqualTo(1f));
            Assert.That(body.linearVelocity.y, Is.LessThan(0f));
            Assert.That(-body.linearVelocity.y, Is.EqualTo(terminal).Within(0.04f));
            TestContext.WriteLine($"Air balloon after 30s: downward speed={-body.linearVelocity.y:F6}m/s; predicted terminal={terminal:F6}m/s");
        }

        [Test]
        public void EntryAndExit_ConserveVelocityAndUseOnlyOneGravityField()
        {
            GravityRegion2D region = Frame();
            BallPhysicsProfile p = Profile(BallMaterialKind.Iron, 7850f, 0f);
            Rigidbody2D body = Body(p, Vector2.zero);
            body.linearVelocity = new Vector2(2f, -3f);
            Step(new[] { body }, 1);
            Assert.That(body.linearVelocity.x, Is.EqualTo(2f).Within(0.001f));
            Assert.That(body.linearVelocity.y, Is.EqualTo(-3f + PhysicsEnvironment.Gravity * Dt).Within(0.001f));
            region.transform.position = Vector2.one * 1000f;
            Step(new[] { body }, 1);
            Assert.That(body.linearVelocity.y, Is.EqualTo(-3f).Within(0.001f));
        }

        private BallPhysicsProfile Profile(BallMaterialKind kind, float density, float air = 1.225f)
        {
            BallPhysicsProfile p = ScriptableObject.CreateInstance<BallPhysicsProfile>();
            objects.Add(p);
            Set(p, "useReferencePhysics", true);
            Set(p, "kind", kind);
            Set(p, "diameterMeters", 0.84f);
            Set(p, "densityKgPerCubicMeter", density);
            Set(p, "airDensityKgPerCubicMeter", air);
            Set(p, "shellThicknessMeters", kind == BallMaterialKind.Iron ? 0.0003f : 0.002f);
            return p;
        }

        private GameObject Create(string name)
        {
            GameObject item = new GameObject(name);
            SceneManager.MoveGameObjectToScene(item, scene);
            objects.Add(item);
            return item;
        }

        private GravityRegion2D Frame()
        {
            GravityRegion2D result = Create("Frame").AddComponent<GravityRegion2D>();
            Set(result, "size", Vector2.one * 200f);
            return result;
        }

        private Rigidbody2D Body(BallPhysicsProfile profile, Vector2 position)
        {
            GameObject item = Create(profile.Kind.ToString());
            item.transform.position = position;
            item.AddComponent<CircleCollider2D>();
            Rigidbody2D body = item.AddComponent<Rigidbody2D>();
            item.AddComponent<SpriteRenderer>();
            item.AddComponent<GravityBody2D>();
            item.AddComponent<BallMaterial2D>().ApplyProfile(profile);
            return body;
        }

        private void Step(Rigidbody2D[] bodies, int count)
        {
            Physics2D.SyncTransforms();
            for (int i = 0; i < count; i++)
            {
                foreach (Rigidbody2D body in bodies)
                {
                    // SendMessage would invoke EVERY FixedUpdate on this GameObject and double-count forces.
                    typeof(BallMaterial2D).GetMethod("ApplyAirForces", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(body.GetComponent<BallMaterial2D>(), null);
                    typeof(GravityBody2D).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(body.GetComponent<GravityBody2D>(), null);
                }
                Assert.That(physics.Simulate(Dt), Is.True);
            }
        }

        private static void Set(object item, string name, object value)
        {
            item.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(item, value);
        }
    }
}
