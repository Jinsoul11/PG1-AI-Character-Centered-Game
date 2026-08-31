using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Day2 开幕：教室不见 Remi，过场期间将其逻辑迁往图书馆默认落点；进入图书馆场景时在 LibaryDefaultPos 生成/落位。
/// Day3 偏离：进公寓时按 intro 是否已播，落 InStory(R) 或 ApartmentDefaultPos。
/// </summary>
public static class RemiWorldPlacement
{
    private const string LibrarySceneName = "Library";
    private const string ApartmentSceneName = "Apartment";
    private const string LibraryDefaultMarker = "LibaryDefaultPos";
    private const string LibraryDefaultMarkerAlt = "LibraryDefaultPos";
    private const string ApartmentDefaultMarker = SceneTravelCatalog.ApartmentDefaultRemiMarkerName;
    private const string RemiResourcePath = "Character/Remi";
    public const float Day1LibraryGlimpseRemiYawDegrees = 90f;
    public const float RemiDefaultIdleYawDegrees = 0f;

    private static bool _day2RemiRelocatedToLibrary;
    private static bool _day3RemiAtApartment;

    public static bool Day2RemiRelocatedToLibrary => _day2RemiRelocatedToLibrary;

    public static bool Day3RemiAtApartment => _day3RemiAtApartment;

    /// <summary>Day3 偏离接受后：玩家前往公寓时在此落位 Remi。</summary>
    public static void PrepareRemiAtApartmentForDay3()
    {
        _day3RemiAtApartment = true;
        Scene scene = SceneManager.GetActiveScene();
        if (IsApartmentScene(scene))
            TryPlaceRemiForDay3Apartment(scene);
    }

    /// <summary>过场开始时调用：教室中隐藏 Remi，并标记待在图书馆默认位置落位。</summary>
    public static void PrepareRemiAbsentFromClassroomForDay2()
    {
        _day2RemiRelocatedToLibrary = true;
        SetRemiVisibleInScene(false);
    }

    /// <summary>读档/重进教室：若仍在 Day2 教室段，确保 Remi 不在教室显示。</summary>
    public static void EnsureDay2AbsentInClassroom()
    {
        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector director = RemiDemoSpineDirector.Instance;
        if (director == null || !director.IsDay2ClassroomPhase())
            return;

        _day2RemiRelocatedToLibrary = true;
        if (IsClassroomScene(SceneManager.GetActiveScene()))
            SetRemiVisibleInScene(false);
    }

    public static void OnSceneLoaded(Scene scene)
    {
        if (!_day2RemiRelocatedToLibrary)
        {
            RemiDemoSpineDirector.EnsureExists();
            if (RemiDemoSpineDirector.Instance != null &&
                RemiDemoSpineDirector.Instance.IsDay2ClassroomPhase())
                _day2RemiRelocatedToLibrary = true;
        }

        if (!_day3RemiAtApartment)
        {
            RemiDemoSpineDirector.EnsureExists();
            if (RemiDemoSpineDirector.Instance != null &&
                RemiDemoSpineDirector.Instance.IsAwaitingDay3ApartmentVisit())
                _day3RemiAtApartment = true;
        }

        // Day3 偏离窗口：Remi 仍在图书馆轨道，公寓场景不得露出她
        if (IsDay3LibraryTrackWindow())
        {
            if (IsLibraryScene(scene))
            {
                TryPlaceRemiAtLibraryDefault();
                SetRemiWorldYaw(RemiDefaultIdleYawDegrees);
            }
            else if (IsClassroomScene(scene) || IsApartmentScene(scene))
                SetRemiVisibleInScene(false);
            return;
        }

        if (_day2RemiRelocatedToLibrary)
        {
            if (IsLibraryScene(scene))
            {
                RemiLibraryDay2CoPresenceFlow.EnsureExists();
                RemiLibraryDay2CoPresenceFlow day2 = RemiLibraryDay2CoPresenceFlow.Instance;
                // 自习告别后 Remi 已离场：勿再落到默认点
                if (day2 != null && day2.HasCompletedStudyFarewell)
                    SetRemiVisibleInScene(false);
                else
                {
                    TryPlaceRemiAtLibraryDefault();
                    // Day2 默认面向书架（Y=0）；第一次剧情再转到 -90
                    SetRemiWorldYaw(RemiDefaultIdleYawDegrees);
                    day2?.NotifyLibraryWindowStart();
                }
            }
            else if (IsClassroomScene(scene))
                SetRemiVisibleInScene(false);
        }

        if (_day3RemiAtApartment && IsApartmentScene(scene))
            TryPlaceRemiForDay3Apartment(scene);
    }

