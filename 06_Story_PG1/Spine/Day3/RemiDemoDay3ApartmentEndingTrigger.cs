using UnityEngine;

/// <summary>
/// 已弃用：Day3 Ending 改由公寓门口「离开」拦截传送触发
/// （<see cref="SceneTravelService.TravelTo"/> → <see cref="RemiDemoSpineDirector.TryPlayDay3ApartmentEnding"/>）。
/// 场景中若仍挂有本组件，保持无操作以免重复触发。
/// </summary>
[DisallowMultipleComponent]
public class RemiDemoDay3ApartmentEndingTrigger : MonoBehaviour
{
}
