using UnityEngine;

/// <summary>
/// Ending 收束模板（仍在用）：开场后 N 页与 Bond Mode B 之后的面对面收束。
/// 旧 Bond 填空模板见 <c>Remi/Memory/废弃/RemiDemoEndingBondTemplates</c>。
/// </summary>
public static class RemiDemoEndingClosingTemplates
{
    public struct ClosingDef
    {
        public string SystemContext;
        public string FilledLetter;
    }

    public static ClosingDef ResolveClosing(RemiDemoEndingPayload payload)
    {
        string filled = !string.IsNullOrWhiteSpace(payload?.closingTemplateFilled)
            ? payload.closingTemplateFilled.Trim()
            : BuildDefaultClosing(payload);

        int count = payload?.timeline?.eventCount ?? 0;
        string context =
            "Demo 尾声面对面收束。Remi 刚回顾完共同经历与相处感受。" +
            $"本局登记 {count} 段共同经历。下方是已填好的收束句，只能润色，不可新增事实。" +
            "第一人称；仅润色下方收束句，不可新增事件、地点或关系事实；1～2 句；可留白。";

        return new ClosingDef
        {
            SystemContext = context,
            FilledLetter = filled,
        };
    }

    public static string BuildDefaultClosing(RemiDemoEndingPayload payload)
    {
        int count = payload?.timeline?.eventCount ?? 0;
        string depthPhrase = DepthClosingPhrase(payload);
        string routePhrase = RouteClosingPhrase(payload);

        if (count <= 0)
            return $"这几天好像很快。{depthPhrase}";

        return $"这几天，我们一起经历了 {count} 段我记得住的事。{depthPhrase}{routePhrase}";
    }

    /// <summary>偏离事实短句（供 Payload / 调试；不再作为 Bond 模板页）。</summary>
    public static string ResolveRouteBaseLine(RemiDemoEndingPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload?.bondSlots?.routeReflectionFallback))
            return payload.bondSlots.routeReflectionFallback.Trim();

        return ResolveDeviationKind(payload) switch
        {
            RemiDemoRouteDeviationKind.Dorm =>
                "最后那天，你把我叫去了宿舍。我知道这对我来说很少见。",
            _ => "这几天就这样过去了。也许下次，我们还能找到别的理由再见面。",
        };
    }

    private static string DepthClosingPhrase(RemiDemoEndingPayload payload)
    {
        return ResolveDepthStage(payload) switch
        {
            RemiDialogueDepthStage.Influential => "和你说话，我好像不用一直端着。",
            RemiDialogueDepthStage.Relational => "和你说话，好像也没那么拘束。",
            _ => "和你说话，好像也没那么陌生了。",
        };
    }

    private static string RouteClosingPhrase(RemiDemoEndingPayload payload)
    {
        return ResolveDeviationKind(payload) switch
        {
            RemiDemoRouteDeviationKind.Dorm => " 最后你把我叫去了宿舍——这对我来说，算是破例。",
            _ => "",
        };
    }

    private static RemiDialogueDepthStage ResolveDepthStage(RemiDemoEndingPayload payload)
    {
        if (payload?.relationship == null)
            return RemiDialogueDepthStage.Surface;
        return (RemiDialogueDepthStage)Mathf.Clamp(payload.relationship.depthStage, 0, 2);
    }

    private static RemiDemoRouteDeviationKind ResolveDeviationKind(RemiDemoEndingPayload payload)
    {
        if (payload?.route == null)
            return RemiDemoRouteDeviationKind.None;
        return (RemiDemoRouteDeviationKind)Mathf.Clamp(payload.route.deviationKind, 0, 1);
    }
}
