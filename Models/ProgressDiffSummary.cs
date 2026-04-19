namespace Sts2SaveSyncTool.Models;

public sealed class ProgressDiffSummary
{
    public ProgressDiffSummary(
        bool bothMissing,
        bool hashesEqual,
        IReadOnlyList<string> globalLines,
        IReadOnlyList<string> characterDifferenceLines,
        IReadOnlyList<string> cacheConsistencyLines,
        string summaryText)
    {
        BothMissing = bothMissing;
        HashesEqual = hashesEqual;
        GlobalLines = globalLines;
        CharacterDifferenceLines = characterDifferenceLines;
        CacheConsistencyLines = cacheConsistencyLines;
        SummaryText = summaryText;
    }

    public bool BothMissing { get; }

    public bool HashesEqual { get; }

    public IReadOnlyList<string> GlobalLines { get; }

    public IReadOnlyList<string> CharacterDifferenceLines { get; }

    public IReadOnlyList<string> CacheConsistencyLines { get; }

    public string SummaryText { get; }

    public string GlobalSummaryText => GlobalLines.Count == 0
        ? "无可展示字段"
        : string.Join(Environment.NewLine, GlobalLines);

    public string CharacterDifferenceText => CharacterDifferenceLines.Count == 0
        ? "角色难度：无差异"
        : string.Join(Environment.NewLine, CharacterDifferenceLines);

    public string CacheConsistencyText => CacheConsistencyLines.Count == 0
        ? "缓存状态：无可展示信息"
        : string.Join(Environment.NewLine, CacheConsistencyLines);
}
