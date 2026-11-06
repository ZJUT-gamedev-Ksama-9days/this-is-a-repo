using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>Short non-blocking message, e.g. "存档已保存 01' 23''" or "已恢复存档".</summary>
    [DisallowMultipleComponent]
    public sealed class ToastView : MonoBehaviour
    {
        private const float FadeSpeed = 3.5f;

        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text messageLabel;

        private float hideAt;
        private bool showing;

        public void Show(string message, float seconds = 1.8f)
        {
            EnsureReferences();
            if (messageLabel != null)
            {
                messageLabel.text = message;
            }

            gameObject.SetActive(true);
            hideAt = Time.unscaledTime + Mathf.Max(0.2f, seconds);
            showing = true;
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        public void Hide()
        {
            showing = false;
            gameObject.SetActive(false);
        }

        private void Awake()
        {
            EnsureReferences();
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        private void Update()
        {
            if (!showing)
            {
                return;
            }

            if (Time.unscaledTime < hideAt)
            {
                return;
            }

            float alpha = group != null ? group.alpha : 1f;
            alpha = Mathf.Max(0f, alpha - Time.unscaledDeltaTime * FadeSpeed);
            if (group != null)
            {
                group.alpha = alpha;
            }

            if (alpha <= 0.001f)
            {
                showing = false;
                gameObject.SetActive(false);
            }
        }

        private void EnsureReferences()
        {
            if (group == null)
            {
                group = GetComponent<CanvasGroup>();
            }

            if (messageLabel == null)
            {
                messageLabel = UiLookup.FindDeep<Text>(transform, "Toast Message");
            }
        }
    }
}
