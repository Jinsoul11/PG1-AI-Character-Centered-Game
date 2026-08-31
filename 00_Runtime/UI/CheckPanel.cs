using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用检视/确认面板：展示说明文案，点击确认后执行一次性回调并关闭。
/// </summary>
public class CheckPanel : BasePanel
{
    public Button CheckButton;

    [Tooltip("留空则在子物体中查找第一个 TMP_Text。")]
    [SerializeField] private TMP_Text descriptionText;

    private Action _onConfirmOnce;

    public override void Init()
    {
        if (CheckButton != null)
        {
            CheckButton.onClick.RemoveAllListeners();
            CheckButton.onClick.AddListener(OnConfirmClicked);
        }
    }

    /// <summary>
    /// 在 <see cref="UiManager.ShowPanel{T}"/> 之后、下一帧调用（确保 <see cref="Init"/> 已绑定按钮）。
    /// </summary>
    public void ConfigureForInspect(string description, Action onConfirmed)
    {
        _onConfirmOnce = onConfirmed;

        if (descriptionText == null)
            descriptionText = GetComponentInChildren<TMP_Text>(true);

        if (descriptionText != null)
            descriptionText.text = description ?? string.Empty;
    }

    private void OnConfirmClicked()
    {
        Action cb = _onConfirmOnce;
        _onConfirmOnce = null;
        cb?.Invoke();
        UiManager.Instance.HidePanel<CheckPanel>();
    }
}
