namespace Sts2SaveSyncTool.Models;

public sealed class SteamAccountState
{
    public SteamAccountState(string steamId, string rootPath, int lastProfileId)
    {
        SteamId = steamId;
        RootPath = rootPath;
        LastProfileId = lastProfileId is >= 1 and <= 3 ? lastProfileId : 1;
    }

    public string SteamId { get; }

    public string RootPath { get; }

    public int LastProfileId { get; }

    public string DisplayName => SteamId;

    public override string ToString()
    {
        return DisplayName;
    }
}
