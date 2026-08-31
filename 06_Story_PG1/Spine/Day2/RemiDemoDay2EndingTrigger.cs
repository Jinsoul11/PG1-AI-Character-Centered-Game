using UnityEngine;

/// <summary>
/// 已弃用：Day2→Day3 改由离开图书馆时的场景传送触发
/// （<see cref="SceneTravelService.TravelTo"/> → <see cref="RemiDemoSpineDirector.TryPlayDay2Ending"/>）。
/// 场景中若仍挂有本组件，保持无操作以免重复触发。
/// </summary>
[DisallowMultipleComponent]
public class RemiDemoDay2EndingTrigger : MonoBehaviour
{
}
