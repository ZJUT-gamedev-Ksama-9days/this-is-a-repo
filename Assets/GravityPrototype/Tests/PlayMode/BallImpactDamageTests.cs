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
    public sealed class BallImpactDamageTests
    {
        private const float StepDuration = 0.02f;
        private Scene scene;
        private PhysicsScene2D physics;
        private Rigidbody2D player;
        private PlayerHealth health;
        private Rigidbody2D ball;
        private BallImpactDamage2D impact;
        private readonly List<GameObject> objects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            scene = SceneManager.CreateScene("BallImpactTest_" + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            physics = scene.GetPhysicsScene2D();
            GameObject character = Create("Player");
            CapsuleCollider2D capsule = character.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.78f, 1.45f);
            player = character.AddComponent<Rigidbody2D>();
            player.gravityScale = 0f;
            player.mass = 2f;
            player.constraints = RigidbodyConstraints2D.FreezeAll;
            health = character.AddComponent<PlayerHealth>();

            GameObject sphere = Create("Ball");
            CircleCollider2D circle = sphere.AddComponent<CircleCollider2D>();
            circle.radius = 0.42f;
            ball = sphere.AddComponent<Rigidbody2D>();
            ball.gravityScale = 0f;
            ball.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            impact = sphere.AddComponent<BallImpactDamage2D>();
            PlaceBall(new Vector2(0f, 2f), Vector2.zero);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in objects)
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            objects.Clear();
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Test]
        public void Formula_ImpulseThresholdAndExponentialGrowthAreContinuousAndCapped()
        {
            Assert.That(impact.CalculateAverageForce(10f), Is.EqualTo(100f).Within(0.001f));
            Assert.That(impact.CalculateDamageFromImpulse(-1f), Is.Zero);
            Assert.That(impact.CalculateDamageFromImpulse(4.999f), Is.Zero);
            Assert.That(impact.CalculateDamageFromImpulse(5f), Is.Zero);
            Assert.That(impact.CalculateDamageFromImpulse(5.001f), Is.InRange(0f, 0.001f));
            Assert.That(impact.CalculateDamageFromImpulse(15f), Is.EqualTo(5f).Within(0.001f));
            Assert.That(impact.CalculateDamageFromImpulse(25f), Is.EqualTo(15f).Within(0.001f));
            Assert.That(impact.CalculateDamageFromImpulse(35f), Is.EqualTo(35f).Within(0.001f));
            Assert.That(impact.CalculateDamageFromImpulse(45f), Is.EqualTo(75f).Within(0.001f));
            Assert.That(impact.CalculateDamageFromImpulse(55f), Is.EqualTo(100f));
            Assert.That(impact.CalculateDamageFromImpulse(float.MaxValue), Is.EqualTo(100f));
            Assert.That(impact.CalculateDamageFromImpulse(float.PositiveInfinity), Is.EqualTo(100f));
            Assert.That(impact.CalculateDamageFromImpulse(float.NaN), Is.Zero);
            float increase1 = impact.CalculateDamageFromImpulse(25f) - impact.CalculateDamageFromImpulse(15f);
            float increase2 = impact.CalculateDamageFromImpulse(35f) - impact.CalculateDamageFromImpulse(25f);
            Assert.That(increase2, Is.EqualTo(2f * increase1).Within(0.001f));
        }

        [Test]
        public void ExponentialTuning_ThresholdAndDoublingIntervalControlCurve()
        {
            typeof(BallImpactDamage2D).GetField("safeHeadImpulse", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(impact, 10f);
            typeof(BallImpactDamage2D).GetField("impulseDoublingInterval", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(impact, 5f);
            typeof(BallImpactDamage2D).GetField("exponentialDamageScale", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(impact, 2f);
            Assert.That(impact.CalculateDamageFromImpulse(10f), Is.Zero);
            Assert.That(impact.CalculateDamageFromImpulse(15f), Is.EqualTo(2f).Within(0.001f));
            Assert.That(impact.CalculateDamageFromImpulse(20f), Is.EqualTo(6f).Within(0.001f));
            typeof(BallImpactDamage2D).GetField("exponentialDamageScale", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(impact, 0f);
            Assert.That(impact.CalculateDamageFromImpulse(100f), Is.Zero);
        }

        [Test]
        public void HeadHitAtImpulseThreshold_IsHarmlessThenStrongerHitDamages()
        {
            PlaceBall(new Vector2(0f, 1.6f), Vector2.down * 5f);
            Step(15);
            Assert.That(impact.LastHeadImpactImpulse, Is.GreaterThan(0f));
            Assert.That(impact.LastHeadImpactImpulse, Is.LessThanOrEqualTo(5.001f));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            ResetContact();
            PlaceBall(new Vector2(0f, 1.6f), Vector2.down * 18f);
            Step(15);
            Assert.That(impact.LastHeadImpactImpulse, Is.GreaterThan(5f));
            Assert.That(health.CurrentHealth, Is.LessThan(100f));
        }

        [Test]
        public void SlowBallContactAndSustainedRest_DoNotDrainHealth()
        {
            PlaceBall(new Vector2(0f, 1.3f), Vector2.down * 2f);
            Step(30);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Step(60);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void BallMovingAwayWhilePlayerStomps_RotatingSphereCannotDealImpactDamage()
        {
            player.constraints = RigidbodyConstraints2D.FreezeRotation;
            player.position = new Vector2(0f, 2.5f);
            PlaceBall(new Vector2(0f, 0f), Vector2.down * 1f);
            ball.angularVelocity = 1000f;
            player.linearVelocity = Vector2.down * 8f;
            CapturePlayerPreSimulation();
            Step(25);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(health.LastDamageSource, Is.EqualTo(PlayerDamageSource.None));
        }

        [Test]
        public void IncomingBallStillDamagesEvenWhenPlayerRunsAway()
        {
            player.constraints = RigidbodyConstraints2D.FreezeRotation;
            ball.mass = 3f;
            PlaceBall(new Vector2(0f, 1.6f), Vector2.down * 25f);
            player.linearVelocity = Vector2.down * 3f;
            CapturePlayerPreSimulation();
            Step(8);
            Assert.That(health.CurrentHealth, Is.LessThan(100f));
            Assert.That(health.LastDamageSource, Is.EqualTo(PlayerDamageSource.BallImpact));
        }

        [Test]
        public void DirectHit_UsesSolverImpulseAndFasterBallHurtsMore()
        {
            PlaceBall(new Vector2(0f, 1.6f), Vector2.down * 8f);
            Step(8);
            Assert.That(impact.LastNormalImpulse, Is.EqualTo(8f).Within(0.5f));
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.LastDamage, Is.Zero);
            ResetContact();
            PlaceBall(new Vector2(0f, 1.6f), Vector2.down * 18f);
            Step(8);
            Assert.That(impact.CalculateDamageFromImpulse(impact.LastHeadImpactImpulse), Is.GreaterThanOrEqualTo(6.5f));
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.LastDamageSource, Is.EqualTo(PlayerDamageSource.BallImpact));
            Assert.That(health.LastBallAverageForce,
                Is.EqualTo(health.LastBallImpulse / health.LastBallContactDuration).Within(0.001f));
            Step(60);
            Assert.That(health.IsDead, Is.True);
        }

        [Test]
        public void SameSpeed_HeavierBallProducesLargerImpulseAndForce()
        {
            PlaceBall(new Vector2(0f, 1.6f), Vector2.down * 10f);
            Step(8);
            float firstImpulse = impact.LastNormalImpulse;
            float firstDamage = health.LastDamage;
            ResetContact();
            ball.mass = 3f;
            PlaceBall(new Vector2(0f, 1.6f), Vector2.down * 10f);
            Step(8);
            Assert.That(impact.LastNormalImpulse, Is.EqualTo(firstImpulse * 3f).Within(0.6f));
            Assert.That(health.LastDamage, Is.GreaterThan(firstDamage));
            TestContext.WriteLine($"Fixed target, 10m/s: 1kg impulse={firstImpulse:F3}Ns; 3kg impulse={impact.LastNormalImpulse:F3}Ns, Favg={impact.LastAverageForce:F3}N");
        }

        [Test]
        public void DynamicTarget_ImpulseMatchesReducedMassAndDoesNotApplyCollisionTwice()
        {
            player.constraints = RigidbodyConstraints2D.FreezeRotation;
            player.mass = 70f;
            ball.mass = 3f;
            PlaceBall(new Vector2(-3f, 0f), Vector2.zero);
            Step(2);
            ball.linearVelocity = Vector2.right * 10f;
            impact.SendMessage("FixedUpdate");
            Step(18);
            float expectedImpulse = (3f * 70f / 73f) * 10f;
            Assert.That(health.CurrentHealth, Is.EqualTo(100f), "Body side impacts must not damage the head.");
            Assert.That(player.linearVelocity.x, Is.EqualTo(expectedImpulse / 70f).Within(0.05f));
            float totalMomentum = ball.mass * ball.linearVelocity.x + player.mass * player.linearVelocity.x;
            Assert.That(totalMomentum, Is.EqualTo(30f).Within(0.1f));
        }

        [Test]
        public void EquivalentForceAndDamage_DoNotDependOnPhysicsStepDuration()
        {
            PlaceBall(new Vector2(0f, 1.6f), Vector2.down * 10f);
            Step(8);
            float firstImpulse = impact.LastNormalImpulse;
            float firstForce = impact.LastAverageForce;
            float firstDamage = health.LastDamage;
            ResetContact();
            PlaceBall(new Vector2(0f, 1.6f), Vector2.down * 10f);
            for (int i = 0; i < 16; i++)
            {
                impact.SendMessage("FixedUpdate");
                Assert.That(physics.Simulate(0.01f), Is.True);
            }
            Assert.That(impact.LastNormalImpulse, Is.EqualTo(firstImpulse).Within(0.2f));
            Assert.That(impact.LastAverageForce, Is.EqualTo(firstForce).Within(2f));
            Assert.That(health.LastDamage, Is.EqualTo(firstDamage).Within(0.2f));
        }

        [Test]
        public void ContactDuration_ChangesDisplayedForceButNotImpulseThresholdOrDamage()
        {
            float baseline = impact.CalculateAverageForce(15f);
            float baselineDamage = impact.CalculateDamageFromImpulse(15f);
            typeof(BallImpactDamage2D).GetField("impactDurationSeconds", BindingFlags.Instance
                | BindingFlags.NonPublic).SetValue(impact, 0.2f);
            Assert.That(impact.CalculateAverageForce(15f), Is.EqualTo(baseline / 2f));
            Assert.That(impact.CalculateDamageFromImpulse(15f), Is.EqualTo(baselineDamage));
            Assert.That(impact.CalculateDamageFromImpulse(5f), Is.Zero);
        }

        [Test]
        public void ReverseAndSidewaysHits_WorkWithoutWorldDownRestriction()
        {
            player.rotation = 180f;
            player.transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            PlaceBall(new Vector2(0f, -1.6f), Vector2.up * 18f);
            Step(8);
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.CurrentHealth, Is.LessThan(100f));
            ResetContact();
            player.rotation = -90f;
            player.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            PlaceBall(new Vector2(1.6f, 0f), Vector2.left * 18f);
            Step(8);
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.CurrentHealth, Is.LessThan(100f));
        }

        [Test]
        public void FastBodyAndFeetImpacts_DoNotCauseHeadDamage()
        {
            ball.mass = 6f;
            PlaceBall(new Vector2(-2f, 0f), Vector2.right * 20f);
            Step(12);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            ResetContact();
            PlaceBall(new Vector2(0f, -2f), Vector2.up * 20f);
            Step(12);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(health.LastDamageSource, Is.EqualTo(PlayerDamageSource.None));
        }

        [Test]
        public void PlayerJumpsHeadIntoStationarySpinningBall_IsNotABallStrike()
        {
            player.constraints = RigidbodyConstraints2D.FreezeRotation;
            player.linearVelocity = Vector2.up * 8f;
            PlaceBall(new Vector2(0f, 2.5f), Vector2.zero);
            ball.angularVelocity = 1800f;
            Step(20);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void HeadBandBoundary_HeadFacingContactOnly()
        {
            // The upper capsule cap should be hittable diagonally; a horizontal torso strike is not.
            PlaceBall(new Vector2(0.15f, 2f), new Vector2(-0.6f, -18f));
            Step(12);
            Assert.That(health.CurrentHealth, Is.LessThan(100f));
            Assert.That(impact.LastHeadImpactImpulse, Is.GreaterThan(0f));
        }

        [Test]
        public void GlancingHit_IgnoresHighTangentialSpeedAndSpin()
        {
            PlaceBall(new Vector2(-0.82f, 0f), new Vector2(2f, 20f));
            ball.angularVelocity = 5000f;
            Step(10);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void PlayerMovingIntoStationaryBall_DoesNotCountAsIncomingBall()
        {
            player.constraints = RigidbodyConstraints2D.FreezeRotation;
            player.mass = 70f;
            ball.mass = 3f;
            PlaceBall(new Vector2(0f, 0.9f), Vector2.zero);
            CapturePlayerPreSimulation();
            player.linearVelocity = Vector2.right * 10f;
            CapturePlayerPreSimulation();
            PlaceBall(new Vector2(1f, 0f), Vector2.zero);
            Step(8);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator Recontact_IsSuppressedDuringCooldownThenAllowed()
        {
            PlaceBall(new Vector2(0f, 3f), Vector2.zero);
            Step(2);
            ball.linearVelocity = Vector2.down * 10f;
            impact.SendMessage("FixedUpdate");
            Step(18);
            Assert.That(health.IsDead, Is.False);
            float afterFirstHealth = health.CurrentHealth;

            PlaceBall(new Vector2(0f, 3f), Vector2.zero);
            Step(2);
            ball.linearVelocity = Vector2.down * 10f;
            impact.SendMessage("FixedUpdate");
            Step(18);
            Assert.That(health.CurrentHealth, Is.EqualTo(afterFirstHealth));

            yield return new WaitForSeconds(0.45f);
            PlaceBall(new Vector2(0f, 3f), Vector2.zero);
            Step(2);
            ball.linearVelocity = Vector2.down * 10f;
            impact.SendMessage("FixedUpdate");
            Step(18);
            Assert.That(health.CurrentHealth, Is.EqualTo(afterFirstHealth), "Subthreshold contacts never accumulate into death.");
        }

        [Test]
        public void BallImpactRemainsLethalWhileFallsAreIgnoredAfterReset()
        {
            Assert.That(health.TakeBallImpactDamage(6.49f, 1f), Is.Zero);
            Assert.That(health.TakeBallImpactDamage(6.5f, 1f), Is.EqualTo(100f));
            Assert.That(health.TakeFallDamage(15f), Is.Zero, "Death is latched.");
            health.ResetHealth();
            Assert.That(health.TakeFallDamage(14.99f), Is.Zero);
            Assert.That(health.TakeFallDamage(15f), Is.Zero);
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.LastDamageSource, Is.EqualTo(PlayerDamageSource.None));
            Assert.That(health.TakeBallImpactDamage(6.5f, 1f), Is.EqualTo(100f));
            Assert.That(health.LastDamageSource, Is.EqualTo(PlayerDamageSource.BallImpact));
        }

        [Test]
        public void LethalHit_ClampsHealthAndIgnoresMoreHitsUntilReset()
        {
            ball.mass = 6f;
            PlaceBall(new Vector2(0f, 3f), Vector2.zero);
            Step(2);
            ball.linearVelocity = Vector2.down * 25f;
            impact.SendMessage("FixedUpdate");
            Step(18);
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.TakeBallImpactDamage(10f, 0f), Is.Zero);
            health.ResetHealth();
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator RealSandbox_FallingBallDamagesPlayerAndResetRestoresHealth()
        {
            const string path = "Assets/GravityPrototype/Scenes/PhysicsSandbox.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            Scene sandbox = SceneManager.GetSceneByPath(path);
            try
            {
                PlayerController2D controller = null;
                Rigidbody2D sandboxBall = null;
                PhysicsSandboxController controls = null;
                foreach (GameObject root in sandbox.GetRootGameObjects())
                {
                    if (root.name == "Player") controller = root.GetComponent<PlayerController2D>();
                    if (root.name == "Ball") sandboxBall = root.GetComponent<Rigidbody2D>();
                    if (root.name == "Sandbox Controls") controls = root.GetComponent<PhysicsSandboxController>();
                }
                typeof(PlayerController2D).GetField("readKeyboardInput", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).SetValue(controller, false);
                Assert.That(sandboxBall.GetComponent<BallImpactDamage2D>(), Is.Not.Null);
                controls.ResetSimulation();
                for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
                PlayerHealth sandboxHealth = controller.GetComponent<PlayerHealth>();
                float radius = sandboxBall.transform.localScale.x * sandboxBall.GetComponent<CircleCollider2D>().radius;
                sandboxBall.position = (Vector2)controller.transform.position + Vector2.up * (radius + 2f);
                sandboxBall.transform.position = sandboxBall.position;
                sandboxBall.linearVelocity = Vector2.zero;
                typeof(PlayerHealth).GetMethod("CapturePreSimulationState", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(sandboxHealth, null);
                typeof(BallImpactDamage2D).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(sandboxBall.GetComponent<BallImpactDamage2D>(), null);
                Physics2D.SyncTransforms();
                sandboxBall.linearVelocity = Vector2.down * 8f;
                for (int i = 0; i < 18; i++) yield return new WaitForFixedUpdate();
                Assert.That(sandboxHealth.CurrentHealth, Is.LessThan(100f));
                Assert.That(sandboxHealth.LastBallImpulse, Is.GreaterThan(0f));
                controls.ResetSimulation();
                Assert.That(sandboxHealth.CurrentHealth, Is.EqualTo(100f));
                Assert.That(sandboxHealth.LastDamageSource, Is.EqualTo(PlayerDamageSource.None));
            }
            finally
            {
                if (sandbox.IsValid() && sandbox.isLoaded) SceneManager.UnloadSceneAsync(sandbox);
            }
        }

        private GameObject Create(string name)
        {
            GameObject item = new GameObject(name);
            SceneManager.MoveGameObjectToScene(item, scene);
            objects.Add(item);
            return item;
        }

        private void PlaceBall(Vector2 position, Vector2 velocity)
        {
            ball.position = position;
            ball.linearVelocity = velocity;
            typeof(PlayerHealth).GetMethod("CapturePreSimulationState", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(health, null);
            impact.SendMessage("FixedUpdate");
            ball.WakeUp();
            Physics2D.SyncTransforms();
        }

        private void CapturePlayerPreSimulation()
        {
            typeof(PlayerHealth).GetMethod("CapturePreSimulationState", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(health, null);
        }

        private void Step(int count)
        {
            for (int i = 0; i < count; i++)
            {
                health.SendMessage("FixedUpdate", SendMessageOptions.DontRequireReceiver);
                impact.SendMessage("FixedUpdate");
                Assert.That(physics.Simulate(StepDuration), Is.True);
            }
        }

        private void ResetContact()
        {
            PlaceBall(new Vector2(0f, 3f), Vector2.zero);
            Step(2);
            health.ResetHealth();
        }
    }
}
