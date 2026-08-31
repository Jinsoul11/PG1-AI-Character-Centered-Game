using TMPro;
using UnityEngine;

/// <summary>
/// Remi 世界气泡（Response）：按 TMP 文案 preferred 宽高自适应背景框。
/// 挂在 Response 根（带底图）上；缺失时由 <see cref="RemiRoleWorldUI"/> 运行时补挂。
/// </summary>
[DisallowMultipleComponent]
public class RemiResponseTextLayout : MonoBehaviour
{
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private RectTransform container;

    [Tooltip("气泡最大宽度（UI 单位，RoleCanvas 未缩放前）")]
    [SerializeField] private float maxWidth = 520f;

    [SerializeField] private float minWidth = 140f;
    [SerializeField] private float minHeight = 56f;
    [SerializeField] private float maxHeight = 360f;

    [Tooltip("左 / 上 / 右 / 下内边距")]
    [SerializeField] private Vector4 padding = new Vector4(18f, 14f, 18f, 14f);

    [Tooltip("短句时收窄宽度；长句顶到 maxWidth 后换行增高")]
    [SerializeField] private bool shrinkWidthToContent = true;

    private bool _containerModeReady;

    private void Reset()
    {
        container = transform as RectTransform;
        targetText = GetComponentInChildren<TMP_Text>(true);
    }

    private void Awake()
    {
        EnsureRefs();
    }

    public void RefreshLayout()
    {
        EnsureRefs();
        if (targetText == null || container == null)
            return;

        EnsureContainerMode();
        ApplyTextInsets();

        string raw = targetText.text;
        if (string.IsNullOrEmpty(raw))
        {
            container.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, minWidth);
            container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, minHeight);
            Canvas.ForceUpdateCanvases();
            return;
        }

        float innerMaxW = Mathf.Max(1f, maxWidth - padding.x - padding.z);
        Vector2 preferred = targetText.GetPreferredValues(raw, innerMaxW, float.PositiveInfinity);
        float prefW = Mathf.Max(1f, preferred.x);
        float prefH = Mathf.Max(1f, preferred.y);

        float width = maxWidth;
        if (shrinkWidthToContent)
            width = Mathf.Clamp(prefW + padding.x + padding.z, minWidth, maxWidth);

        // 收窄后按实际内容宽再量一次高度（避免按 maxWidth 换行偏高）
        float innerW = Mathf.Max(1f, width - padding.x - padding.z);
        preferred = targetText.GetPreferredValues(raw, innerW, float.PositiveInfinity);
        prefH = Mathf.Max(1f, preferred.y);

        float height = Mathf.Clamp(prefH + padding.y + padding.w, minHeight, maxHeight);

        container.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        container.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        targetText.ForceMeshUpdate(true);
        Canvas.ForceUpdateCanvases();
    }

    private void EnsureRefs()
    {
        if (container == null)
            container = transform as RectTransform;

        if (targetText == null)
        {
            foreach (TMP_Text tmp in GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp != null && tmp.CompareTag("NPCPromptText"))
                {
                    targetText = tmp;
                    break;
                }
            }
        }

        if (targetText == null)
            targetText = GetComponentInChildren<TMP_Text>(true);
    }

    /// <summary>
    /// Response 预制体原先 stretch 铺满 RoleCanvas，sizeDelta 无法表示内容高度；
    /// 改为居中锚点 + 显式宽高。
    /// </summary>
    private void EnsureContainerMode()
    {
        if (_containerModeReady || container == null)
            return;

        Vector2 pos = container.anchoredPosition;
        container.anchorMin = new Vector2(0.5f, 0.5f);
        container.anchorMax = new Vector2(0.5f, 0.5f);
        container.pivot = new Vector2(0.5f, 0.5f);
        container.anchoredPosition = pos;
        _containerModeReady = true;
    }

    private void ApplyTextInsets()
    {
        RectTransform textRt = targetText.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.pivot = new Vector2(0.5f, 0.5f);
        textRt.offsetMin = new Vector2(padding.x, padding.w);
        textRt.offsetMax = new Vector2(-padding.z, -padding.y);

        targetText.enableWordWrapping = true;
        targetText.overflowMode = TextOverflowModes.Overflow;
    }
}
