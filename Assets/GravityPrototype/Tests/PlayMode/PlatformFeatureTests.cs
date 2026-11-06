using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GravityPrototype.Tests
{
    public sealed class PlatformFeatureTests
    {
        private Scene scene;
        private PlayerController2D player;
        private PlayerHealth health;
        private LevelController level;
        private Rigidbody2D body;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            const string path = "Assets/GravityPrototype/Scenes/PlatformLevel.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            scene = SceneManager.GetSceneByPath(path);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == "Player") player = root.GetComponent<PlayerController2D>();
                if (root.name == "Level") level = root.GetComponent<LevelController>();
            }
            health = player.GetComponent<PlayerHealth>();
            body = player.GetComponent<Rigidbody2D>();
            Set(player, "readKeyboardInput", false);
            Set(player.GetComponent<AstronautToolController>(), "readKeyboardInput", false);
            Set(player.GetComponent<MechanicalArm2D>(), "readMouseInput", false);
            Set(level, "readKeyboardInput", false);
            Set(level, "reloadSceneOnDeath", false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }

        [Test]
        public void Launch_PreservesGravityAndClearsBufferedJump()
        {
            var pad = Find<GravityBouncePad2D>();
            pad.ReturnToHome();
            body.linearVelocity = new Vector2(2f, -1f);
            player.SetInput(0f, true);
            float gravity = body.gravityScale;
            Assert.That(pad.TryBounce(player), Is.True);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(9f));
            Assert.That(body.linearVelocity.x, Is.EqualTo(2f).Within(0.01f));
            Assert.That(body.gravityScale, Is.EqualTo(gravity));
            Assert.That(player.IsGrounded, Is.False);
            Assert.That((float)typeof(PlayerController2D).GetField("jumpBufferRemaining", Private).GetValue(player), Is.Zero);
            Assert.That(pad.TryBounce(player), Is.False, "Launch cooldown prevents double launch.");
        }

        [UnityTest]
        public IEnumerator RealPadContact_LaunchesPlayerThroughCollision()
        {
            var pad = Find<GravityBouncePad2D>();
            body.position = pad.Body.position + Vector2.up * 1.2f;
            body.transform.position = body.position;
            body.linearVelocity = Vector2.down * 2f;
            Physics2D.SyncTransforms();
            bool launched = false;
            pad.Bounced += (_, __) => launched = true;
            for (int i = 0; i < 30 && !launched; i++) yield return new WaitForFixedUpdate();
            Assert.That(launched, Is.True);
            Assert.That(body.linearVelocity.y, Is.GreaterThan(5f));
        }

        [UnityTest]
        public IEnumerator Spike_ContinuousDamageSurvivesResetWhileStillOverlapping()
        {
            SpikeHazard2D spike = Find<SpikeHazard2D>();
            body.position = spike.transform.position;
            body.transform.position = body.position;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.LastDamageSource, Is.EqualTo(PlayerDamageSource.Spike));
            health.ResetHealth();
            spike.ClearContacts();
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Assert.That(health.IsDead, Is.True, "Reset must rediscover an existing lethal overlap.");
            spike.enabled = false;
            health.ResetHealth();
            spike.enabled = true;
            for (int i = 0; i < 4; i++) yield return new WaitForFixedUpdate();
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.LastDamageSource, Is.EqualTo(PlayerDamageSource.Spike));
        }

        [Test]
        public void SpikeCurves_LinearSteppedAndDelayed()
        {
            var profile = new SpikeDamageProfile();
            Assert.That(profile.EvaluateTotalDamage(2f), Is.EqualTo(50f));
            Set(profile, "mode", SpikeDamageMode.Stepped);
            Assert.That(profile.EvaluateTotalDamage(0.39f), Is.Zero);
            Assert.That(profile.EvaluateTotalDamage(0.81f), Is.EqualTo(20f));
            Set(profile, "mode", SpikeDamageMode.DelayedBurst);
            Assert.That(profile.EvaluateTotalDamage(0.5f), Is.Zero);
            Assert.That(profile.EvaluateTotalDamage(1.6f), Is.EqualTo(80f).Within(0.01f));
        }

        [UnityTest]
        public IEnumerator Death_InPlaceRestartRestoresToolsPadsTimerAndHealth()
        {
            var pad = Find<GravityBouncePad2D>();
            Vector2 home = pad.Body.position;
            pad.Body.position += Vector2.right;
            player.GetComponent<AstronautToolController>().Equip(AstronautTool.EmptyHands);
            health.TakeDamage(100f);
            Assert.That(level.IsDeathPending, Is.True);
            Assert.That(player.enabled, Is.False);
            yield return new WaitForSeconds(0.9f);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
            Assert.That(player.enabled, Is.True);
            Assert.That(level.IsDeathPending, Is.False);
            Assert.That(level.Timer.IsRunning, Is.True);
            Assert.That(Vector2.Distance(home, pad.Body.position), Is.LessThan(0.05f));
            Assert.That(player.GetComponent<AstronautToolController>().Equipped, Is.EqualTo(AstronautTool.GravityDevice));
        }

        [UnityTest]
        public IEnumerator Goal_StopsClockAndPersistsBestWithoutTouchingRealRecord()
        {
            string id = "LocusTest_" + System.Guid.NewGuid().ToString("N");
            Set(level, "levelId", id);
            try
            {
                yield return new WaitForSeconds(0.05f);
                level.Goal.MarkReached();
                Assert.That(level.IsCompleted, Is.True);
                Assert.That(level.Timer.IsRunning, Is.False);
                Assert.That(LevelRecordStore.TryGetBestTime(id, out float best), Is.True);
                Assert.That(best, Is.EqualTo(level.LastRunSeconds));
                Assert.That(LevelRecordStore.SubmitTime(id, best + 1f), Is.False);
                Assert.That(LevelRecordStore.SubmitTime(id, best * 0.5f), Is.True);
            }
            finally { LevelRecordStore.ClearRecord(id); }
        }

        private T Find<T>() where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T value = root.GetComponentInChildren<T>();
                if (value != null) return value;
            }
            return null;
        }
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    }
}
