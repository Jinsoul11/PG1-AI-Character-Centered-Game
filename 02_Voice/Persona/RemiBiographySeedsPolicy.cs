using System.Collections.Generic;
using System.Text;

/// <summary>
/// 随关系阶段解锁的「个人向」biography_seeds（习惯、私事、过往、人生设想）。
/// 公共背景（如作品展）由 <see cref="RemiCharacterPrompt"/> 固定给出，不在此重复。
/// </summary>
public static class RemiBiographySeedsPolicy
{
    public static string BuildPersonalSeedsBlock(RemiDialogueDepthStage stage)
    {
        IReadOnlyList<string> seeds = stage switch
        {
            RemiDialogueDepthStage.Influential => InfluentialPersonalSeeds(),
            RemiDialogueDepthStage.Relational => RelationalPersonalSeeds(),
            _ => SurfacePersonalSeeds(),
        };

        if (seeds == null || seeds.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append("biography_seeds_personal:\n");
        foreach (string seed in seeds)
        {
            if (string.IsNullOrWhiteSpace(seed))
                continue;
            sb.Append("  - ").Append(seed.Trim()).Append('\n');
        }

        return sb.ToString().TrimEnd();
    }

    private static IReadOnlyList<string> SurfacePersonalSeeds() => new[]
    {
        "喜欢二次元",
    };

    private static IReadOnlyList<string> RelationalPersonalSeeds() => new[]
    {
        "喜欢二次元",
        "大一的时候接触了百合动漫，从此坠入二次元",
    };

    private static IReadOnlyList<string> InfluentialPersonalSeeds() => new[]
    {
        "喜欢二次元",
        "大一的时候接触了百合动漫，从此坠入二次元",
        "接触了人工智能后，开始研究如何将人工智能与游戏结合",
    };
}
