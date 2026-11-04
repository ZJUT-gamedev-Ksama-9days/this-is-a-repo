using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GravityPrototype.Tests
{
    public sealed class PhysicsSandboxIntegrationTests
    {
        private const string ScenePath = "Assets/GravityPrototype/Scenes/PhysicsSandbox.unity";
        private Scene sandbox;
        private GameObject ballObject;
        private GameObject frameObject;
        private GameObject controlsObject;
        private Camera camera;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            sandbox = SceneManager.GetSceneByPath(ScenePath);
            foreach (GameObject root in sandbox.GetRootGameObjects())
            {
                switch (root.name)
                {
                    case "Ball": ballObject = root; break;
                    case "Gravity Frame": frameObject = root; break;
                    case "Sandbox Controls": controlsObject = root; break;
                    case "Main Camera": camera = root.GetComponent<Camera>(); break;
                }
            }

            Assert.That(ballObject, Is.Not.Null);
            Assert.That(frameObject, Is.Not.Null);
            Assert.That(controlsObject, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
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
        public IEnumerator SceneComponents_RunInPlayerLoopAndReactToMovingFrame()
        {
            Rigidbody2D ball = ballObject.GetComponent<Rigidbody2D>();
            GravityBody2D gravity = ballObject.GetComponent<GravityBody2D>();
            Assert.That(ball.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode2D.Continuous));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(gravity.CurrentRegion, Is.Null);
            Assert.That(ball.linearVelocity.y, Is.LessThan(0f));

            frameObject.transform.position = ball.position;
            float before = ball.linearVelocity.y;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(gravity.CurrentRegion, Is.SameAs(frameObject.GetComponent<GravityRegion2D>()));
            Assert.That(ball.linearVelocity.y, Is.GreaterThan(before));
            Assert.That(frameObject.GetComponentsInChildren<Collider2D>(), Is.Empty,
                "The frame must never act as a physical wall.");
            Assert.That(frameObject.GetComponentsInChildren<LineRenderer>().Length, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Reset_RestoresBallFrameAndVelocitiesRepeatedly()
        {
            Rigidbody2D ball = ballObject.GetComponent<Rigidbody2D>();
            PhysicsSandboxController controls = controlsObject.GetComponent<PhysicsSandboxController>();
            // The async scene load can already advance physics before the test resumes.
            // Compare against the controller's Awake-time snapshot, not an already-falling ball.
            const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
            Vector2 initialPosition = (Vector2)typeof(PhysicsSandboxController)
                .GetField("ballStartPosition", privateInstance).GetValue(controls);
            float initialRotation = (float)typeof(PhysicsSandboxController)
                .GetField("ballStartRotation", privateInstance).GetValue(controls);
            Vector3 initialFrame = (Vector3)typeof(PhysicsSandboxController)
                .GetField("frameStartPosition", privateInstance).GetValue(controls);
            for (int i = 0; i < 5; i++)
            {
                ball.position = Vector2.one * i;
                ball.rotation = 45f;
                ball.linearVelocity = new Vector2(4f, 7f);
                ball.angularVelocity = 90f;
                frameObject.transform.position = Vector3.up * i;
                controls.ResetSimulation();
                Assert.That(Vector2.Distance(ball.position, initialPosition), Is.LessThan(0.001f));
                Assert.That(ball.rotation, Is.EqualTo(initialRotation).Within(0.001f));
                Assert.That(ball.linearVelocity, Is.EqualTo(Vector2.zero));
                Assert.That(ball.angularVelocity, Is.EqualTo(0f));
                Assert.That(frameObject.transform.position, Is.EqualTo(initialFrame));
            }

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(ball.linearVelocity.y, Is.LessThan(0f));
        }

        [Test]
        public void DragCoordinates_ConvertCameraScreenPointToWorldPlane()
        {
            GravityFrameDrag2D drag = frameObject.GetComponent<GravityFrameDrag2D>();
            // Explicit camera assignment isolates the test from any other loaded scene's MainCamera.
            typeof(GravityFrameDrag2D).GetField("inputCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(drag, camera);
            Vector3 expected = new Vector3(-2f, 1.5f, frameObject.transform.position.z);
            Vector3 screenPoint = camera.WorldToScreenPoint(expected);
            object[] arguments = { screenPoint, Vector3.zero };
            MethodInfo method = typeof(GravityFrameDrag2D).GetMethod("TryGetWorldPoint",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That((bool)method.Invoke(drag, arguments), Is.True);
            Assert.That(Vector3.Distance((Vector3)arguments[1], expected), Is.LessThan(0.001f));
        }

        [Test]
        public void DragBounds_KeepEntireFrameInsideCamera()
        {
            GravityFrameDrag2D drag = frameObject.GetComponent<GravityFrameDrag2D>();
            typeof(GravityFrameDrag2D).GetField("inputCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(drag, camera);
            MethodInfo method = typeof(GravityFrameDrag2D).GetMethod("ClampToCamera",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Vector3 position = (Vector3)method.Invoke(drag, new object[] { new Vector3(1000f, -1000f, 0f) });
            Vector2 half = frameObject.GetComponent<GravityRegion2D>().WorldHalfExtents;
            Vector3 topRight = camera.WorldToViewportPoint(position + (Vector3)half);
            Vector3 bottomLeft = camera.WorldToViewportPoint(position - (Vector3)half);
            Assert.That(topRight.x, Is.LessThanOrEqualTo(1.001f));
            Assert.That(bottomLeft.x, Is.GreaterThanOrEqualTo(-0.001f));
            Assert.That(topRight.y, Is.LessThanOrEqualTo(1.001f));
            Assert.That(bottomLeft.y, Is.GreaterThanOrEqualTo(-0.001f));
        }
    }
}
