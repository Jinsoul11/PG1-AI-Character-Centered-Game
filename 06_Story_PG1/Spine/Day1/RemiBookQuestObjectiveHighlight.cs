using UnityEngine;

/// <summary>
/// 挂在任务书本上：当 <see cref="RemiBookQuestFlow.AwaitsBookPickup"/> 为真时显示「要找的书」提示（子物体光晕 / 可选点光脉冲）；
/// 玩家检视拿到书或任务结束后自动隐藏。无需改 <see cref="RemiBookQuestFlow"/>。
/// </summary>
[DisallowMultipleComponent]
public class RemiBookQuestObjectiveHighlight : MonoBehaviour
{
    [Tooltip("亮点根物体（例如带自发光的球体、粒子子物体）；留空则尝试查找子物体名为 Highlight / Glow")]
    [SerializeField] private GameObject highlightRoot;

    [Tooltip("可选：额外点光源脉冲（建议 Range 小、Intensity 中等）。")]
    [SerializeField] private Light highlightPointLight;

    [SerializeField] private float pulseHz = 1.15f;
    [SerializeField] private Vector2 lightIntensityRange = new Vector2(0.35f, 1.4f);
    [Tooltip("相对初始缩放的脉冲倍率最小/最大。")]
    [SerializeField] private Vector2 scalePulseMultiplier = new Vector2(0.92f, 1.08f);

    private Transform _pulseTransform;
    private Vector3 _baseScale = Vector3.one;

    private void Awake()
    {
        if (highlightRoot == null)
        {
            Transform t = transform.Find("Highlight") ?? transform.Find("Glow");
            if (t != null) highlightRoot = t.gameObject;
        }

        if (highlightRoot != null)
        {
            _pulseTransform = highlightRoot.transform;
            _baseScale = _pulseTransform.localScale;
        }
    }

    private void Update()
    {
        RemiBookQuestFlow flow = RemiBookQuestFlow.Instance;
        bool show = flow != null && flow.AwaitsBookPickup();

        if (highlightRoot != null && highlightRoot.activeSelf != show)
            highlightRoot.SetActive(show);

        if (!show)
        {
            if (highlightPointLight != null)
                highlightPointLight.enabled = false;
            return;
        }

        float t = Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f * Mathf.Max(0.05f, pulseHz))) * 0.5f + 0.5f;

        if (highlightPointLight != null)
        {
            highlightPointLight.enabled = true;
            highlightPointLight.intensity = Mathf.Lerp(lightIntensityRange.x, lightIntensityRange.y, t);
        }

        if (_pulseTransform != null)
        {
            float s = Mathf.Lerp(scalePulseMultiplier.x, scalePulseMultiplier.y, t);
            _pulseTransform.localScale = _baseScale * s;
        }
    }
}
