using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>
    /// Victory flourish played before the records popup: a scaled-in banner plus a burst of UI sparks.
    /// All timing uses unscaled time because the level flow pauses the game while the celebration runs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VictoryEffectView : MonoBehaviour
    {
        private const float IntroSeconds = 0.35f;
        private const int SparkCount = 18;
        private const float SparkSeconds = 0.9f;

        [SerializeField] private CanvasGroup bannerGroup;
        [SerializeField] private RectTransform banner;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text subtitleLabel;
        [SerializeField] private RectTransform sparkContainer;
        [SerializeField] private Image sparkTemplate;

        private readonly List<Image> sparkPool = new List<Image>();
        private Coroutine sequence;
        private int sparkCursor;

        public bool IsPlaying => sequence != null;

        public void Play(string title, string subtitle, float holdSeconds)
        {
            EnsureReferences();
            gameObject.SetActive(true);
            if (titleLabel != null)
            {
                titleLabel.text = title;
            }

            if (subtitleLabel != null)
            {
                subtitleLabel.text = subtitle;
            }

            StopSequence();
            sequence = StartCoroutine(RunSequence(Mathf.Max(0.2f, holdSeconds)));
        }

        public void Hide()
        {
            StopSequence();
            HideSparks();
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            EnsureReferences();
            HideSparks();
        }

        private void OnDisable()
        {
            StopSequence();
            HideSparks();
        }

        private IEnumerator RunSequence(float holdSeconds)
        {
            SetBannerProgress(0f);
            float elapsed = 0f;
            while (elapsed < IntroSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetBannerProgress(Mathf.Clamp01(elapsed / IntroSeconds));
                yield return null;
            }

            SetBannerProgress(1f);
            StartCoroutine(AnimateSparks());
            yield return new WaitForSecondsRealtime(holdSeconds);
            sequence = null;
        }

        private IEnumerator AnimateSparks()
        {
            if (sparkContainer == null || sparkTemplate == null)
            {
                yield break;
            }

            List<Image> active = new List<Image>(SparkCount);
            List<Vector2> directions = new List<Vector2>(SparkCount);
            List<float> distances = new List<float>(SparkCount);
            for (int i = 0; i < SparkCount; i++)
            {
                Image spark = RentSpark();
                float angle = (i / (float)SparkCount) * Mathf.PI * 2f;
                directions.Add(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                distances.Add(Random.Range(180f, 460f));
                spark.rectTransform.anchoredPosition = Vector2.zero;
                spark.color = new Color(0.55f, 1f, 0.75f, 1f);
                active.Add(spark);
            }

            float elapsed = 0f;
            while (elapsed < SparkSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / SparkSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                for (int i = 0; i < active.Count; i++)
                {
                    Image spark = active[i];
                    if (spark == null)
                    {
                        continue;
                    }

                    spark.rectTransform.anchoredPosition = directions[i] * (distances[i] * eased);
                    Color color = spark.color;
                    color.a = 1f - t;
                    spark.color = color;
                    spark.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.4f, t);
                }

                yield return null;
            }

            HideSparks();
        }

        private Image RentSpark()
        {
            while (sparkPool.Count <= sparkCursor)
            {
                Image spark = Instantiate(sparkTemplate, sparkContainer);
                spark.gameObject.name = "Spark " + (sparkPool.Count + 1);
                sparkPool.Add(spark);
            }

            Image rented = sparkPool[sparkCursor];
            sparkCursor++;
            rented.gameObject.SetActive(true);
            return rented;
        }

        private void HideSparks()
        {
            sparkCursor = 0;
            for (int i = 0; i < sparkPool.Count; i++)
            {
                if (sparkPool[i] != null)
                {
                    sparkPool[i].gameObject.SetActive(false);
                }
            }
        }

        private void SetBannerProgress(float progress)
        {
            if (bannerGroup != null)
            {
                bannerGroup.alpha = progress;
            }

            if (banner != null)
            {
                banner.localScale = Vector3.one * Mathf.SmoothStep(0.72f, 1f, progress);
            }
        }

        private void StopSequence()
        {
            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
            }

            StopAllCoroutines();
        }

        private void EnsureReferences()
        {
            if (bannerGroup == null)
            {
                bannerGroup = UiLookup.FindDeep<CanvasGroup>(transform, "Banner");
            }

            if (banner == null)
            {
                banner = UiLookup.FindDeep<RectTransform>(transform, "Banner");
            }

            if (titleLabel == null)
            {
                titleLabel = UiLookup.FindDeep<Text>(transform, "Victory Title");
            }

            if (subtitleLabel == null)
            {
                subtitleLabel = UiLookup.FindDeep<Text>(transform, "Victory Subtitle");
            }

            if (sparkContainer == null)
            {
                sparkContainer = UiLookup.FindDeep<RectTransform>(transform, "Sparks");
            }

            if (sparkTemplate == null)
            {
                sparkTemplate = UiLookup.FindDeep<Image>(transform, "Spark Template");
            }
        }
    }
}
