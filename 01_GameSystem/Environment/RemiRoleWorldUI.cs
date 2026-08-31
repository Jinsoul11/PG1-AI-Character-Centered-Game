using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Remi 头顶 RoleCanvas：未开始对话且玩家在范围内只显示 <b>Tip</b>；开始对话后只显示 <b>Response</b>。
/// 可挂在 RoleCanvas 上；Tip/Response 未绑定时会按子物体名称自动查找。
/// </summary>
public class RemiRoleWorldUI : MonoBehaviour
{
    [SerializeField] private GameObject tipRoot;
    [SerializeField] private GameObject responseRoot;

    [Tooltip("Tip 下显示「按 F…」的 TMP；可与 RemiInteraction.worldPromptText 为同一引用")]
    [SerializeField] private TMP_Text tipLine;

    [Tooltip("Response 子层级里曾用于面向相机的脚本，启动时自动禁用")]
    [SerializeField] private MonoBehaviour[] disableOnResponseAwake;

    public TMP_Text TipLine => tipLine;
    public TMP_Text ResponseLine { get; private set; }

    private bool _storyPlaying;
    private bool _suppressResponseVisual;
    private bool _whisperReplyVisible;

    private RemiResponseTextLayout _responseTextLayout;

    private void Awake()
    {
        AutoWireRootsAndTip();
        WireResponseLine();
        EnsureResponseTextLayout();
        DisableFacingScriptsOnResponse();
        ApplyStoryPlaying(false);
        ApplyState(false, false);
    }

    private void AutoWireRootsAndTip()
    {
        if (tipRoot == null || responseRoot == null)
        {
            foreach (Transform c in transform)
            {
                if (tipRoot == null && c.name.Equals("Tip", StringComparison.OrdinalIgnoreCase))
                    tipRoot = c.gameObject;
                if (responseRoot == null && c.name.Equals("Response", StringComparison.OrdinalIgnoreCase))
                    responseRoot = c.gameObject;
            }
        }

        if (tipLine == null && tipRoot != null)
            tipLine = tipRoot.GetComponentInChildren<TMP_Text>(true);
    }

    private void WireResponseLine()
    {
        if (responseRoot == null) return;
        foreach (TMP_Text tmp in responseRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.CompareTag("NPCPromptText"))
            {
                ResponseLine = tmp;
                return;
            }
        }

        ResponseLine = responseRoot.GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>剧情演出期间：Tip 与 Response 全部隐藏；结束后由 ApplyState 再决定显隐。</summary>
    public void ApplyStoryPlaying(bool storyPlaying)
    {
        _storyPlaying = storyPlaying;
        if (!storyPlaying)
            return;

        if (tipRoot != null)
            tipRoot.SetActive(false);
        if (responseRoot != null)
            responseRoot.SetActive(false);
    }

    private void DisableFacingScriptsOnResponse()
    {
        if (responseRoot == null) return;

        if (disableOnResponseAwake != null)
        {
            foreach (MonoBehaviour b in disableOnResponseAwake)
            {
                if (b != null) b.enabled = false;
            }
        }

        foreach (RemiTipBillboard bill in responseRoot.GetComponentsInChildren<RemiTipBillboard>(true))
        {
            if (bill != null) bill.enabled = false;
        }
    }

    public void SetSuppressResponseVisual(bool suppress)
    {
        _suppressResponseVisual = suppress;
    }

    /// <param name="playerInRange">玩家在交互范围内</param>
    /// <param name="dialogueOpen">已开始对话（RemiInteraction 正在对话）</param>
    public void ApplyState(bool playerInRange, bool dialogueOpen)
    {
        if (_storyPlaying)
            return;

        bool showResponse = (dialogueOpen || _whisperReplyVisible) && !_suppressResponseVisual;
        if (responseRoot != null)
            responseRoot.SetActive(showResponse);
        if (tipRoot != null)
            tipRoot.SetActive(playerInRange && !dialogueOpen && !_whisperReplyVisible);
    }

    /// <summary>自习点问：不开 DialoguePanel，仅亮 Response 气泡。</summary>
    public void SetWhisperReplyVisible(bool visible)
    {
        _whisperReplyVisible = visible;
        if (!visible && ResponseLine != null)
            ResponseLine.text = string.Empty;
    }

    public void SetTipPrompt(string text)
    {
        if (tipLine != null)
            tipLine.text = text ?? string.Empty;
    }

    public void SetResponseText(string text)
    {
        if (ResponseLine != null)
        {
            ResponseLine.richText = true;
            ResponseLine.text = text ?? string.Empty;
        }

        EnsureResponseTextLayout();
        _responseTextLayout?.RefreshLayout();
    }

    private void EnsureResponseTextLayout()
    {
        if (_responseTextLayout != null)
            return;

        if (responseRoot != null)
        {
            _responseTextLayout = responseRoot.GetComponent<RemiResponseTextLayout>();
            if (_responseTextLayout == null)
                _responseTextLayout = responseRoot.AddComponent<RemiResponseTextLayout>();
            return;
        }

        if (ResponseLine != null)
            _responseTextLayout = ResponseLine.GetComponentInParent<RemiResponseTextLayout>();
    }
}
