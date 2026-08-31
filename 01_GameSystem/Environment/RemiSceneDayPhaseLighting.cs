using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在每个可玩场景：四档时段仅旋转 Directional Light 的 X 轴实现昼夜变化。
/// 由 <see cref="RemiDayPhaseLightingCoordinator"/> 在时段变化或场景加载后调用。
/// </summary>
[DisallowMultipleComponent]
public class RemiSceneDayPhaseLighting : MonoBehaviour
{
    private const string PreferredSunName = "Directional Light_A";

    [Header("主方向光（可空，优先找 Directional Light_A）")]
    [SerializeField] private Light sunLight;
    [SerializeField] private bool autoFindDirectionalLight = true;
    [Tooltip("Play 时关闭场景中其余 Directional，避免烘焙补光与运行时主光叠加。")]
    [SerializeField] private bool disableOtherDirectionalLights = true;

    [Header("四档 X 轴仰角")]
    [SerializeField] private RemiDayPhaseLightingPreset morning = RemiDayPhaseLightingPreset.DefaultMorning();
    [SerializeField] private RemiDayPhaseLightingPreset afternoon = RemiDayPhaseLightingPreset.DefaultAfternoon();
    [SerializeField] private RemiDayPhaseLightingPreset evening = RemiDayPhaseLightingPreset.DefaultEvening();
    [SerializeField] private RemiDayPhaseLightingPreset night = RemiDayPhaseLightingPreset.DefaultNight();

    private HashSet<GameObject> _phaseToggleObjects;

    public Light SunLight => sunLight;

    private void Reset()
    {
        morning = RemiDayPhaseLightingPreset.DefaultMorning();
        afternoon = RemiDayPhaseLightingPreset.DefaultAfternoon();
        evening = RemiDayPhaseLightingPreset.DefaultEvening();
        night = RemiDayPhaseLightingPreset.DefaultNight();
    }

    private void Awake()
    {
        EnsureDefaultPresetsIfEmpty();
        ResolveSunLight();
        DisableOtherDirectionalLights();
        RebuildPhaseToggleCache();
    }

    /// <summary>场景 YAML 未配置仰角时，使用代码内四档默认值。</summary>
    private void EnsureDefaultPresetsIfEmpty()
    {
        if (!Mathf.Approximately(morning.sunRotationX, 0f) ||
            !Mathf.Approximately(afternoon.sunRotationX, 0f))
            return;

        morning = RemiDayPhaseLightingPreset.DefaultMorning();
        afternoon = RemiDayPhaseLightingPreset.DefaultAfternoon();
        evening = RemiDayPhaseLightingPreset.DefaultEvening();
        night = RemiDayPhaseLightingPreset.DefaultNight();
    }

    private void OnEnable()
    {
        RemiDayPhaseLightingCoordinator.Register(this);
    }

    private void OnDisable()
    {
        RemiDayPhaseLightingCoordinator.Unregister(this);
    }

    /// <summary>立即切换到指定时段光照（无过渡）。</summary>
    public void ApplyPhase(RemiDayPhase phase)
    {
        EnsureDefaultPresetsIfEmpty();
        ResolveSunLight();
        RemiDayPhaseLightingPreset preset = GetPreset(phase);
        preset.ApplySunRotation(sunLight);
        ApplyPhaseObjects(preset);
    }

    public RemiDayPhaseLightingPreset GetPreset(RemiDayPhase phase) =>
        phase switch
        {
            RemiDayPhase.Morning => morning,
            RemiDayPhase.Afternoon => afternoon,
            RemiDayPhase.Evening => evening,
            RemiDayPhase.Night => night,
            _ => morning,
        };

    private void ResolveSunLight()
    {
        if (sunLight != null || !autoFindDirectionalLight)
            return;

        GameObject named = GameObject.Find(PreferredSunName);
        if (named != null)
            sunLight = named.GetComponent<Light>();

        if (sunLight != null)
            return;

#if UNITY_2023_1_OR_NEWER
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
        Light[] lights = Object.FindObjectsOfType<Light>();
#endif
        foreach (Light light in lights)
        {
            if (light != null && light.type == LightType.Directional)
            {
                sunLight = light;
                break;
            }
        }
    }

    private void DisableOtherDirectionalLights()
    {
        if (!disableOtherDirectionalLights)
            return;

#if UNITY_2023_1_OR_NEWER
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
        Light[] lights = Object.FindObjectsOfType<Light>();
#endif
        foreach (Light light in lights)
        {
            if (light == null || light.type != LightType.Directional || light == sunLight)
                continue;
            light.enabled = false;
        }
    }

    private void RebuildPhaseToggleCache()
    {
        _phaseToggleObjects = new HashSet<GameObject>();
        CollectActivateObjects(morning);
        CollectActivateObjects(afternoon);
        CollectActivateObjects(evening);
        CollectActivateObjects(night);
    }

    private void CollectActivateObjects(RemiDayPhaseLightingPreset preset)
    {
        if (preset?.activateObjects == null)
            return;
        foreach (GameObject go in preset.activateObjects)
        {
            if (go != null)
                _phaseToggleObjects.Add(go);
        }
    }

    private void ApplyPhaseObjects(RemiDayPhaseLightingPreset preset)
    {
        if (_phaseToggleObjects == null || _phaseToggleObjects.Count == 0)
            RebuildPhaseToggleCache();

        HashSet<GameObject> activeNow = new HashSet<GameObject>();
        if (preset?.activateObjects != null)
        {
            foreach (GameObject go in preset.activateObjects)
            {
                if (go != null)
                    activeNow.Add(go);
            }
        }

        foreach (GameObject go in _phaseToggleObjects)
        {
            if (go == null)
                continue;
            go.SetActive(activeNow.Contains(go));
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Preview/Morning")]
    private void Editor_PreviewMorning() => ApplyPhase(RemiDayPhase.Morning);

    [ContextMenu("Preview/Afternoon")]
    private void Editor_PreviewAfternoon() => ApplyPhase(RemiDayPhase.Afternoon);

    [ContextMenu("Preview/Evening")]
    private void Editor_PreviewEvening() => ApplyPhase(RemiDayPhase.Evening);

    [ContextMenu("Preview/Night")]
    private void Editor_PreviewNight() => ApplyPhase(RemiDayPhase.Night);

    [ContextMenu("Preview/Current World Phase")]
    private void Editor_PreviewCurrentWorldPhase()
    {
        RemiDayPhase phase = RemiPresenceService.Instance != null
            ? RemiPresenceService.Instance.WorldTime.phase
            : RemiDayPhase.Morning;
        ApplyPhase(phase);
    }
#endif
}
