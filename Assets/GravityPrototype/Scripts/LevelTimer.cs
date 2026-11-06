using System;
using UnityEngine;

namespace GravityPrototype
{
    /// <summary>Per-run stopwatch for one level. It only measures; the level controller decides when to stop it.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelTimer : MonoBehaviour
    {
        [SerializeField, Tooltip("启用后自动开始计时；关闭时由关卡控制器调用 BeginRun。")]
        private bool beginAutomatically = true;
        [SerializeField, Min(0f), Tooltip("进入关卡后开始计时前的准备时间，秒。")]
        private float startDelay;
        [SerializeField, Tooltip("使用不受 timeScale 影响的时间，暂停菜单不会影响成绩。")]
        private bool useUnscaledTime = true;

        private double startTime;
        private double finishedSeconds;

        public event Action<LevelTimer> Finished;

        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }
        public bool HasFinished { get; private set; }

        /// <summary>Seconds of the current run, or the final time once stopped.</summary>
        public float ElapsedSeconds =>
            IsRunning && !IsPaused ? (float)Math.Max(0.0, GetNow() - startTime) : (float)finishedSeconds;

        private double GetNow()
        {
            return useUnscaledTime ? Time.unscaledTimeAsDouble : Time.timeAsDouble;
        }

        private void Start()
        {
            if (beginAutomatically)
            {
                BeginRun();
            }
        }

        public void BeginRun()
        {
            startTime = GetNow() + Math.Max(0.0, startDelay);
            finishedSeconds = 0.0;
            IsRunning = true;
            IsPaused = false;
            HasFinished = false;
        }

        /// <summary>Stops the clock once and returns the final time. Later calls return the same value.</summary>
        public float Stop()
        {
            if (!IsRunning)
            {
                return (float)finishedSeconds;
            }

            finishedSeconds = ElapsedSeconds;
            IsRunning = false;
            IsPaused = false;
            HasFinished = true;
            Finished?.Invoke(this);
            return (float)finishedSeconds;
        }

        /// <summary>Clears the clock without starting it, e.g. while a death restart is pending.</summary>
        public void Pause()
        {
            if (!IsRunning || IsPaused) return;
            finishedSeconds = ElapsedSeconds;
            IsPaused = true;
        }

        public void Resume()
        {
            if (!IsRunning || !IsPaused) return;
            startTime = GetNow() - finishedSeconds;
            IsPaused = false;
        }

        public void ResumeRunAt(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0f) return;
            finishedSeconds = seconds;
            startTime = GetNow() - seconds;
            IsRunning = true;
            IsPaused = false;
            HasFinished = false;
        }

        public void ResetTimer()
        {
            IsRunning = false;
            IsPaused = false;
            HasFinished = false;
            finishedSeconds = 0.0;
            startTime = GetNow();
        }
    }
}
