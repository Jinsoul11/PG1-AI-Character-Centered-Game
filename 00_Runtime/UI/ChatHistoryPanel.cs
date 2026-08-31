using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Text;

[System.Serializable]
public class ChatRecord
{
    public string role;
    public string content;
}
public class ChatHistoryPanel : BasePanel
{
    [Header("UI引用")]
    public ScrollRect chatScrollRect; // 滚动容器
    public Transform contentTransform; // 对话条目父物体（Content）
    public TMP_Text chatItemPrefab; // 对话条目预制体
    public Button toBottom;
    public Button clearHistory;
    public Button close;
    public Button test;

    [Header("样式配置")]
    public Color playerColor = new Color(0.2f, 0.6f, 1f); // 玩家蓝色
    public Color aiColor = new Color(0.1f, 0.8f, 0.1f); // AI绿色
    public float itemMaxWidth = 400f;

    private const int MaxHistoryCount = 50; // 最大显示 / 保存条数

    private List<TMP_Text> _chatItems = new List<TMP_Text>();
    // 用来保存到本地的纯数据
    private List<ChatRecord> _historyRecords = new List<ChatRecord>();

    public override void Init()
    {
        LoadHistory();

        // 绑定按钮事件
        toBottom.onClick.AddListener(ScrollToBottom);
        clearHistory.onClick.AddListener(() =>
        {
            if (DeepSeekDialogueManager.Instance != null)
                DeepSeekDialogueManager.Instance.EndSessionCaptureAndClear(RemiInteractionChannel.FaceToFace);
            ClearChatHistory();
        });

        close.onClick.AddListener(() =>
        {
            var hp = UiManager.Instance.GetPanel<ChatHistoryPanel>();
            if (hp != null)
                hp.gameObject.SetActive(false);

            var dp = UiManager.Instance.GetPanel<DialoguePanel>();
            if (dp != null)
            {
                dp.gameObject.SetActive(true);
                dp.ShowMe();
            }
            else
                UiManager.Instance.ShowPanel<DialoguePanel>();
        });

        test.onClick.AddListener(CopyAllConversationText);
    }

    /// <summary>
    /// 修复后的AddChatItem：确保对齐和尺寸生效
    /// </summary>
    /// <param name="role">user/Remi</param>
    /// <param name="content">对话内容</param>
    public void AddChatItem(string role, string content, bool saveToFile = true)
    {
        //Debug.Log($"[History] AddChatItem role={role}, content={content}, saveToFile={saveToFile}");

        // 如果已经达到最大条数，移除最前面的显示项（覆盖旧内容）
        if (chatItemPrefab != null && _chatItems.Count >= MaxHistoryCount)
        {
            var oldestItem = _chatItems[0];
            if (oldestItem != null)
            {
                Destroy(oldestItem.gameObject);
            }
            _chatItems.RemoveAt(0);
        }

        // 1~4. 创建并显示 UI（如果你仍在用旧历史面板的 ScrollView）
        if (chatItemPrefab != null && contentTransform != null)
        {
            TMP_Text newItem = Instantiate(chatItemPrefab, contentTransform);
            newItem.text = $"{role}: {content}";
            newItem.color = role == "user" ? playerColor : aiColor;
            newItem.alignment = role == "user" ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            var rt = newItem.rectTransform;
            float w = Mathf.Max(1f, itemMaxWidth);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
            newItem.ForceMeshUpdate(true);
            float h = Mathf.Max(newItem.preferredHeight, 1f);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            _chatItems.Add(newItem);
        }

        // 5. 保存到本地的纯数据
        if (saveToFile)
        {
            // 先保证历史记录不超过上限（从前面开始覆盖）
            if (_historyRecords.Count >= MaxHistoryCount)
            {
                _historyRecords.RemoveAt(0);
            }

            _historyRecords.Add(new ChatRecord
            {
                role = role,
                content = content
            });

            // 把整个列表存成一份 Json
            JsonMgr.Instance.SaveData(_historyRecords, "Conversation1");
        }

        // 6. 延迟滚动到底部（等待布局刷新）
        if (chatScrollRect != null)
        {
            Invoke(nameof(ScrollToBottom), 0.05f);
        }
    }

    /// <summary>从当前内存历史从后往前找最后一条 Remi 发言（与存档一致）。</summary>
    public bool TryGetLastRemiLine(out string content)
    {
        for (int i = _historyRecords.Count - 1; i >= 0; i--)
        {
            ChatRecord r = _historyRecords[i];
            if (r == null || string.IsNullOrEmpty(r.role)) continue;
            if (string.Equals(r.role, "Remi", System.StringComparison.OrdinalIgnoreCase))
            {
                content = r.content ?? string.Empty;
                return true;
            }
        }

        content = null;
        return false;
    }

    public void LoadHistory()
    {
        Debug.Log("读取历史");
        // 从本地读出 List<ChatRecord>
        var loaded = JsonMgr.Instance.LoadData<List<ChatRecord>>("Conversation1");
        if (loaded == null || loaded.Count == 0)
        {
            return;
        }

        // 只保留最新的 MaxHistoryCount 条
        if (loaded.Count > MaxHistoryCount)
        {
            int startIndex = loaded.Count - MaxHistoryCount;
            _historyRecords = loaded.GetRange(startIndex, MaxHistoryCount);
        }
        else
        {
            _historyRecords = loaded;
        }

        // 重新创建 UI，但不再保存到文件（saveToFile = false）
        foreach (var record in _historyRecords)
        {
            AddChatItem(record.role, record.content, false);
        }
    }

    /// <summary>按磁盘 Conversation1 重建面板（读档后同步 UI，不删文件）。</summary>
    public void ReloadChatFromStorage()
    {
        foreach (var item in _chatItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }

        _chatItems.Clear();
        _historyRecords.Clear();
        LoadHistory();
    }

    private void ScrollToBottom()
    {
        if (chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases(); // 强制刷新布局
            chatScrollRect.verticalNormalizedPosition = 0f; // 0=底部
        }
    }

    public void ClearChatHistory()
    {
        foreach (var item in _chatItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        _chatItems.Clear();
        _historyRecords.Clear();
        JsonMgr.Instance.DeleteData("Conversation1");
    }

    /// <summary>将全部对话记录复制到系统剪贴板。</summary>
    public void CopyAllConversationText()
    {
        if (_historyRecords == null || _historyRecords.Count == 0)
        {
            Debug.Log("[ChatHistory] 无对话记录可复制。");
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < _historyRecords.Count; i++)
        {
            ChatRecord record = _historyRecords[i];
            if (record == null || string.IsNullOrWhiteSpace(record.content))
                continue;

            string role = string.IsNullOrWhiteSpace(record.role) ? "unknown" : record.role.Trim();
            if (sb.Length > 0)
                sb.AppendLine();
            sb.Append(role).Append(": ").Append(record.content.Trim());
        }

        if (sb.Length == 0)
        {
            Debug.Log("[ChatHistory] 无有效对话内容可复制。");
            return;
        }

        GUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log($"[ChatHistory] 已复制 {_historyRecords.Count} 条对话到剪贴板。");
    }
}
