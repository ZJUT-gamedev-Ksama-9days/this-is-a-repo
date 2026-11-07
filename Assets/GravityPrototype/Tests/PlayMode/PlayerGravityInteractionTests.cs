using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GravityPrototype.Tests
{
    public sealed class PlayerGravityInteractionTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene scene;
        private PlayerController2D player;
        private Rigidbody2D body;
        private GravityBody2D gravity;
        private PlayerHealth health;
        private PlayerFallDamage2D fall;
        private GravityRegion2D region;
        private PhysicsSandboxController controls;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            const string path = "Assets/GravityPrototype/Scenes/PhysicsSandbox.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(path);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Player") player = root.GetComponent<PlayerController2D>();
                if (root.name == "Gravity Frame") region = root.GetComponent<GravityRegion2D>();
                if (root.name == "Sandbox Controls") controls = root.GetComponent<PhysicsSandboxController>();
            }
            Assert.That(player, Is.Not.Null);
            body = player.GetComponent<Rigidbody2D>();
            gravity = player.GetComponent<GravityBody2D>();
            health = player.GetComponent<PlayerHealth>();
            fall = player.GetComponent<PlayerFallDamage2D>();
            Assert.That(gravity, Is.Not.Null);
            SetField(player, "readKeyboardInput", false);
            controls.ResetSimulation();
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [UnityTest]
        public IEnumerator FrameOverRestingPlayer_LiftsAndRotatesWithoutFakeFloorJump()
        {
            float initialY = body.position.y;
            region.transform.position = body.position;
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();
            Assert.That(gravity.CurrentRegion, Is.SameAs(region));
            Assert.That(body.position.y, Is.GreaterThan(initialY + 0.1f));
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0.4f));
            Assert.That(player.IsGrounded, Is.False);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(body.rotation, 180f)), Is.LessThan(0.1f));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator Ceiling_IsGroundThenJumpMovesAwayAndHorizontalControlsRemainReadable()
        {
            Place(new Vector2(-6.2f, 1f));
            CoverRoom(Vector2.up);
            yield return WaitForSupport();
            Assert.That(body.position.y, Is.GreaterThan(3f));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            player.SetInput(1f, false);
            float x = body.position.x;
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.x, Is.GreaterThan(x));
            player.SetInput(0f, true);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.LessThan(-0.5f));
            Assert.That(player.IsGrounded, Is.False);
        }

        [UnityTest]
        public IEnumerator SidewaysGravity_WallSupportsAndJumpPointsAway()
        {
            Place(new Vector2(7f, 1f));
            CoverRoom(Vector2.right);
            yield return WaitForSupport();
            Assert.That(body.position.x, Is.GreaterThan(8f));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            player.SetInput(1f, false);
            float y = body.position.y;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.y, Is.GreaterThan(y));
            player.SetInput(0f, true);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.x, Is.LessThan(-0.5f));
            Assert.That(player.IsGrounded, Is.False);
        }

        [UnityTest]
        public IEnumerator ZeroGravity_PreservesFallAxisVelocityAndDoesNotGrantJump()
        {
            Place(new Vector2(-6.2f, 0f));
            CoverRoom(Vector2.zero);
            body.linearVelocity = Vector2.up * 2f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.False);
            Assert.That(body.linearVelocity.y, Is.EqualTo(2f).Within(0.05f));
            player.SetInput(0f, true);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.EqualTo(2f).Within(0.05f));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator UpwardFall_LandingOnCeilingNeverKills()
        {
            Place(new Vector2(-6.2f, -0.475f));
            CoverRoom(Vector2.up);
            yield return WaitForSupport();
            Assert.That(health.IsDead, Is.False);
            Assert.That(fall.LastDamage, Is.Zero);
            float remaining = health.CurrentHealth;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.That(health.CurrentHealth, Is.EqualTo(remaining));
        }

        [UnityTest]
        public IEnumerator Reversal_StartsNewFallSegmentAndResetClearsHealthAndRotation()
        {
            Place(new Vector2(-6.2f, 2.5f));
            region.transform.position = Vector2.one * 100f;
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            CoverRoom(Vector2.up);
            yield return WaitForSupport();
            Assert.That(health.CurrentHealth, Is.EqualTo(100f), "A short reversed fall must not reuse world-Y fall history.");
            region.transform.position = Vector2.one * 100f;
            yield return WaitForSupport();
            Assert.That(body.position.y, Is.LessThan(-3f));
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            controls.ResetSimulation();
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(Mathf.Abs(body.rotation), Is.LessThan(0.01f));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        }

        private void Place(Vector2 position)
        {
            player.SetInput(0f, false);
            body.position = position;
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            fall.ResetTracking();
        }

        private void CoverRoom(Vector2 direction)
        {
            SetField(region, "size", new Vector2(40f, 40f));
            SetField(region, "direction", direction);
            region.transform.position = Vector3.zero;
        }

        private IEnumerator WaitForSupport()
        {
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            for (int i = 0; i < 250; i++)
            {
                if (player.IsGrounded) yield break;
                yield return new WaitForFixedUpdate();
            }
            Assert.Fail("Player never found support in the current gravity direction.");
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        }
    }
}
