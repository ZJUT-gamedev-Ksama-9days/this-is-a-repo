using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GravityPrototype.Tests
{
    public sealed class CharacterPixelAnimationTests
    {
        private Scene scene;
        private PlayerController2D player;
        private PlayerHealth health;
        private CharacterPixelAnimator animator;
        private SpriteRenderer artwork;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            const string path="Assets/GravityPrototype/Scenes/PhysicsSandbox.unity";
            yield return SceneManager.LoadSceneAsync(path,LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByPath(path);
            foreach(var root in scene.GetRootGameObjects())if(root.name=="Player")player=root.GetComponent<PlayerController2D>();
            Assert.That(player.GetComponent<CapsuleCollider2D>().size,Is.EqualTo(PixelMetrics.PlayerColliderSize));
            animator=player.GetComponent<CharacterPixelAnimator>();health=player.GetComponent<PlayerHealth>();
            artwork=player.transform.Find("Visuals/Pixel Astronaut").GetComponent<SpriteRenderer>();
            Set(player,"readKeyboardInput",false);player.enabled=false;
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown(){if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);}

        [Test] public void AuthoredArt_HasExact32By48PixelsAt32Ppu()
        {
            Assert.That(artwork.sprite.texture.width,Is.EqualTo(32));
            Assert.That(artwork.sprite.texture.height,Is.EqualTo(48));
            Assert.That(artwork.sprite.pixelsPerUnit,Is.EqualTo(32f));
            Assert.That(artwork.bounds.size.x,Is.EqualTo(1f).Within(.001f));
            Assert.That(artwork.bounds.size.y,Is.EqualTo(1.5f).Within(.001f));
        }
        [Test] public void Animator_HasAllFivePixelStatesAssigned()
        {
            foreach(string n in new[]{"standing","floatFrame","walkA","walkB","jump","death"})
            {var sprite=(Sprite)typeof(CharacterPixelAnimator).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(animator);Assert.That(sprite,Is.Not.Null,n);}
        }
        [Test] public void GroundedIdle_UsesStandingFrame()
        {
            animator.Tick(false,true,false,0f,Vector2.zero,Vector2.up,0f);
            Assert.That(animator.CurrentState,Is.EqualTo("Standing"));
            Assert.That(artwork.sprite.name,Does.StartWith("Idle_"));
            Sprite first=artwork.sprite;
            animator.Tick(false,true,false,0f,Vector2.zero,Vector2.up,.11f);
            Assert.That(artwork.sprite,Is.Not.SameAs(first));
        }
        [Test] public void WalkInput_AnimatesEvenWhenPlayerIsBlockedByWall()
        {
            animator.Tick(false,true,false,1f,Vector2.zero,Vector2.up,0f);
            Assert.That(animator.CurrentState,Is.EqualTo("Walking"));
            Sprite first=artwork.sprite;
            animator.Tick(false,true,false,1f,Vector2.zero,Vector2.up,.11f);
            Assert.That(artwork.sprite.name,Does.StartWith("Walk_"));
            Assert.That(artwork.sprite,Is.Not.SameAs(first));
        }
        [Test] public void Jump_FloatAndDeathChooseCorrectFrames()
        {
            animator.Tick(false,false,false,0f,Vector2.up,Vector2.up,0f);
            Assert.That(animator.CurrentState,Is.EqualTo("Jumping"));
            Assert.That(artwork.sprite.name,Does.StartWith("Jump_"));
            animator.Tick(false,false,true,0f,Vector2.zero,Vector2.up,0f);
            Assert.That(animator.CurrentState,Is.EqualTo("Floating"));
            Sprite first=artwork.sprite;
            animator.Tick(false,false,true,0f,Vector2.zero,Vector2.up,.11f);
            Assert.That(artwork.sprite,Is.Not.SameAs(first));
            animator.Tick(true,false,true,0f,Vector2.zero,Vector2.up,0f);
            Assert.That(artwork.sprite.name,Is.EqualTo("Death_00"));
            animator.Tick(true,false,true,0f,Vector2.zero,Vector2.up,5f);
            Assert.That(artwork.sprite.name,Is.EqualTo("Death_06"));
            animator.Tick(false,true,false,0f,Vector2.zero,Vector2.up,0f);
            Assert.That(animator.CurrentState,Is.EqualTo("Standing"));
            Assert.That(artwork.sprite.name,Is.EqualTo("Idle_00"));
        }
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
    }
}
