using System;
using UnityEngine;

/// <summary>Demo 三场景标识（与 Build Settings 中场景名对应）。</summary>
public enum SceneTravelLocation
{
    Classroom = 0,
    Library = 1,
    Apartment = 2,
}

public static class SceneTravelCatalog
{
    public static string GetSceneName(SceneTravelLocation location) =>
        location switch
        {
            SceneTravelLocation.Classroom => "Classroom",
            SceneTravelLocation.Library => "Library",
            SceneTravelLocation.Apartment => "Apartment",
            _ => "Classroom",
        };

    public static string GetDestinationLabel(SceneTravelLocation location) =>
        location switch
        {
            SceneTravelLocation.Classroom => "前往教室",
            SceneTravelLocation.Library => "前往图书馆",
            SceneTravelLocation.Apartment => "前往公寓",
            _ => location.ToString(),
        };

    /// <summary>过场文案用场景名（不含「前往」）。</summary>
    public static string GetLocationDisplayName(SceneTravelLocation location) =>
        location switch
        {
            SceneTravelLocation.Classroom => "教室",
            SceneTravelLocation.Library => "图书馆",
            SceneTravelLocation.Apartment => "公寓",
            _ => location.ToString(),
        };

    /// <summary>教室开场剧情玩家站位（开始界面新游戏落点）。</summary>
    public const string ClassroomInStorySpawnName = "InStory";

    /// <summary>图书馆 Remi 默认落点（场景内物体名；历史拼写 Libary）。</summary>
    public const string LibraryDefaultRemiMarkerName = "LibaryDefaultPos";

    /// <summary>同上正确拼写备用名。</summary>
    public const string LibraryDefaultRemiMarkerNameAlt = "LibraryDefaultPos";

    /// <summary>Day2 自习：起点为馆内默认落点，其后按序 InStudy(R)…(4)。</summary>
    public const string LibraryStudyWaypointStart = LibraryDefaultRemiMarkerName;
    public const string LibraryStudyWaypoint0 = "InStudy(R)";
    public const string LibraryStudyWaypoint1 = "InStudy(R) (1)";
    public const string LibraryStudyWaypoint2 = "InStudy(R) (2)";
    public const string LibraryStudyWaypoint3 = "InStudy(R) (3)";
    public const string LibraryStudyWaypoint4 = "InStudy(R) (4)";

    /// <summary>Day2 自习终点告别机位根（可含子 Camera；相对 Remi 固定）。</summary>
    public const string LibraryDay2FarewellCamMarkerName = "FinalSpecial";

    /// <summary>Ending 回顾一瞥：按共同经历切到对应场景的 Recap_Day1..3 机位。</summary>
    public const string EndingRecapCamDay1 = "Recap_Day1";
    public const string EndingRecapCamDay2 = "Recap_Day2";
    public const string EndingRecapCamDay3 = "Recap_Day3";

    public struct EndingRecapGlimpseSpec
    {
        public SceneTravelLocation Location;
        public string CamMarker;
    }

    /// <summary>共同经历 → 回顾场景 + 机位名（Recap_Day1/2/3）。</summary>
    public static bool TryGetEndingRecapGlimpse(RemiSharedExperienceId id, out EndingRecapGlimpseSpec spec)
    {
        switch (id)
        {
            case RemiSharedExperienceId.Day1CommissionBook:
                spec = new EndingRecapGlimpseSpec
                {
                    Location = SceneTravelLocation.Classroom,
                    CamMarker = EndingRecapCamDay1,
                };
                return true;
            case RemiSharedExperienceId.Day2LibraryCoPresence:
                spec = new EndingRecapGlimpseSpec
                {
                    Location = SceneTravelLocation.Library,
                    CamMarker = EndingRecapCamDay2,
                };
                return true;
            case RemiSharedExperienceId.Day3DormDeviation:
                spec = new EndingRecapGlimpseSpec
                {
                    Location = SceneTravelLocation.Apartment,
                    CamMarker = EndingRecapCamDay3,
                };
                return true;
            default:
                spec = default;
                return false;
        }
    }

