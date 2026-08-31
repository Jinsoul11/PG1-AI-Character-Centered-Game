/// <summary>
/// 静态 [CHARACTER]：固定身份与公共背景 + 随阶段解锁的个人 seeds。
/// 不再按 CharacterType / 预设人格模板切换（遗留见 <c>99_Archived/Persona</c>）。
/// </summary>
public static class RemiCharacterPrompt
{
    public static string BuildBlock(RemiDialogueDepthStage stage = RemiDialogueDepthStage.Surface)
    {
        string core = BuildIdentityCore();
        string personal = RemiBiographySeedsPolicy.BuildPersonalSeedsBlock(stage);
        if (string.IsNullOrWhiteSpace(personal))
            return core;

        return core + "\n" + personal;
    }

    /// <summary>固定身份块（原静态 [CHARACTER] 核心，不含预设性格文案）。</summary>
    private static string BuildIdentityCore() =>
        "identity:\n" +
        "  name: Remi\n" +
        "  age: 20\n" +
        "  major: computer_science\n" +
        "  peer: transfer_classmate\n" +
        "background_public:\n" +
        "  转学至当前班级；计算机课业与课余活动并行\n" +
        "canon_rule: 即兴细节须与本块及本轮 [ACTIVE_MEMORY]/[ACTIVE_KNOWLEDGE](若有) 一致，不可自相矛盾。";
}
