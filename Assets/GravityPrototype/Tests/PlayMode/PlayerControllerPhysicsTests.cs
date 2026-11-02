using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GravityPrototype.Tests
{
    public sealed class PlayerControllerPhysicsTests
    {
        private const string ScenePath = "Assets/GravityPrototype/Scenes/PhysicsSandbox.unity";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene sandbox;
        private PlayerController2D player;
        private Rigidbody2D body;
        private Rigidbody2D ball;
        private GravityRegion2D region;
        private PhysicsSandboxController controls;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            sandbox = SceneManager.GetSceneByPath(ScenePath);
            foreach (GameObject root in sandbox.GetRootGameObjects())
            {
                if (root.name == "Player") player = root.GetComponent<PlayerController2D>();
                if (root.name == "Ball") ball = root.GetComponent<Rigidbody2D>();
                if (root.name == "Gravity Frame") region = root.GetComponent<GravityRegion2D>();
                if (root.name == "Sandbox Controls") controls = root.GetComponent<PhysicsSandboxController>();
            }

            Assert.That(player, Is.Not.Null, "The sandbox must contain a playable protagonist.");
            body = player.GetComponent<Rigidbody2D>();
            typeof(PlayerController2D).GetField("readKeyboardInput", PrivateInstance).SetValue(player, false);
            controls.ResetSimulation();
            // Wait for both character and ball to make real contacts with the floor.
            for (int i = 0; i < 65; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True, "A standing capsule must recognize the floor.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (sandbox.IsValid() && sandbox.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(sandbox);
            }
        }

        [UnityTest]
        public IEnumerator Movement_ReachesSpeedThenBrakesAndReverses()
        {
            float start = body.position.x;
            player.SetInput(-1f, false);
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.x, Is.LessThan(start - 0.3f));
            Assert.That(body.linearVelocity.x, Is.InRange(-5.6f, -5f));
            player.SetInput(0f, false);
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(body.linearVelocity.x), Is.LessThan(0.1f));
            player.SetInput(1f, false);
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.x, Is.GreaterThan(5f));
            Assert.That(Mathf.Abs(body.rotation), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator Jump_LeavesFloorRejectsSecondJumpAndLands()
        {
            float startY = body.position.y;
            player.SetInput(0f, true);
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.y, Is.GreaterThan(startY + 0.2f));
            Assert.That(player.IsGrounded, Is.False);
            float previousVerticalSpeed = body.linearVelocity.y;
            player.SetInput(0f, true);
            for (int i = 0; i < 2; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.LessThan(previousVerticalSpeed), "No mid-air double jump.");
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.True);
            Assert.That(body.position.y, Is.EqualTo(startY).Within(0.04f));
        }

        [UnityTest]
        public IEnumerator WalkingIntoBall_PushesItThroughPhysicsContacts()
        {
            region.transform.position = Vector3.one * 100f;
            float initialBallX = ball.position.x;
            player.SetInput(1f, false);
            for (int i = 0; i < 55; i++) yield return new WaitForFixedUpdate();
            Assert.That(ball.position.x, Is.GreaterThan(initialBallX + 1f));
            Assert.That(ball.linearVelocity.x, Is.GreaterThan(0.5f));
            Assert.That(body.position.x, Is.LessThan(ball.position.x), "Player must not tunnel through the ball.");
            Assert.That(body.position.y, Is.LessThan(-2.8f), "Pushing should not launch the player upward.");
        }

        [UnityTest]
        public IEnumerator FrameOverPlayer_DoesNotReverseCharacterGravity()
        {
            Assert.That(player.GetComponent<GravityBody2D>(), Is.Null);
            Vector2 position = body.position;
            position.y = 0f;
            body.position = position;
            body.linearVelocity = Vector2.zero;
            region.transform.position = position;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.LessThan(-0.5f));
        }

        [UnityTest]
        public IEnumerator WallContact_DoesNotGrantJump()
        {
            body.position = new Vector2(-8.7f, 0f);
            body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
            player.SetInput(-1f, false);
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            Assert.That(player.IsGrounded, Is.False);
            player.SetInput(-1f, true);
            for (int i = 0; i < 2; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.linearVelocity.y, Is.LessThan(0f));
            Assert.That(body.position.x, Is.GreaterThan(-8.85f), "Capsule must not cross the wall.");
        }

        [UnityTest]
        public IEnumerator Player_CanWalkUpTestRamp()
        {
            region.transform.position = Vector3.one * 100f;
            body.position = new Vector2(1.5f, -3.25f);
            body.linearVelocity = Vector2.zero;
            ball.position = new Vector2(-5f, -3.6f);
            Physics2D.SyncTransforms();
            float initialY = body.position.y;
            player.SetInput(1f, false);
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.x, Is.GreaterThan(4f));
            Assert.That(body.position.y, Is.GreaterThan(initialY + 0.25f));
            Assert.That(player.IsGrounded, Is.True);
            float rampY = body.position.y;
            player.SetInput(0f, true);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            Assert.That(body.position.y, Is.GreaterThan(rampY + 0.2f), "Upward slope travel must not prevent jumping.");
        }

        [Test]
        public void SandboxReset_RestoresPlayerAndClearsQueuedInput()
        {
            Vector2 spawn = (Vector2)typeof(PlayerController2D).GetField("spawnPosition", PrivateInstance).GetValue(player);
            for (int i = 0; i < 5; i++)
            {
                body.position = new Vector2(i, 1f);
                body.linearVelocity = new Vector2(4f, 7f);
                player.SetInput(1f, true);
                controls.ResetSimulation();
                Assert.That(Vector2.Distance(body.position, spawn), Is.LessThan(0.001f));
                Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That((float)typeof(PlayerController2D).GetField("horizontalInput", PrivateInstance).GetValue(player), Is.Zero);
                Assert.That((float)typeof(PlayerController2D).GetField("jumpBufferRemaining", PrivateInstance).GetValue(player), Is.Zero);
            }
        }
    }
}
