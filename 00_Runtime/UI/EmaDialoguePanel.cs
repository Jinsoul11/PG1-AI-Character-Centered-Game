using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ema 的选项式对话面板。
/// 需要在 Resources/UI/ 下创建同名预制体 `EmaDialoguePanel` 并把字段拖引用。
/// </summary>
public class EmaDialoguePanel : BasePanel
{
    [Header("UI引用")]
    [SerializeField] private TMP_Text emaText;
    [SerializeField] private Transform optionsRoot;
    [SerializeField] private Button optionButtonPrefab;
    [SerializeField] private Button closeButton;

    [Header("可选：复用历史面板")]
    [SerializeField] private ChatHistoryPanel historyPanel;

    private readonly List<Button> _spawnedButtons = new List<Button>();
    private Ema _ema;

    public void Bind(Ema ema)
    {
        _ema = ema;
        _ema?.ResetDialogue();
        Refresh();
    }

    public override void Init()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(() =>
            {
                UiManager.Instance.HidePanel<EmaDialoguePanel>();
                UiManager.Instance.canvasObj.SetActive(false);
            });
        }

        // 可选：如果你希望 Ema 的对话也写入同一个历史面板
        if (historyPanel == null)
        {
            historyPanel = UiManager.Instance.GetPanel<ChatHistoryPanel>();
        }
    }

    private void Refresh()
    {
        ClearOptions();

        if (_ema == null)
        {
            if (emaText != null) emaText.text = "（Ema 未绑定）";
            return;
        }

        var node = _ema.GetCurrentNode();
        if (node == null)
        {
            if (emaText != null) emaText.text = "（没有可用对话）";
            return;
        }

        if (emaText != null) emaText.text = node.emaLine ?? string.Empty;

        if (node.options == null || node.options.Count == 0)
        {
            SpawnOption("（结束）", () => UiManager.Instance.HidePanel<EmaDialoguePanel>());
            return;
        }

        for (int i = 0; i < node.options.Count; i++)
        {
            int idx = i;
            string label = string.IsNullOrWhiteSpace(node.options[i].label) ? $"选项 {i + 1}" : node.options[i].label;
            SpawnOption(label, () => OnOptionClicked(idx));
        }
    }

    private void OnOptionClicked(int optionIndex)
    {
        if (_ema == null) return;

        if (_ema.TrySelectOption(optionIndex, out var playerLine, out var emaReply))
        {
            // 写入历史（如果存在）
            if (historyPanel != null)
            {
                historyPanel.AddChatItem("user", playerLine);
                if (!string.IsNullOrWhiteSpace(emaReply))
                {
                    historyPanel.AddChatItem("Ema", emaReply);
                }
            }

            // 在面板上展示回复：优先显示 emaReply；否则显示当前节点台词
            if (emaText != null)
            {
                emaText.text = string.IsNullOrWhiteSpace(emaReply) ? (_ema.GetCurrentNode()?.emaLine ?? "") : emaReply;
            }

            // 如果跳转到了新节点，刷新选项
            Refresh();
        }
    }

    private void SpawnOption(string label, UnityEngine.Events.UnityAction onClick)
    {
        if (optionsRoot == null || optionButtonPrefab == null) return;

        var btn = Instantiate(optionButtonPrefab, optionsRoot);
        var txt = btn.GetComponentInChildren<TMP_Text>(true);
        if (txt != null) txt.text = label;
        btn.onClick.AddListener(onClick);
        _spawnedButtons.Add(btn);
    }

    private void ClearOptions()
    {
        foreach (var b in _spawnedButtons)
        {
            if (b != null) Destroy(b.gameObject);
        }
        _spawnedButtons.Clear();
    }
}

