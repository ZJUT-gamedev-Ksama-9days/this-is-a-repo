using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GravityPrototype.Tests
{
    /// <summary>生命值 API（一击即死口径）：无效伤害不生效、致死后不再累计、重置恢复满血。</summary>
    public sealed class PlayerHealthTests
    {
        private const string ScenePath = "Assets/GravityPrototype/Scenes/PhysicsSandbox.unity";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private Scene sandbox;
        private PlayerController2D player;
        private PlayerHealth health;
        private PhysicsSandboxController controls;

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
            health = player.GetComponent<PlayerHealth>();
            Assert.That(health, Is.Not.Null);
            typeof(PlayerController2D).GetField("readKeyboardInput", PrivateInstance).SetValue(player, false);
            controls.ResetSimulation();
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
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
    }
}