    /// <summary>Day1 傍晚一瞥：在图书馆默认落点显示 Remi（不标记 Day2 迁馆）；可选覆盖世界 Y 轴朝向。</summary>
    public static void PlaceRemiForLibraryGlimpse(float? worldYawDegrees = null)
    {
        TryPlaceRemiAtLibraryDefault();
        if (worldYawDegrees.HasValue)
            SetRemiWorldYaw(worldYawDegrees.Value);
    }

    /// <summary>Day2 收束一瞥：在公寓默认落点显示 Remi（不标记 Day3 入住）。</summary>
    public static void PlaceRemiForApartmentGlimpse(float? worldYawDegrees = null)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!TryPlaceRemiAtNamedMarker(ApartmentDefaultMarker, scene))
            TryPlaceRemiAtNamedMarker("ApartmentDefaultPos(R)", scene);
        if (worldYawDegrees.HasValue)
            SetRemiWorldYaw(worldYawDegrees.Value);
    }

    /// <summary>Day2 自习告别后：图书馆内隐藏 Remi。</summary>
    public static void HideRemiInCurrentScene()
    {
        SetRemiVisibleInScene(false);
    }

    public static void SetRemiWorldYaw(float yawDegrees)
    {
        Transform remi = ResolveRemiTransform(includeInactive: true);
        if (remi == null)
            return;

        Vector3 e = remi.eulerAngles;
        e.y = yawDegrees;
        remi.eulerAngles = e;
    }

    private static void TryPlaceRemiAtLibraryDefault()
    {
        GameObject marker = GameObject.Find(LibraryDefaultMarker);
        if (marker == null)
            marker = GameObject.Find(LibraryDefaultMarkerAlt);
        if (marker == null)
        {
            // 兜底：用共现后 Remi 落点
            marker = GameObject.Find(SceneTravelCatalog.LibraryRemiAfterStoryMarkerName);
        }

        if (marker == null)
        {
            Debug.LogWarning($"[RemiWorldPlacement] 图书馆场景未找到落点 {LibraryDefaultMarker} / {LibraryDefaultMarkerAlt}。");
            return;
        }

        Transform remi = ResolveRemiTransform(includeInactive: true);
        if (remi == null)
        {
            GameObject prefab = Resources.Load<GameObject>(RemiResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[RemiWorldPlacement] 无法从 Resources/{RemiResourcePath} 加载 Remi。");
                return;
            }

            GameObject instance = Object.Instantiate(prefab, marker.transform.position, marker.transform.rotation);
            instance.name = prefab.name;
            remi = instance.transform;
        }
        else
        {
            remi.SetPositionAndRotation(marker.transform.position, marker.transform.rotation);
        }

        remi.gameObject.SetActive(true);
        WireStoryDirectorRemiRoot(remi);
    }

    /// <summary>
    /// 等待公寓 intro：Remi 在 InStory(R)；intro 已播完：ApartmentDefaultPos。
    /// </summary>
    private static void TryPlaceRemiForDay3Apartment(Scene scene)
    {
        bool awaitingIntro = false;
        RemiDemoSpineDirector.EnsureExists();
        RemiDemoSpineDirector director = RemiDemoSpineDirector.Instance;
        if (director != null && director.IsAwaitingDay3ApartmentVisit())
            awaitingIntro = PlayerPrefs.GetInt(RemiApartmentDay3CoPresenceStory.PrefsPlayedKey, 0) == 0;

        string marker = awaitingIntro
            ? SceneTravelCatalog.ApartmentRemiDuringStoryMarkerName
            : SceneTravelCatalog.ApartmentDefaultRemiMarkerName;
        TryPlaceRemiAtNamedMarker(marker, scene);
    }

    private static void TryPlaceRemiAtApartmentDefault()
    {
        TryPlaceRemiAtNamedMarker(ApartmentDefaultMarker, SceneManager.GetActiveScene());
    }

    /// <summary>场景内按名字落位 Remi（含 inactive 标记；找不到则告警）。</summary>
    public static bool PlaceRemiAtNamedMarker(string markerName) =>
        TryPlaceRemiAtNamedMarker(markerName, SceneManager.GetActiveScene());

    /// <summary>Day3 Ending 开场：Remi 站在公寓 InStory(R)。</summary>
    public static bool PlaceRemiForDay3Ending() =>
        PlaceRemiAtNamedMarker(SceneTravelCatalog.ApartmentRemiDuringStoryMarkerName);

    /// <summary>
    /// Ending 回顾切景后：强制显示 Remi。
    /// Day2 迁馆标记会在教室 OnSceneLoaded 时把 Remi 藏掉，导致 SendSystem 找不到组件。
    /// </summary>
    public static void EnsureRemiActiveForEndingRecap(SceneTravelLocation location)
    {
        switch (location)
        {
            case SceneTravelLocation.Library:
                TryPlaceRemiAtLibraryDefault();
                break;
            case SceneTravelLocation.Apartment:
                PlaceRemiForDay3Ending();
                break;
            case SceneTravelLocation.Classroom:
            default:
            {
                Transform remi = ResolveRemiTransform(includeInactive: true);
                if (remi != null)
                {
                    remi.gameObject.SetActive(true);
                    WireStoryDirectorRemiRoot(remi);
                }
                else
                {
                    Debug.LogWarning("[RemiWorldPlacement] Ending 回顾：教室场景未找到 Remi（含 inactive）。");
                }

                break;
            }
        }
    }

    private static bool TryPlaceRemiAtNamedMarker(string markerName, Scene scene)
    {
        Transform markerTf = SceneTravelService.TryFindSceneMarker(markerName, scene);
        if (markerTf == null)
        {
            // 兼容未走 SceneTravelService 的查找
            GameObject markerGo = GameObject.Find(markerName);
            markerTf = markerGo != null ? markerGo.transform : null;
        }

        if (markerTf == null)
        {
            Debug.LogWarning($"[RemiWorldPlacement] 公寓场景未找到落点 {markerName}。");
            return false;
        }

        Transform remi = ResolveRemiTransform(includeInactive: true);
        if (remi == null)
        {
            GameObject prefab = Resources.Load<GameObject>(RemiResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[RemiWorldPlacement] 无法从 Resources/{RemiResourcePath} 加载 Remi。");
                return false;
            }

            GameObject instance = Object.Instantiate(prefab, markerTf.position, markerTf.rotation);
            instance.name = prefab.name;
            remi = instance.transform;
        }
        else
        {
            remi.SetPositionAndRotation(markerTf.position, markerTf.rotation);
        }

        remi.gameObject.SetActive(true);
        WireStoryDirectorRemiRoot(remi);
        return true;
    }

    private static void SetRemiVisibleInScene(bool visible)
    {
        Transform remi = ResolveRemiTransform(includeInactive: true);
        if (remi != null)
            remi.gameObject.SetActive(visible);
    }

    private static Transform ResolveRemiTransform(bool includeInactive)
    {
        StoryDirector director = Object.FindObjectOfType<StoryDirector>();
        if (director != null)
        {
            Transform wired = director.GetRemiRoot();
            if (wired != null)
                return wired;
        }

        RemiInteraction interaction = includeInactive
            ? Object.FindObjectOfType<RemiInteraction>(true)
            : Object.FindObjectOfType<RemiInteraction>();
        if (interaction != null)
            return interaction.transform;

        GameObject named = GameObject.Find("Remi");
        return named != null ? named.transform : null;
    }

    private static void WireStoryDirectorRemiRoot(Transform remi)
    {
        StoryDirector director = FindLocalStoryDirector(SceneManager.GetActiveScene());
        director?.SetRemiRoot(remi);
    }

    private static StoryDirector FindLocalStoryDirector(Scene scene)
    {
        if (scene.IsValid())
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null)
                    continue;

                StoryDirector local = root.GetComponentInChildren<StoryDirector>(true);
                if (local != null && local.GetComponentInParent<RemiDemoSpineDirector>() == null)
                    return local;
            }
        }

        return Object.FindObjectOfType<StoryDirector>();
    }

    private static bool IsLibraryScene(Scene scene) =>
        string.Equals(scene.name, LibrarySceneName, System.StringComparison.OrdinalIgnoreCase);

    private static bool IsClassroomScene(Scene scene) =>
        string.Equals(scene.name, "Classroom", System.StringComparison.OrdinalIgnoreCase);

    private static bool IsApartmentScene(Scene scene) =>
        string.Equals(scene.name, ApartmentSceneName, System.StringComparison.OrdinalIgnoreCase);

    public static void ClearDay2RelocationFlag()
    {
        _day2RemiRelocatedToLibrary = false;
    }

    public static void ClearDay3ApartmentRelocationFlag()
    {
        _day3RemiAtApartment = false;
    }

    /// <summary>Day3 偏离窗口未采纳：仍在图书馆轨道。</summary>
    public static bool IsDay3LibraryTrackWindow()
    {
        RemiDemoSpineDirector.EnsureExists();
        return RemiDemoSpineDirector.Instance != null &&
               RemiDemoSpineDirector.Instance.IsDay3DeviationWindowOpen;
    }

#if UNITY_EDITOR
    public static void Editor_ResetDay2Relocation() => ClearDay2RelocationFlag();

    public static void Editor_ResetDay3ApartmentRelocation() => ClearDay3ApartmentRelocationFlag();
#endif
}
