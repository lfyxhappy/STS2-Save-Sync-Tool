using System.Globalization;

namespace Sts2SaveSyncTool.Models;

public sealed class SteamCacheSnapshot
{
    public SteamCacheSnapshot(
        string? cacheRootPath,
        string? cacheFilePath,
        string? remoteCachePath,
        bool canParticipateInSync,
        string syncSupportText,
        bool cacheFileExists = false,
        DateTime? cacheFileLastWriteTimeUtc = null,
        long? cacheFileSize = null,
        string? cacheFileHash = null,
        string? cacheFileSha1 = null,
        bool cacheFileMatchesLocalProgress = false,
        bool entryExists = false,
        long? entrySize = null,
        string? entrySha1 = null,
        long? entryLocalTimeSeconds = null,
        long? entryTimeSeconds = null,
        long? entryRemoteTimeSeconds = null,
        bool entryMetadataMatchesLocalProgress = false,
        bool entryTimestampsAlignedWithCacheFile = false,
        string? cacheFileStateText = null,
        string? remoteCacheEntryStateText = null,
        string? summaryText = null)
    {
        CacheRootPath = cacheRootPath;
        CacheFilePath = cacheFilePath;
        RemoteCachePath = remoteCachePath;
        CanParticipateInSync = canParticipateInSync;
        SyncSupportText = syncSupportText;
        CacheFileExists = cacheFileExists;
        CacheFileLastWriteTimeUtc = cacheFileLastWriteTimeUtc;
        CacheFileSize = cacheFileSize;
        CacheFileHash = cacheFileHash;
        CacheFileSha1 = cacheFileSha1;
        CacheFileMatchesLocalProgress = cacheFileMatchesLocalProgress;
        EntryExists = entryExists;
        EntrySize = entrySize;
        EntrySha1 = entrySha1;
        EntryLocalTimeSeconds = entryLocalTimeSeconds;
        EntryTimeSeconds = entryTimeSeconds;
        EntryRemoteTimeSeconds = entryRemoteTimeSeconds;
        EntryMetadataMatchesLocalProgress = entryMetadataMatchesLocalProgress;
        EntryTimestampsAlignedWithCacheFile = entryTimestampsAlignedWithCacheFile;
        CacheFileStateText = cacheFileStateText ?? "未检查";
        RemoteCacheEntryStateText = remoteCacheEntryStateText ?? "未检查";
        SummaryText = summaryText ?? syncSupportText;
    }

    public string? CacheRootPath { get; }

    public string? CacheFilePath { get; }

    public string? RemoteCachePath { get; }

    public bool CanParticipateInSync { get; }

    public string SyncSupportText { get; }

    public bool CacheFileExists { get; }

    public DateTime? CacheFileLastWriteTimeUtc { get; }

    public long? CacheFileSize { get; }

    public string? CacheFileHash { get; }

    public string? CacheFileSha1 { get; }

    public bool CacheFileMatchesLocalProgress { get; }

    public bool EntryExists { get; }

    public long? EntrySize { get; }

    public string? EntrySha1 { get; }

    public long? EntryLocalTimeSeconds { get; }

    public long? EntryTimeSeconds { get; }

    public long? EntryRemoteTimeSeconds { get; }

    public bool EntryMetadataMatchesLocalProgress { get; }

    public bool EntryTimestampsAlignedWithCacheFile { get; }

    public string CacheFileStateText { get; }

    public string RemoteCacheEntryStateText { get; }

    public string SummaryText { get; }

    public string CacheFileLastWriteTimeText => CacheFileLastWriteTimeUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "缺失";

    public string CacheRootPathText => string.IsNullOrWhiteSpace(CacheRootPath) ? "未定位" : CacheRootPath;

    public string RemoteCachePathText => string.IsNullOrWhiteSpace(RemoteCachePath) ? "未定位" : RemoteCachePath;
}
