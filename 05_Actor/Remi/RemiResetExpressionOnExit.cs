using UnityEngine;

/// <summary>
/// Face 表情状态退出时把 Expression 拉回 Default（6），避免卡在触发值反复进同一表情。
/// </summary>
public class RemiResetExpressionOnExit : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (animator == null) return;
        animator.SetInteger("Expression", Remi.ExpressionIdle);
    }
}
