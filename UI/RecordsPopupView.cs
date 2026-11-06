using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GravityPrototype.UI
{
    /// <summary>
    /// Shared records popup. The same window is reused by the side menu (browse), by a single level
    /// victory (next level / quit) and by the final victory (restart / quit); only the footer changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RecordsPopupView : MonoBehaviour
    {
        [SerializeField] private Text titleLabel;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private LevelRecordRowView rowTemplate;
        [SerializeField] private Button primaryButton;
        [SerializeField] private Text primaryLabel;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button closeButton;

        private readonly List<LevelRecordRowView> rowPool = new List<LevelRecordRowView>();
        private bool listenersBound;

        public event Action PrimaryClicked;
        public event Action QuitClicked;
        public event Action CloseClicked;

        public RecordsPopupMode Mode { get; private set; }
        public bool IsVisible => gameObject.activeSelf;

        public void Show(RecordsPopupMode mode, IReadOnlyList<LevelRecordRowData> rows)
        {
            EnsureReferences();
            Mode = mode;
            gameObject.SetActive(true);
            ApplyTitle(mode);
            FillRows(rows);
            ApplyFooter(mode);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void ApplyTitle(RecordsPopupMode mode)
        {
            if (titleLabel == null)
            {
                return;
            }

            switch (mode)
            {
                case RecordsPopupMode.LevelVictory:
                    titleLabel.text = "关卡完成 · 当前记录";
                    break;
                case RecordsPopupMode.FinalVictory:
                    titleLabel.text = "全部关卡完成 · 当前记录";
                    break;
                default:
                    titleLabel.text = "当前记录";
                    break;
            }
        }

        private void ApplyFooter(RecordsPopupMode mode)
        {
            bool isBrowse = mode == RecordsPopupMode.Browse;
            SetButtonVisible(primaryButton, !isBrowse);
            SetButtonVisible(quitButton, !isBrowse);
            SetButtonVisible(closeButton, isBrowse);

            if (primaryLabel == null)
            {
                return;
            }

            primaryLabel.text = mode == RecordsPopupMode.FinalVictory ? "重新开始" : "下一关";
        }

        private void FillRows(IReadOnlyList<LevelRecordRowData> rows)
        {
            if (rowContainer == null || rowTemplate == null)
            {
                return;
            }

            int count = rows != null ? rows.Count : 0;
            EnsureRowCount(count);
            for (int i = 0; i < rowPool.Count; i++)
            {
                bool used = i < count;
                rowPool[i].gameObject.SetActive(used);
                if (used)
                {
                    rowPool[i].SetData(rows[i]);
                }
            }
        }

        private void EnsureRowCount(int count)
        {
            while (rowPool.Count < count)
            {
                LevelRecordRowView row = Instantiate(rowTemplate, rowContainer);
                row.gameObject.name = "Row " + (rowPool.Count + 1);
                row.gameObject.SetActive(true);
                rowPool.Add(row);
            }
        }

        private static void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.gameObject.SetActive(visible);
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
                titleLabel = UiLookup.FindDeep<Text>(transform, "Records Title");
            }

            if (rowContainer == null)
            {
                RectTransform candidate = UiLookup.FindDeep<RectTransform>(transform, "Rows");
                if (candidate != null)
                {
                    rowContainer = candidate;
                }
            }

            if (rowTemplate == null && rowContainer != null)
            {
                rowTemplate = UiLookup.Find<LevelRecordRowView>(rowContainer, "Row Template");
            }

            if (primaryButton == null)
            {
                primaryButton = UiLookup.FindDeep<Button>(transform, "Primary Button");
            }

            if (primaryLabel == null && primaryButton != null)
            {
                primaryLabel = primaryButton.GetComponentInChildren<Text>(true);
            }

            if (quitButton == null)
            {
                quitButton = UiLookup.FindDeep<Button>(transform, "Quit Button");
            }

            if (closeButton == null)
            {
                closeButton = UiLookup.FindDeep<Button>(transform, "Close Button");
            }

            if (listenersBound)
            {
                return;
            }

            listenersBound = true;
            if (primaryButton != null)
            {
                primaryButton.onClick.AddListener(HandlePrimary);
            }

            if (quitButton != null)
            {
                quitButton.onClick.AddListener(HandleQuit);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(HandleClose);
            }
        }

        private void HandlePrimary()
        {
            PrimaryClicked?.Invoke();
        }

        private void HandleQuit()
        {
            QuitClicked?.Invoke();
        }

        private void HandleClose()
        {
            CloseClicked?.Invoke();
        }
    }
}
