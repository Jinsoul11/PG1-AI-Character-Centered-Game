/// <summary>
/// 面对面 / 手机互斥：玩家与 Remi 同场景（日程地点）时仅面对面；分离时仅手机可打字。
/// LLM 工作记忆已合流，靠互斥避免两通道同时写入。
/// </summary>
public static class RemiInteractionChannelPolicy
{
    /// <summary>玩家当前场景与 Remi 日程地点是否同一可玩场景。</summary>
    public static bool IsCoLocatedWithRemi(RemiPresenceService presence)
    {
        if (presence == null)
            return false;

        SceneTravelLocation playerScene = SceneTravelCatalog.ResolveFromActiveScene();
        SceneTravelLocation remiScene = SceneTravelCatalog.MapRemiLocation(presence.CurrentLocation);
        if (playerScene == remiScene)
            return true;

        // Day1 找书窗口/委托：人仍在教室走动，勿因日程 phase 误判为图书馆而封面对面。
        if (playerScene == SceneTravelLocation.Classroom)
        {
            RemiBookQuestFlow flow = RemiBookQuestFlow.Instance;
            if (flow != null && flow.IsQuestFeatureEnabled &&
                (flow.State == RemiBookQuestFlow.QuestState.WindowOpen ||
                 flow.State == RemiBookQuestFlow.QuestState.WaitingForBook))
                return true;
        }

        // Day2 共现 Window / Story / FreeChat：玩家已在图书馆，即使日程短暂不同步也允许面对面。
        if (playerScene == SceneTravelLocation.Library)
        {
            RemiLibraryDay2CoPresenceFlow day2 = RemiLibraryDay2CoPresenceFlow.Instance;
            if (day2 != null && day2.AllowsFaceToFaceCoLocation)
                return true;
        }

        // Day3 公寓偏离自由聊：玩家在公寓且尚未终幕，即使日程覆盖被误清也允许面对面。
        if (playerScene == SceneTravelLocation.Apartment)
        {
            RemiDemoSpineDirector spine = RemiDemoSpineDirector.Instance;
            if (spine != null &&
                spine.CurrentBeat >= RemiDemoSpineBeat.Day3DeviationAccepted &&
                spine.CurrentBeat < RemiDemoSpineBeat.Day3Complete)
                return true;
        }

        return false;
    }

    /// <summary>手机聊天 Tab 是否允许玩家输入（可读历史 / 动态）。</summary>
    public static bool CanPlayerTypeInSocialChannel(RemiPresenceService presence)
    {
        if (presence == null)
            return false;
        if (IsCoLocatedWithRemi(presence))
            return false;
        return presence.CanUseSocialChannelNow();
    }

    /// <summary>是否允许发起面对面自由对话（须共位且 Remi 当前方便聊）。</summary>
    public static bool CanPlayerUseFaceToFaceChannel(RemiPresenceService presence)
    {
        if (presence == null)
            return false;
        if (!IsCoLocatedWithRemi(presence))
            return false;
        return presence.CanOpenFaceToFaceDialogue();
    }
}
