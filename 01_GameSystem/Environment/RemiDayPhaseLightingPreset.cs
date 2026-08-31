using System;
using UnityEngine;

/// <summary>
/// 单个叙事时段的光照：仅调整主方向光 localEulerAngles.x（仰角）。
/// </summary>
[Serializable]
public class RemiDayPhaseLightingPreset
{
    [Tooltip("Directional Light 本地 X 轴仰角：高=偏正午，低=晨昏，负=低于地平线（夜间）。")]
    public float sunRotationX = 35f;

    [Header("可选：时段专属物体（如台灯）")]
    public GameObject[] activateObjects;

    public void ApplySunRotation(Light sun)
    {
        if (sun == null)
            return;

        sun.enabled = true;
        Vector3 euler = sun.transform.localEulerAngles;
        sun.transform.localEulerAngles = new Vector3(sunRotationX, euler.y, euler.z);
    }

    public static RemiDayPhaseLightingPreset DefaultMorning() => new() { sunRotationX = 25f };

    public static RemiDayPhaseLightingPreset DefaultAfternoon() => new() { sunRotationX = 65f };

    public static RemiDayPhaseLightingPreset DefaultEvening() => new() { sunRotationX = 12f };

    public static RemiDayPhaseLightingPreset DefaultNight() => new() { sunRotationX = -18f };
}
