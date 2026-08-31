using System;
using System.Collections.Generic;

/// <summary>Analyzer LLM 对单条 Unit 的语义输出（理解，非创作）。</summary>
[Serializable]
public class RemiFragmentAnalyzerLlmItem
{
    public string unitId = "";
    /// <summary>Intrinsic / memoryStrength → breakdown.semantic。</summary>
    public float memoryStrength;
    public string semanticReason = "";
    public List<string> meaningTags = new List<string>();
    public string atmosphere = "";
}

[Serializable]
public class RemiFragmentAnalyzerDayResult
{
    public int storyDay;
    public bool success;
    public string error = "";
    public string rawResponse = "";
    public int unitCount;
    public int analyzedCount;
    public long analyzedUnixMs;
}
