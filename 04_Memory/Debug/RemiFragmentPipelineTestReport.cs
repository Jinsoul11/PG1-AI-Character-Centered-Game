using System;
using System.Collections.Generic;

/// <summary>Fragment 全链路测试报告（Debug）。</summary>
[Serializable]
public class RemiFragmentPipelineTestReport
{
    public string runId = "";
    public string fixtureId = "";
    public long startedUnixMs;
    public long finishedUnixMs;
    public float durationMs;
    public bool success;
    public string status = "";
    public List<int> storyDays = new List<int>();

    public int archiveWrittenCount;
    public List<RemiFragmentPipelineTestLineSnap> archiveSamples = new List<RemiFragmentPipelineTestLineSnap>();

    public List<RemiFragmentPipelineTestDayReport> days = new List<RemiFragmentPipelineTestDayReport>();

    public List<RemiFragmentPipelineTestImpressionSnap> fragmentMemory =
        new List<RemiFragmentPipelineTestImpressionSnap>();

    public RemiFragmentPipelineTestSelectionReport selection = new RemiFragmentPipelineTestSelectionReport();
    public RemiFragmentPipelineTestBondReport bond = new RemiFragmentPipelineTestBondReport();
    public List<RemiFragmentPipelineTestLlmCall> llmCalls = new List<RemiFragmentPipelineTestLlmCall>();
}

[Serializable]
public class RemiFragmentPipelineTestLineSnap
{
    public int storyDay;
    public string speaker = "";
    public string content = "";
}

[Serializable]
public class RemiFragmentPipelineTestDayReport
{
    public int storyDay;
    public RemiFragmentPipelineTestFilterReport filter = new RemiFragmentPipelineTestFilterReport();
    public RemiFragmentPipelineTestCuratorReport curator = new RemiFragmentPipelineTestCuratorReport();
    public RemiFragmentPipelineTestUnitsReport units = new RemiFragmentPipelineTestUnitsReport();
    public RemiFragmentPipelineTestAnalyzerReport analyzer = new RemiFragmentPipelineTestAnalyzerReport();
    public int promotedCount;
}

[Serializable]
public class RemiFragmentPipelineTestFilterReport
{
    public int keptCount;
    public int rejectedCount;
    public List<string> keptSamples = new List<string>();
    public List<string> rejectReasons = new List<string>();
}

[Serializable]
public class RemiFragmentPipelineTestCuratorReport
{
    public bool success;
    public string error = "";
    public int candidateCount;
    public List<string> summaries = new List<string>();
}

[Serializable]
public class RemiFragmentPipelineTestUnitsReport
{
    public int count;
    public List<string> ids = new List<string>();
}

[Serializable]
public class RemiFragmentPipelineTestAnalyzerReport
{
    public bool success;
    public string error = "";
    public int analyzedCount;
    public List<RemiFragmentPipelineTestUnitSnap> units = new List<RemiFragmentPipelineTestUnitSnap>();
}

[Serializable]
public class RemiFragmentPipelineTestUnitSnap
{
    public string id = "";
    public string summary = "";
    public string tags = "";
    public float intrinsic;
    public float weight;
    public string weightReason = "";
}

[Serializable]
public class RemiFragmentPipelineTestImpressionSnap
{
    public string id = "";
    public int storyDay;
    public string summary = "";
    public string tags = "";
    public float weight;
    public bool eligibleForBond;
}

[Serializable]
public class RemiFragmentPipelineTestSelectionReport
{
    public int maxSelected;
    public bool hasBondPresentation;
    public List<RemiFragmentPipelineTestImpressionSnap> selected =
        new List<RemiFragmentPipelineTestImpressionSnap>();
    public List<string> rejectedWithReason = new List<string>();
}

[Serializable]
public class RemiFragmentPipelineTestBondReport
{
    /// <summary>llm | honest_fallback | skipped</summary>
    public string source = "skipped";
    public string line = "";
    public string skipReason = "";
    public string llmError = "";
    public string brief = "";
}

[Serializable]
public class RemiFragmentPipelineTestLlmCall
{
    public string stage = "";
    public int storyDay;
    public bool ok;
    public float ms;
    public string error = "";
}
