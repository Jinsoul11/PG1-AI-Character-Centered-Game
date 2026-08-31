/// <summary>动态 [MEMORY] 共同经历段（只读；静态 policy 见 <see cref="Remi"/>）。</summary>
public static class RemiMemoryBuilder
{
    public static string BuildExperiencesBlock()
    {
        if (RemiSharedExperienceMemory.Instance == null)
            return string.Empty;
        return RemiSharedExperienceMemory.Instance.BuildExperiencesBlock();
    }
}