    public static bool TryGetEndingRecapGlimpse(string experienceIdKey, out EndingRecapGlimpseSpec spec)
    {
        if (RemiSharedExperienceCatalog.TryParseIdKey(experienceIdKey, out RemiSharedExperienceId id))
            return TryGetEndingRecapGlimpse(id, out spec);
        spec = default;
        return false;
    }

    /// <summary>Day1 结束「图书馆一瞥」机位（可选空物体；没有则用 AfterCon(P) 或相对 Remi 的偏移）。</summary>
    public const string LibraryDay1GlimpseCamMarkerName = "Day1LibraryGlimpseCam";

    /// <summary>Day2 结束「公寓一瞥」机位（挂在 Apartment 场景；可含子 Camera）。</summary>
    public const string ApartmentDay2GlimpseCamMarkerName = "Day2LibraryGlimpseCam";

    /// <summary>图书馆 Day2 共现场景内剧情站位。</summary>
    public const string LibraryInStorySpawnName = "InStory";

    /// <summary>图书馆共现固定剧情结束后 Remi 落点。</summary>
    public const string LibraryRemiAfterStoryMarkerName = "AfterCon(R)";

    /// <summary>图书馆共现自由面对面对话时玩家落点。</summary>
    public const string LibraryPlayerFreeDialogueMarkerName = "AfterCon(P)";

    /// <summary>公寓 Day3 固定剧情：玩家站位 InStory(P)。</summary>
    public const string ApartmentInStorySpawnName = "InStory(P)";

    /// <summary>公寓 Day3 固定剧情：Remi 站位 InStory(R)。</summary>
    public const string ApartmentRemiDuringStoryMarkerName = "InStory(R)";

    /// <summary>公寓 Day3 固定剧情结束后 Remi 落点。</summary>
    public const string ApartmentRemiAfterStoryMarkerName = "ApartmentDefaultPos";

    /// <summary>公寓 Day3 闲聊对话时玩家落点 DuringCon(P)。</summary>
    public const string ApartmentPlayerFreeDialogueMarkerName = "DuringCon(P)";

    /// <summary>Remi Day3 偏离后默认落点（与剧情结束后同点）。</summary>
    public const string ApartmentDefaultRemiMarkerName = "ApartmentDefaultPos";

    public static string GetSpawnPointName(SceneTravelLocation location) =>
        location switch
        {
            SceneTravelLocation.Classroom => "PlayerDefaultPos1",
            SceneTravelLocation.Library => "PlayerDefaultPos2",
            SceneTravelLocation.Apartment => "PlayerDefaultPos3",
            _ => "PlayerDefaultPos1",
        };

    /// <summary>新游戏首次进入场景时的落点（教室为剧情 InStory，其余与默认一致）。</summary>
    public static string GetNewGameSpawnPointName(SceneTravelLocation location) =>
        location switch
        {
            SceneTravelLocation.Classroom => ClassroomInStorySpawnName,
            _ => GetSpawnPointName(location),
        };

    public static SceneTravelLocation ResolveFromActiveScene()
    {
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (string.Equals(sceneName, "Library", StringComparison.OrdinalIgnoreCase))
            return SceneTravelLocation.Library;
        if (string.Equals(sceneName, "Apartment", StringComparison.OrdinalIgnoreCase))
            return SceneTravelLocation.Apartment;
        return SceneTravelLocation.Classroom;
    }

    /// <summary>Remi 日程地点 → 玩家可传送场景。</summary>
    public static SceneTravelLocation MapRemiLocation(RemiLocation location) =>
        location switch
        {
            RemiLocation.Library => SceneTravelLocation.Library,
            RemiLocation.Dorm => SceneTravelLocation.Apartment,
            _ => SceneTravelLocation.Classroom,
        };

    public static void GetDestinationsFrom(
        SceneTravelLocation current,
        out SceneTravelLocation optionA,
        out SceneTravelLocation optionB)
    {
        switch (current)
        {
            case SceneTravelLocation.Classroom:
                optionA = SceneTravelLocation.Library;
                optionB = SceneTravelLocation.Apartment;
                break;
            case SceneTravelLocation.Library:
                optionA = SceneTravelLocation.Classroom;
                optionB = SceneTravelLocation.Apartment;
                break;
            default:
                optionA = SceneTravelLocation.Classroom;
                optionB = SceneTravelLocation.Library;
                break;
        }
    }
}
