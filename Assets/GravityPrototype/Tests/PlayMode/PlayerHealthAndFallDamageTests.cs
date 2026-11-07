using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GravityPrototype.Tests
{
    public sealed class PlayerHealthAndFallDamageTests
    {
        private const string ScenePath = "Assets/GravityPrototype/Scenes/PhysicsSandbox.unity";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene sandbox;
        private PlayerController2D player;
        private PlayerHealth health;
        private PlayerFallDamage2D fallDamage;
        private Rigidbody2D body;
        private PhysicsSandboxController controls;
        private float groundY;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            sandbox = SceneManager.GetSceneByPath(ScenePath);
            foreach (GameObject root in sandbox.GetRootGameObjects())
            {
                if (root.name == "Player") player = root.GetComponent<PlayerController2D>();
                if (root.name == "Sandbox Controls") controls = root.GetComponent<PhysicsSandboxController>();
            }

            Assert.That(player, Is.Not.Null);
            body = player.GetComponent<Rigidbody2D>();
            health = player.GetComponent<PlayerHealth>();
            fallDamage = player.GetComponent<PlayerFallDamage2D>();
            Assert.That(health, Is.Not.Null);
            Assert.That(fallDamage, Is.Not.Null);
            Assert.That(player.GetComponent<PlayerHealthHUD>(), Is.Not.Null);
            typeof(PlayerController2D).GetField("readKeyboardInput", PrivateInstance).SetValue(player, false);
            controls.ResetSimulation();
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True);
            groundY = body.position.y;
            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaxHealth));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (sandbox.IsValid() && sandbox.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(sandbox);
            }
        }

        [Test]
        public void DamageFormula_ThresholdIsSafeAndExcessHeightIsLinear()
        {
            Assert.That(fallDamage.CalculateDamage(0f), Is.Zero);
            Assert.That(fallDamage.CalculateDamage(2.99f), Is.Zero);
            Assert.That(fallDamage.CalculateDamage(3f), Is.Zero);
            Assert.That(fallDamage.CalculateDamage(4f), Is.EqualTo(20f).Within(0.001f));
            Assert.That(fallDamage.CalculateDamage(6f), Is.EqualTo(60f).Within(0.001f));
            Assert.That(fallDamage.CalculateDamage(8f), Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void Health_ClampsAtZeroRejectsInvalidDamageAndResets()
        {
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(health.TakeDamage(-1f), Is.Zero);
            Assert.That(health.TakeDamage(float.NaN), Is.Zero);
            Assert.That(health.TakeDamage(float.PositiveInfinity), Is.Zero);
            Assert.That(health.TakeDamage(25f), Is.EqualTo(100f));
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.Fraction, Is.Zero);
            Assert.That(health.TakeDamage(1000f), Is.Zero);
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.CurrentHealth, Is.Zero);
            Assert.That(health.TakeDamage(1f), Is.Zero);
            controls.ResetSimulation();
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(health.IsDead, Is.False);
        }

        [Test]
        public void LethalThresholds_AreInclusiveAndSubthresholdHitsNeverAccumulate()
        {
            int deaths = 0;
            health.Died += _ => deaths++;
            for (int i = 0; i < 20; i++)
            {
                Assert.That(health.TakeBallImpactDamage(6.499f, 1f), Is.Zero);
                Assert.That(health.TakeFallDamage(14.999f), Is.Zero);
            }
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.TakeFallDamage(fallDamage.CalculateDamage(3.749f)), Is.Zero);
            Assert.That(health.TakeFallDamage(fallDamage.CalculateDamage(3.75f)), Is.EqualTo(100f));
            Assert.That(health.LastDamageSource, Is.EqualTo(PlayerDamageSource.Fall));
            Assert.That(deaths, Is.EqualTo(1));
            health.ResetHealth();
            health.TakeBallImpactDamage(6.5f, 1f);
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.LastDamageSource, Is.EqualTo(PlayerDamageSource.BallImpact));
            Assert.That(deaths, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator NormalJump_DoesNotDamagePlayer()
        {
            float startY = body.position.y;
            float maximumY = startY;
            player.SetInput(0f, true);
            for (int i = 0; i < 160; i++)
            {
                yield return new WaitForFixedUpdate();
                maximumY = Mathf.Max(maximumY, body.position.y);
                Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            }

            Assert.That(player.IsGrounded, Is.True);
            float jumpSpeed = (float)typeof(PlayerController2D).GetField("jumpSpeed", PrivateInstance).GetValue(player);
            float expectedApex = jumpSpeed * jumpSpeed / (2f * Physics2D.gravity.magnitude * body.gravityScale);
            Assert.That(maximumY - startY, Is.EqualTo(expectedApex).Within(0.08f));
            TestContext.WriteLine($"Jump measured height={maximumY - startY:F4} m; theory={expectedApex:F4} m; speed={jumpSpeed:F3} m/s; g={Physics2D.gravity.magnitude:F3} m/s²");
            Assert.That(fallDamage.LastDamage, Is.Zero);
        }

        [UnityTest]
        public IEnumerator BelowThresholdDrop_DoesNotDamagePlayer()
        {
            PlaceAboveGround(2.5f);
            yield return WaitForLanding();
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(fallDamage.LastDamage, Is.Zero);
        }

        [UnityTest]
        public IEnumerator HigherDrop_DealsMoreDamageOnlyOncePerLanding()
        {
            PlaceAboveGround(4f);
            yield return WaitForLanding();
            float firstDamage = 100f - health.CurrentHealth;
            Assert.That(firstDamage, Is.EqualTo(100f));
            float afterLanding = health.CurrentHealth;
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Assert.That(health.CurrentHealth, Is.EqualTo(afterLanding), "Persistent ground contacts must not repeat damage.");

            controls.ResetSimulation();
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            PlaceAboveGround(6f);
            yield return WaitForLanding();
            float secondDamage = 100f - health.CurrentHealth;
            Assert.That(secondDamage, Is.EqualTo(100f));
            Assert.That(secondDamage, Is.EqualTo(firstDamage), "Both falls exceed the instant-death threshold.");
        }

        [UnityTest]
        public IEnumerator RisingBeforeFall_MeasuresAirbornePeakNotJustStartHeight()
        {
            PlaceAboveGround(2f, Mathf.Sqrt(2f * Physics2D.gravity.magnitude * body.gravityScale * 1.8f));
            yield return WaitForLanding();
            Assert.That(fallDamage.LastFallHeight, Is.GreaterThan(3f));
            Assert.That(health.CurrentHealth, Is.LessThan(100f));
            Assert.That(fallDamage.LastDamage, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator WallContact_DoesNotSettleFallEarly()
        {
            PlaceAboveGround(6f);
            body.position = new Vector2(-8.7f, body.position.y);
            player.SetInput(-1f, false);
            Physics2D.SyncTransforms();
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            yield return WaitForLanding();
            Assert.That(health.IsDead, Is.True);
        }

        [UnityTest]
        public IEnumerator BufferedJumpOnLanding_CannotBypassDamage()
        {
            PlaceAboveGround(4f);
            // Queue only near the landing, after any coyote time from the test teleport has expired.
            for (int i = 0; i < 180 && body.position.y > groundY + 0.9f; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(player.IsGrounded, Is.False);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            for (int i = 0; i < 100 && health.CurrentHealth == health.MaxHealth; i++)
            {
                player.SetInput(0f, true);
                yield return new WaitForFixedUpdate();
            }

            Assert.That(health.IsDead, Is.True);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.LessThanOrEqualTo(0.1f), "A buffered jump must not bypass lethal landing.");
        }

        [UnityTest]
        public IEnumerator LethalFall_BlocksMovementUntilFullReset()
        {
            PlaceAboveGround(6f);
            yield return WaitForLanding();
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.CurrentHealth, Is.Zero);
            player.SetInput(1f, true);
            float before = body.position.x;
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.x, Is.EqualTo(before).Within(0.02f));
            Assert.That(body.linearVelocity.y, Is.LessThan(0.1f));

            controls.ResetSimulation();
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(health.IsDead, Is.False);
            player.SetInput(1f, true);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator MidairReset_ClearsOldFallHeightAndRestoresFullHealth()
        {
            PlaceAboveGround(6f);
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            controls.ResetSimulation();
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(fallDamage.LastFallHeight, Is.LessThan(0.2f));
            Assert.That(fallDamage.LastDamage, Is.Zero);
        }

        [UnityTest]
        public IEnumerator WalkingDownSlope_DoesNotAccumulateFallDamage()
        {
            body.position = new Vector2(5.4f, -2.4f);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            fallDamage.ResetTracking();
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
            player.SetInput(-1f, false);
            for (int i = 0; i < 65; i++) yield return new WaitForFixedUpdate();
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        private void PlaceAboveGround(float height, float verticalSpeed = 0f)
        {
            player.SetInput(0f, false);
            body.position = new Vector2(-6.2f, groundY + height);
            body.linearVelocity = new Vector2(0f, verticalSpeed);
            Physics2D.SyncTransforms();
            fallDamage.ResetTracking();
        }

        private IEnumerator WaitForLanding()
        {
            // Clear contacts from the pre-teleport pose before checking the landing state.
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            for (int i = 0; i < 250; i++)
            {
                if (player.IsGrounded) yield break;
                yield return new WaitForFixedUpdate();
            }

            Assert.Fail("Player did not land within the expected physics steps.");
        }
    }
}
