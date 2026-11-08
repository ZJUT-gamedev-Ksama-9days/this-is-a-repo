using System;
using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>
    /// 失败提示弹窗（设计文档 2.8.2.3）：角色死亡后弹出，“再来一次”重开本关，“结束游戏”退出。
    /// 它只抛意图，冻结与场景切换由 LevelFlowController 负责。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefeatPopupView : MonoBehaviour
    {
        [SerializeField] private Text titleLabel;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button quitButton;

        private bool listenersBound;

        public event Action RetryClicked;
        public event Action QuitClicked;

        public bool IsVisible => gameObject.activeSelf;

        public void Show()
        {
            EnsureReferences();
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetTitle(string text)
        {
            EnsureReferences();
            if (titleLabel != null)
            {
                titleLabel.text = string.IsNullOrEmpty(text) ? "角色已失事" : text;
            }
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void EnsureReferences()
        {
            if (titleLabel == null)
            {
                titleLabel = UiLookup.FindDeep<Text>(transform, "Defeat Title");
            }

            if (retryButton == null)
            {
                retryButton = UiLookup.FindDeep<Button>(transform, "Retry Button");
            }

            if (quitButton == null)
            {
                quitButton = UiLookup.FindDeep<Button>(transform, "Quit Button");
            }

            if (listenersBound)
            {
                return;
            }

            listenersBound = true;
            if (retryButton != null)
            {
                retryButton.onClick.AddListener(HandleRetry);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(HandleQuit);
            }
        }

        private void HandleRetry()
        {
            RetryClicked?.Invoke();
        }

        private void HandleQuit()
        {
            QuitClicked?.Invoke();
        }
    }
}
