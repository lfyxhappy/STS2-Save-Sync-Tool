namespace Sts2SaveSyncTool.Services;

internal sealed class SyncTargetBundle
{
    public SyncTargetBundle(
        string steamId,
        int profileId,
        SaveSide targetSide,
        string sourceLabel,
        string targetLabel,
        string sourcePath,
        byte[] sourceBytes,
        string sourceHash,
        string sourceSha1,
        long sourceSize,
        string appDataProgressPath,
        string appDataBackupPath,
        string cacheFilePath,
        string remoteCachePath,
        string cacheRelativePath,
        DateTime canonicalTimestampUtc,
        byte[] updatedRemoteCacheBytes)
    {
        SteamId = steamId;
        ProfileId = profileId;
        TargetSide = targetSide;
        SourceLabel = sourceLabel;
        TargetLabel = targetLabel;
        SourcePath = sourcePath;
        SourceBytes = sourceBytes;
        SourceHash = sourceHash;
        SourceSha1 = sourceSha1;
        SourceSize = sourceSize;
        AppDataProgressPath = appDataProgressPath;
        AppDataBackupPath = appDataBackupPath;
        CacheFilePath = cacheFilePath;
        RemoteCachePath = remoteCachePath;
        CacheRelativePath = cacheRelativePath;
        CanonicalTimestampUtc = canonicalTimestampUtc;
        UpdatedRemoteCacheBytes = updatedRemoteCacheBytes;
    }

    public string SteamId { get; }

    public int ProfileId { get; }

    public SaveSide TargetSide { get; }

    public string SourceLabel { get; }

    public string TargetLabel { get; }

    public string SourcePath { get; }

    public byte[] SourceBytes { get; }

    public string SourceHash { get; }

    public string SourceSha1 { get; }

    public long SourceSize { get; }

    public string AppDataProgressPath { get; }

    public string AppDataBackupPath { get; }

    public string CacheFilePath { get; }

    public string RemoteCachePath { get; }

    public string CacheRelativePath { get; }

    public DateTime CanonicalTimestampUtc { get; }

    public byte[] UpdatedRemoteCacheBytes { get; }
}
