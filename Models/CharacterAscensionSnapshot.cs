namespace Sts2SaveSyncTool.Models;

public sealed class CharacterAscensionSnapshot
{
    public CharacterAscensionSnapshot(string id, int maxAscension, int preferredAscension)
    {
        Id = id;
        DisplayName = id.StartsWith("CHARACTER.", StringComparison.OrdinalIgnoreCase)
            ? id["CHARACTER.".Length..]
            : id;
        MaxAscension = maxAscension;
        PreferredAscension = preferredAscension;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public int MaxAscension { get; }

    public int PreferredAscension { get; }

    public string SummaryText => $"{DisplayName}: A{MaxAscension} / 预设 {PreferredAscension}";
}
