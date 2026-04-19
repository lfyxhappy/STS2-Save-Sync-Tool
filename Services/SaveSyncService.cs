using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sts2SaveSyncTool.Models;

namespace Sts2SaveSyncTool.Services;

public sealed class SaveSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly SyncBackupService _backupService;
    private readonly SteamCacheLocator _steamCacheLocator;
    private readonly string _accountRoot;

    public SaveSyncService(SyncBackupService backupService)
    {
        _backupService = backupService;
        _steamCacheLocator = new SteamCacheLocator();
        _accountRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlayTheSpire2",
            "steam");
    }

    public IReadOnlyList<SteamAccountState> DiscoverSteamAccounts()
    {
        if (!Directory.Exists(_accountRoot))
        {
            return Array.Empty<SteamAccountState>();
        }

        return Directory.GetDirectories(_accountRoot)
            .Where(IsSteamAccountDirectory)
            .Select(path => new SteamAccountState(
                Path.GetFileName(path),
                path,
                ReadLastProfileId(path)))
            .OrderBy(item => item.SteamId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<ProfilePairState> LoadProfilePairs(SteamAccountState account, string? steamRootOverride = null)
    {
        AccountContext context = BuildAccountContext(account, steamRootOverride, requireWritableCache: false);

        return Enumerable.Range(1, 3)
            .Select(profileId => LoadProfilePair(account, profileId, context))
            .ToArray();
    }

    public ProfilePairState LoadProfilePair(SteamAccountState account, int profileId, string? steamRootOverride = null)
    {
        AccountContext context = BuildAccountContext(account, steamRootOverride, requireWritableCache: false);
        return LoadProfilePair(account, profileId, context);
    }

    public SyncOperationResult Sync(SteamAccountState account, int profileId, SyncDirection direction, string? steamRootOverride = null)
    {
        bool sourceIsModded = direction == SyncDirection.ModdedToNormal;
        bool targetIsModded = direction == SyncDirection.NormalToModded;

        AccountContext context = BuildAccountContext(account, steamRootOverride, requireWritableCache: true);
        ProfilePairState currentPair = LoadProfilePair(account, profileId, context);
        ProgressSnapshot sourceSnapshot = sourceIsModded ? currentPair.ModdedSnapshot : currentPair.NormalSnapshot;
        ProgressSnapshot targetSnapshot = targetIsModded ? currentPair.ModdedSnapshot : currentPair.NormalSnapshot;

        string sourceLabel = sourceIsModded ? "Modded" : "普通档";
        string targetLabel = targetIsModded ? "Modded" : "普通档";

        if (!sourceSnapshot.Exists)
        {
            throw new InvalidOperationException($"{sourceLabel} 的 {BuildProfileName(profileId)} 不存在 progress.save。");
        }

        if (sourceSnapshot.HasParseError)
        {
            throw new InvalidOperationException($"{sourceLabel} 的 {BuildProfileName(profileId)} progress.save 无法解析，已取消同步。");
        }

        if (!targetSnapshot.SupportsTargetBundleSync)
        {
            throw new InvalidOperationException(targetSnapshot.CacheSnapshot.SyncSupportText);
        }

        byte[] sourceBytes = File.ReadAllBytes(sourceSnapshot.FilePath);
        _ = ParseProgress(sourceBytes, sourceSnapshot.FilePath);

        string sourceHash = ComputeSha256(sourceBytes);
        string sourceSha1 = ComputeSha1(sourceBytes);
        long sourceSize = sourceBytes.LongLength;

        SourceSnapshotForComparison comparisonSource = new(
            sourceSnapshot.FilePath,
            sourceHash,
            sourceSha1,
            sourceSize);

        if (IsTargetBundleConsistent(comparisonSource, targetSnapshot))
        {
            return new SyncOperationResult(
                currentPair,
                $"{BuildProfileName(profileId)} 的 {targetLabel} 目标副本已经完整一致，无需同步。",
                null);
        }

        SyncTargetBundle bundle = BuildTargetBundle(
            account,
            profileId,
            targetIsModded ? SaveSide.Modded : SaveSide.Normal,
            sourceLabel,
            targetLabel,
            sourceSnapshot.FilePath,
            sourceBytes,
            sourceHash,
            sourceSha1,
            sourceSize,
            context);

        IReadOnlyList<BackupTargetDescriptor> backupTargets =
        [
            new BackupTargetDescriptor("progress.save", bundle.AppDataProgressPath),
            new BackupTargetDescriptor("progress.save.backup", bundle.AppDataBackupPath),
            new BackupTargetDescriptor("remote.progress.save", bundle.CacheFilePath),
            new BackupTargetDescriptor("remotecache.vdf", bundle.RemoteCachePath)
        ];

        string? backupDirectory = _backupService.BackupTargetFiles(account.SteamId, profileId, bundle.TargetSide, backupTargets);
        ExecuteSyncTransaction(bundle);

        ProfilePairState updatedPair = LoadProfilePair(account, profileId, steamRootOverride);
        string message = backupDirectory is null
            ? $"已将 {BuildProfileName(profileId)} 从 {sourceLabel} 同步到 {targetLabel}，并更新了 Steam 本地缓存。"
            : $"已将 {BuildProfileName(profileId)} 从 {sourceLabel} 同步到 {targetLabel}，并更新了 Steam 本地缓存。备份目录：{backupDirectory}";

        return new SyncOperationResult(updatedPair, message, backupDirectory);
    }

    internal SteamCacheLocationResult ResolveSteamEnvironment(SteamAccountState account, string? steamRootOverride)
    {
        return _steamCacheLocator.Resolve(account.SteamId, steamRootOverride);
    }

    private ProfilePairState LoadProfilePair(SteamAccountState account, int profileId, AccountContext context)
    {
        string normalPath = GetProgressPath(account.RootPath, profileId, isModded: false);
        string moddedPath = GetProgressPath(account.RootPath, profileId, isModded: true);
        string normalBackupPath = GetProgressBackupPath(account.RootPath, profileId, isModded: false);
        string moddedBackupPath = GetProgressBackupPath(account.RootPath, profileId, isModded: true);

        ProgressSnapshot normalSnapshot = LoadSnapshot(
            "普通档",
            normalPath,
            normalBackupPath,
            BuildCacheRelativePath(profileId, isModded: false),
            context);
        ProgressSnapshot moddedSnapshot = LoadSnapshot(
            "Modded 档",
            moddedPath,
            moddedBackupPath,
            BuildCacheRelativePath(profileId, isModded: true),
            context);

        ProgressDiffSummary diffSummary = BuildDiff(normalSnapshot, moddedSnapshot);
        bool canSyncToModded = normalSnapshot.CanUseAsSource
            && moddedSnapshot.SupportsTargetBundleSync
            && !IsTargetBundleConsistent(normalSnapshot, moddedSnapshot);
        bool canSyncToNormal = moddedSnapshot.CanUseAsSource
            && normalSnapshot.SupportsTargetBundleSync
            && !IsTargetBundleConsistent(moddedSnapshot, normalSnapshot);

        return new ProfilePairState(
            profileId,
            account.LastProfileId == profileId,
            normalSnapshot,
            moddedSnapshot,
            diffSummary,
            canSyncToModded,
            canSyncToNormal);
    }

    private AccountContext BuildAccountContext(SteamAccountState account, string? steamRootOverride, bool requireWritableCache)
    {
        SteamCacheLocationResult location = _steamCacheLocator.Resolve(account.SteamId, steamRootOverride);
        if (requireWritableCache && !location.HasCacheRoot)
        {
            throw new InvalidOperationException(location.CacheStatusText);
        }

        RemoteCacheDocument? remoteCacheDocument = null;
        string? remoteCacheError = null;

        if (location.HasCacheRoot)
        {
            if (!File.Exists(location.RemoteCachePath!))
            {
                remoteCacheError = $"未找到 remotecache.vdf：{location.RemoteCachePath}";
            }
            else
            {
                try
                {
                    remoteCacheDocument = RemoteCacheDocument.Load(location.RemoteCachePath!);
                }
                catch (Exception ex)
                {
                    remoteCacheError = $"读取 remotecache.vdf 失败：{ex.Message}";
                }
            }
        }

        if (requireWritableCache && (remoteCacheDocument is null || !string.IsNullOrWhiteSpace(remoteCacheError)))
        {
            throw new InvalidOperationException(remoteCacheError ?? "无法读取 remotecache.vdf。");
        }

        return new AccountContext(location, remoteCacheDocument, remoteCacheError);
    }

    private ProgressSnapshot LoadSnapshot(
        string sideName,
        string filePath,
        string backupPath,
        string cacheRelativePath,
        AccountContext context)
    {
        bool exists = File.Exists(filePath);
        bool backupExists = File.Exists(backupPath);
        string? backupHash = backupExists ? ComputeSha256(File.ReadAllBytes(backupPath)) : null;

        if (!exists)
        {
            SteamCacheSnapshot missingCache = BuildCacheSnapshot(
                cacheRelativePath,
                context,
                expectedHash: null,
                expectedSha1: null,
                expectedSize: null,
                expectedLastWriteTimeUtc: null);

            return new ProgressSnapshot(
                sideName,
                filePath,
                exists: false,
                backupPath: backupPath,
                cacheSnapshot: missingCache,
                backupExists: backupExists,
                backupHash: backupHash);
        }

        DateTime lastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
        byte[] bytes = File.ReadAllBytes(filePath);
        long fileSize = bytes.LongLength;
        string hash = ComputeSha256(bytes);
        string sha1Hash = ComputeSha1(bytes);

        SteamCacheSnapshot cacheSnapshot = BuildCacheSnapshot(
            cacheRelativePath,
            context,
            hash,
            sha1Hash,
            fileSize,
            lastWriteTimeUtc);

        try
        {
            ProgressSaveDto parsed = ParseProgress(bytes, filePath);
            CharacterAscensionSnapshot[] characterAscensions = (parsed.CharacterStats ?? Array.Empty<CharacterStatDto>())
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .Select(item => new CharacterAscensionSnapshot(
                    item.Id!,
                    item.MaxAscension ?? 0,
                    item.PreferredAscension ?? 0))
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new ProgressSnapshot(
                sideName,
                filePath,
                exists: true,
                backupPath: backupPath,
                cacheSnapshot: cacheSnapshot,
                lastWriteTimeUtc: lastWriteTimeUtc,
                fileSize: fileSize,
                hash: hash,
                sha1Hash: sha1Hash,
                backupExists: backupExists,
                backupHash: backupHash,
                totalPlaytime: parsed.TotalPlaytime,
                floorsClimbed: parsed.FloorsClimbed,
                totalUnlocks: parsed.TotalUnlocks,
                preferredMultiplayerAscension: parsed.PreferredMultiplayerAscension,
                characterAscensions: characterAscensions);
        }
        catch (Exception ex)
        {
            return new ProgressSnapshot(
                sideName,
                filePath,
                exists: true,
                backupPath: backupPath,
                cacheSnapshot: cacheSnapshot,
                lastWriteTimeUtc: lastWriteTimeUtc,
                fileSize: fileSize,
                hash: hash,
                sha1Hash: sha1Hash,
                backupExists: backupExists,
                backupHash: backupHash,
                parseError: ex.Message);
        }
    }

    private SteamCacheSnapshot BuildCacheSnapshot(
        string cacheRelativePath,
        AccountContext context,
        string? expectedHash,
        string? expectedSha1,
        long? expectedSize,
        DateTime? expectedLastWriteTimeUtc)
    {
        string? cacheFilePath = context.Location.RemoteRootPath is null
            ? null
            : Path.Combine(context.Location.RemoteRootPath, cacheRelativePath.Replace('/', Path.DirectorySeparatorChar));
        bool cacheFileExists = !string.IsNullOrWhiteSpace(cacheFilePath) && File.Exists(cacheFilePath);
        DateTime? cacheFileLastWriteTimeUtc = cacheFileExists ? File.GetLastWriteTimeUtc(cacheFilePath!) : null;
        long? cacheFileSize = null;
        string? cacheFileHash = null;
        string? cacheFileSha1 = null;

        if (cacheFileExists)
        {
            byte[] cacheBytes = File.ReadAllBytes(cacheFilePath!);
            cacheFileSize = cacheBytes.LongLength;
            cacheFileHash = ComputeSha256(cacheBytes);
            cacheFileSha1 = ComputeSha1(cacheBytes);
        }

        if (!context.Location.HasCacheRoot)
        {
            return new SteamCacheSnapshot(
                context.Location.AppCacheRootPath,
                cacheFilePath,
                context.Location.RemoteCachePath,
                canParticipateInSync: false,
                syncSupportText: context.Location.CacheStatusText,
                cacheFileExists: cacheFileExists,
                cacheFileLastWriteTimeUtc: cacheFileLastWriteTimeUtc,
                cacheFileSize: cacheFileSize,
                cacheFileHash: cacheFileHash,
                cacheFileSha1: cacheFileSha1,
                cacheFileStateText: "缓存根目录不可用",
                remoteCacheEntryStateText: "无法读取",
                summaryText: context.Location.CacheStatusText);
        }

        if (!string.IsNullOrWhiteSpace(context.RemoteCacheError) || context.RemoteCacheDocument is null)
        {
            return new SteamCacheSnapshot(
                context.Location.AppCacheRootPath,
                cacheFilePath,
                context.Location.RemoteCachePath,
                canParticipateInSync: false,
                syncSupportText: context.RemoteCacheError ?? "无法读取 remotecache.vdf。",
                cacheFileExists: cacheFileExists,
                cacheFileLastWriteTimeUtc: cacheFileLastWriteTimeUtc,
                cacheFileSize: cacheFileSize,
                cacheFileHash: cacheFileHash,
                cacheFileSha1: cacheFileSha1,
                cacheFileStateText: cacheFileExists ? "存在" : "缺失",
                remoteCacheEntryStateText: "无法读取",
                summaryText: context.RemoteCacheError ?? "无法读取 remotecache.vdf。");
        }

        bool entryExists = context.RemoteCacheDocument.TryGetEntry(cacheRelativePath, out RemoteCacheEntry? entry);
        long? entrySize = entry?.Size;
        string? entrySha1 = entry?.Sha1;
        long? entryLocalTime = entry?.LocalTime;
        long? entryTime = entry?.Time;
        long? entryRemoteTime = entry?.RemoteTime;

        if (string.IsNullOrWhiteSpace(expectedHash) || string.IsNullOrWhiteSpace(expectedSha1) || !expectedSize.HasValue || !expectedLastWriteTimeUtc.HasValue)
        {
            return new SteamCacheSnapshot(
                context.Location.AppCacheRootPath,
                cacheFilePath,
                context.Location.RemoteCachePath,
                canParticipateInSync: true,
                syncSupportText: "可参与同步",
                cacheFileExists: cacheFileExists,
                cacheFileLastWriteTimeUtc: cacheFileLastWriteTimeUtc,
                cacheFileSize: cacheFileSize,
                cacheFileHash: cacheFileHash,
                cacheFileSha1: cacheFileSha1,
                entryExists: entryExists,
                entrySize: entrySize,
                entrySha1: entrySha1,
                entryLocalTimeSeconds: entryLocalTime,
                entryTimeSeconds: entryTime,
                entryRemoteTimeSeconds: entryRemoteTime,
                cacheFileStateText: cacheFileExists ? "存在" : "缺失",
                remoteCacheEntryStateText: entryExists ? "存在" : "缺失",
                summaryText: "未检查（progress.save 缺失）");
        }

        bool cacheFileMatchesLocalProgress = cacheFileExists
            && cacheFileSize == expectedSize
            && string.Equals(cacheFileHash, expectedHash, StringComparison.OrdinalIgnoreCase)
            && string.Equals(cacheFileSha1, expectedSha1, StringComparison.OrdinalIgnoreCase);

        long expectedTimestamp = ToUnixSeconds(expectedLastWriteTimeUtc.Value);
        bool entryMetadataMatchesLocalProgress = entryExists
            && entrySize == expectedSize
            && string.Equals(entrySha1, expectedSha1, StringComparison.OrdinalIgnoreCase);
        bool entryTimesMatchProgress = entryExists
            && entryLocalTime == expectedTimestamp
            && entryTime == expectedTimestamp
            && entryRemoteTime == expectedTimestamp;
        bool entryTimestampsAlignedWithCacheFile = cacheFileExists
            && cacheFileLastWriteTimeUtc.HasValue
            && entryExists
            && entryLocalTime == ToUnixSeconds(cacheFileLastWriteTimeUtc.Value)
            && entryTime == ToUnixSeconds(cacheFileLastWriteTimeUtc.Value)
            && entryRemoteTime == ToUnixSeconds(cacheFileLastWriteTimeUtc.Value);

        string cacheFileStateText = !cacheFileExists
            ? "缺失，可补建"
            : cacheFileMatchesLocalProgress
                ? "与本地 progress.save 一致"
                : "与本地 progress.save 不一致";

        string remoteCacheEntryStateText = !entryExists
            ? "缺失，可补建"
            : entryMetadataMatchesLocalProgress && entryTimesMatchProgress
                ? "元数据一致"
                : "元数据与本地档不一致";

        string summaryText = cacheFileMatchesLocalProgress && entryMetadataMatchesLocalProgress && entryTimesMatchProgress && entryTimestampsAlignedWithCacheFile
            ? "缓存正常"
            : !entryExists
                ? "缓存条目缺失，可补建"
                : !cacheFileExists
                    ? "缓存文件缺失，可补建"
                    : "缓存内容或元数据与本地档不一致，建议同步修复";

        return new SteamCacheSnapshot(
            context.Location.AppCacheRootPath,
            cacheFilePath,
            context.Location.RemoteCachePath,
            canParticipateInSync: true,
            syncSupportText: "可参与同步",
            cacheFileExists: cacheFileExists,
            cacheFileLastWriteTimeUtc: cacheFileLastWriteTimeUtc,
            cacheFileSize: cacheFileSize,
            cacheFileHash: cacheFileHash,
            cacheFileSha1: cacheFileSha1,
            cacheFileMatchesLocalProgress: cacheFileMatchesLocalProgress,
            entryExists: entryExists,
            entrySize: entrySize,
            entrySha1: entrySha1,
            entryLocalTimeSeconds: entryLocalTime,
            entryTimeSeconds: entryTime,
            entryRemoteTimeSeconds: entryRemoteTime,
            entryMetadataMatchesLocalProgress: entryMetadataMatchesLocalProgress,
            entryTimestampsAlignedWithCacheFile: entryTimestampsAlignedWithCacheFile,
            cacheFileStateText: cacheFileStateText,
            remoteCacheEntryStateText: remoteCacheEntryStateText,
            summaryText: summaryText);
    }

    private ProgressDiffSummary BuildDiff(ProgressSnapshot normal, ProgressSnapshot modded)
    {
        bool bothMissing = !normal.Exists && !modded.Exists;
        bool hashesEqual = normal.Exists
            && modded.Exists
            && !string.IsNullOrWhiteSpace(normal.Hash)
            && string.Equals(normal.Hash, modded.Hash, StringComparison.OrdinalIgnoreCase);

        List<string> globalLines =
        [
            $"文件状态：普通 {normal.FileStateText} | Modded {modded.FileStateText}",
            $"修改时间：普通 {normal.LastWriteTimeText} | Modded {modded.LastWriteTimeText}",
            $"总时长：普通 {normal.TotalPlaytimeText} | Modded {modded.TotalPlaytimeText}",
            $"爬楼层数：普通 {normal.FloorsClimbedText} | Modded {modded.FloorsClimbedText}",
            $"总解锁：普通 {normal.TotalUnlocksText} | Modded {modded.TotalUnlocksText}",
            $"多人难度偏好：普通 {normal.PreferredMultiplayerAscensionText} | Modded {modded.PreferredMultiplayerAscensionText}"
        ];

        List<string> characterDifferenceLines = BuildCharacterDifferenceLines(normal, modded);
        List<string> cacheConsistencyLines =
        [
            $"普通档缓存：{normal.CacheSnapshot.SummaryText}",
            $"Modded 缓存：{modded.CacheSnapshot.SummaryText}"
        ];

        string summaryText = bothMissing
            ? "两侧都缺失"
            : !normal.Exists || !modded.Exists
                ? "一侧缺失"
                : normal.HasParseError || modded.HasParseError
                    ? "存在解析问题"
                    : hashesEqual
                        ? IsSideBundleHealthy(normal) && IsSideBundleHealthy(modded)
                            ? "无差异"
                            : "进度内容一致，但缓存或备份需要修复"
                        : "存在差异";

        return new ProgressDiffSummary(
            bothMissing,
            hashesEqual,
            globalLines,
            characterDifferenceLines,
            cacheConsistencyLines,
            summaryText);
    }

    private static List<string> BuildCharacterDifferenceLines(ProgressSnapshot normal, ProgressSnapshot modded)
    {
        if (!normal.Exists && !modded.Exists)
        {
            return ["角色难度：两侧都缺失"];
        }

        if (!normal.HasValidData || !modded.HasValidData)
        {
            return [$"角色难度：无法完整比较（普通 {normal.FileStateText} | Modded {modded.FileStateText}）"];
        }

        Dictionary<string, CharacterAscensionSnapshot> normalMap = normal.CharacterAscensions
            .ToDictionary(item => item.Id, item => item, StringComparer.OrdinalIgnoreCase);
        Dictionary<string, CharacterAscensionSnapshot> moddedMap = modded.CharacterAscensions
            .ToDictionary(item => item.Id, item => item, StringComparer.OrdinalIgnoreCase);

        List<string> lines = new();
        foreach (string characterId in normalMap.Keys.Union(moddedMap.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
        {
            normalMap.TryGetValue(characterId, out CharacterAscensionSnapshot? normalCharacter);
            moddedMap.TryGetValue(characterId, out CharacterAscensionSnapshot? moddedCharacter);

            if (normalCharacter is null || moddedCharacter is null)
            {
                string displayName = normalCharacter?.DisplayName ?? moddedCharacter?.DisplayName ?? characterId;
                lines.Add($"{displayName}：普通 {FormatCharacterState(normalCharacter)} | Modded {FormatCharacterState(moddedCharacter)}");
                continue;
            }

            if (normalCharacter.MaxAscension != moddedCharacter.MaxAscension
                || normalCharacter.PreferredAscension != moddedCharacter.PreferredAscension)
            {
                lines.Add(
                    $"{normalCharacter.DisplayName}：普通 A{normalCharacter.MaxAscension} / 预设 {normalCharacter.PreferredAscension} | Modded A{moddedCharacter.MaxAscension} / 预设 {moddedCharacter.PreferredAscension}");
            }
        }

        if (lines.Count == 0)
        {
            lines.Add("角色难度：无差异");
        }

        return lines;
    }

    private SyncTargetBundle BuildTargetBundle(
        SteamAccountState account,
        int profileId,
        SaveSide targetSide,
        string sourceLabel,
        string targetLabel,
        string sourcePath,
        byte[] sourceBytes,
        string sourceHash,
        string sourceSha1,
        long sourceSize,
        AccountContext context)
    {
        string targetRoot = GetProfileRoot(account.RootPath, profileId, targetSide == SaveSide.Modded);
        string targetProgressPath = Path.Combine(targetRoot, "saves", "progress.save");
        string targetBackupPath = Path.Combine(targetRoot, "saves", "progress.save.backup");
        string cacheRelativePath = BuildCacheRelativePath(profileId, targetSide == SaveSide.Modded);
        string cacheFilePath = Path.Combine(context.Location.RemoteRootPath!, cacheRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string remoteCachePath = context.Location.RemoteCachePath!;
        DateTime canonicalTimestampUtc = TruncateToWholeSecond(DateTime.UtcNow);
        long canonicalTimestampSeconds = ToUnixSeconds(canonicalTimestampUtc);

        RemoteCacheDocument remoteCacheDocument = RemoteCacheDocument.Load(remoteCachePath);
        RemoteCacheEntry cacheEntry = remoteCacheDocument.GetOrAddEntry(cacheRelativePath);
        cacheEntry.ApplyMetadata(sourceSize, sourceSha1, canonicalTimestampSeconds);

        return new SyncTargetBundle(
            account.SteamId,
            profileId,
            targetSide,
            sourceLabel,
            targetLabel,
            sourcePath,
            sourceBytes,
            sourceHash,
            sourceSha1,
            sourceSize,
            targetProgressPath,
            targetBackupPath,
            cacheFilePath,
            remoteCachePath,
            cacheRelativePath,
            canonicalTimestampUtc,
            remoteCacheDocument.ToBytes());
    }

    private void ExecuteSyncTransaction(SyncTargetBundle bundle)
    {
        FileStateSnapshot[] originalStates =
        [
            CaptureFileState(bundle.AppDataProgressPath),
            CaptureFileState(bundle.AppDataBackupPath),
            CaptureFileState(bundle.CacheFilePath),
            CaptureFileState(bundle.RemoteCachePath)
        ];

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(bundle.AppDataProgressPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(bundle.AppDataBackupPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(bundle.CacheFilePath)!);

            File.WriteAllBytes(bundle.AppDataProgressPath, bundle.SourceBytes);
            File.WriteAllBytes(bundle.AppDataBackupPath, bundle.SourceBytes);
            File.WriteAllBytes(bundle.CacheFilePath, bundle.SourceBytes);

            File.SetLastWriteTimeUtc(bundle.AppDataProgressPath, bundle.CanonicalTimestampUtc);
            File.SetLastWriteTimeUtc(bundle.CacheFilePath, bundle.CanonicalTimestampUtc);

            WriteAllBytesAtomic(bundle.RemoteCachePath, bundle.UpdatedRemoteCacheBytes);
        }
        catch (Exception ex)
        {
            try
            {
                RestoreOriginalStates(originalStates);
            }
            catch (Exception rollbackEx)
            {
                throw new InvalidOperationException($"同步失败，且回滚未完全成功：{rollbackEx.Message}", ex);
            }

            throw new InvalidOperationException($"同步失败，已回滚：{ex.Message}", ex);
        }
    }

    private static FileStateSnapshot CaptureFileState(string path)
    {
        if (!File.Exists(path))
        {
            return new FileStateSnapshot(path, Existed: false, Bytes: null, LastWriteTimeUtc: null);
        }

        return new FileStateSnapshot(
            path,
            Existed: true,
            Bytes: File.ReadAllBytes(path),
            LastWriteTimeUtc: File.GetLastWriteTimeUtc(path));
    }

    private static void RestoreOriginalStates(IEnumerable<FileStateSnapshot> originalStates)
    {
        List<string> errors = new();

        foreach (FileStateSnapshot originalState in originalStates)
        {
            try
            {
                if (originalState.Existed)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(originalState.Path)!);
                    WriteAllBytesAtomic(originalState.Path, originalState.Bytes!);

                    if (originalState.LastWriteTimeUtc.HasValue)
                    {
                        File.SetLastWriteTimeUtc(originalState.Path, originalState.LastWriteTimeUtc.Value);
                    }
                }
                else if (File.Exists(originalState.Path))
                {
                    File.Delete(originalState.Path);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{originalState.Path}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" | ", errors));
        }
    }

    private static void WriteAllBytesAtomic(string path, byte[] bytes)
    {
        string? directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException($"无法解析目录：{path}");
        }

        Directory.CreateDirectory(directory);
        string tempPath = Path.Combine(directory, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllBytes(tempPath, bytes);

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static bool IsTargetBundleConsistent(ProgressSnapshot source, ProgressSnapshot target)
    {
        if (!source.CanUseAsSource)
        {
            return false;
        }

        SourceSnapshotForComparison comparison = new(
            source.FilePath,
            source.Hash!,
            source.Sha1Hash!,
            source.FileSize!.Value);

        return IsTargetBundleConsistent(comparison, target);
    }

    private static bool IsTargetBundleConsistent(SourceSnapshotForComparison source, ProgressSnapshot target)
    {
        if (!target.Exists || !target.SupportsTargetBundleSync || !target.LastWriteTimeUtc.HasValue)
        {
            return false;
        }

        if (!string.Equals(target.Hash, source.Sha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(target.BackupHash, source.Sha256Hash, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!target.CacheSnapshot.CacheFileExists
            || target.CacheSnapshot.CacheFileSize != source.FileSize
            || !string.Equals(target.CacheSnapshot.CacheFileHash, source.Sha256Hash, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(target.CacheSnapshot.CacheFileSha1, source.Sha1Hash, StringComparison.OrdinalIgnoreCase)
            || !target.CacheSnapshot.CacheFileLastWriteTimeUtc.HasValue)
        {
            return false;
        }

        long progressTimestamp = ToUnixSeconds(target.LastWriteTimeUtc.Value);
        long cacheTimestamp = ToUnixSeconds(target.CacheSnapshot.CacheFileLastWriteTimeUtc.Value);
        if (progressTimestamp != cacheTimestamp)
        {
            return false;
        }

        return target.CacheSnapshot.EntryExists
            && target.CacheSnapshot.EntrySize == source.FileSize
            && string.Equals(target.CacheSnapshot.EntrySha1, source.Sha1Hash, StringComparison.OrdinalIgnoreCase)
            && target.CacheSnapshot.EntryLocalTimeSeconds == progressTimestamp
            && target.CacheSnapshot.EntryTimeSeconds == progressTimestamp
            && target.CacheSnapshot.EntryRemoteTimeSeconds == progressTimestamp;
    }

    private static bool IsSideBundleHealthy(ProgressSnapshot snapshot)
    {
        return snapshot.CanUseAsSource && IsTargetBundleConsistent(snapshot, snapshot);
    }

    private static bool IsSteamAccountDirectory(string accountPath)
    {
        return File.Exists(Path.Combine(accountPath, "profile.save"))
            || Directory.Exists(Path.Combine(accountPath, "modded"))
            || Directory.Exists(Path.Combine(accountPath, "profile1"))
            || Directory.Exists(Path.Combine(accountPath, "profile2"))
            || Directory.Exists(Path.Combine(accountPath, "profile3"));
    }

    private static int ReadLastProfileId(string accountRoot)
    {
        string profileSelectionPath = Path.Combine(accountRoot, "profile.save");
        if (!File.Exists(profileSelectionPath))
        {
            return 1;
        }

        try
        {
            string json = File.ReadAllText(profileSelectionPath);
            RootProfileSelectionDto? dto = JsonSerializer.Deserialize<RootProfileSelectionDto>(json, JsonOptions);
            if (dto?.LastProfileId is >= 1 and <= 3)
            {
                return dto.LastProfileId.Value;
            }
        }
        catch
        {
        }

        return 1;
    }

    private static string FormatCharacterState(CharacterAscensionSnapshot? character)
    {
        return character is null
            ? "缺失"
            : $"A{character.MaxAscension} / 预设 {character.PreferredAscension}";
    }

    private static string GetProgressPath(string accountRoot, int profileId, bool isModded)
    {
        return Path.Combine(GetProfileRoot(accountRoot, profileId, isModded), "saves", "progress.save");
    }

    private static string GetProgressBackupPath(string accountRoot, int profileId, bool isModded)
    {
        return Path.Combine(GetProfileRoot(accountRoot, profileId, isModded), "saves", "progress.save.backup");
    }

    private static string GetProfileRoot(string accountRoot, int profileId, bool isModded)
    {
        return isModded
            ? Path.Combine(accountRoot, "modded", $"profile{profileId}")
            : Path.Combine(accountRoot, $"profile{profileId}");
    }

    private static string BuildProfileName(int profileId)
    {
        return $"Profile {profileId}";
    }

    private static string BuildCacheRelativePath(int profileId, bool isModded)
    {
        return isModded
            ? $"modded/profile{profileId}/saves/progress.save"
            : $"profile{profileId}/saves/progress.save";
    }

    private static string ComputeSha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string ComputeSha1(byte[] bytes)
    {
        return Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
    }

    private static ProgressSaveDto ParseProgress(byte[] bytes, string filePath)
    {
        try
        {
            ProgressSaveDto? parsed = JsonSerializer.Deserialize<ProgressSaveDto>(bytes, JsonOptions);
            if (parsed is null)
            {
                throw new JsonException("文件内容为空。");
            }

            return parsed;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{filePath} 不是有效的 progress.save JSON：{ex.Message}", ex);
        }
    }

    private static DateTime TruncateToWholeSecond(DateTime value)
    {
        long ticks = value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond);
        return new DateTime(ticks, DateTimeKind.Utc);
    }

    private static long ToUnixSeconds(DateTime value)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeSeconds();
    }

    private sealed class RootProfileSelectionDto
    {
        [JsonPropertyName("last_profile_id")]
        public int? LastProfileId { get; init; }
    }

    private sealed class ProgressSaveDto
    {
        [JsonPropertyName("total_playtime")]
        public long? TotalPlaytime { get; init; }

        [JsonPropertyName("floors_climbed")]
        public int? FloorsClimbed { get; init; }

        [JsonPropertyName("total_unlocks")]
        public int? TotalUnlocks { get; init; }

        [JsonPropertyName("preferred_multiplayer_ascension")]
        public int? PreferredMultiplayerAscension { get; init; }

        [JsonPropertyName("character_stats")]
        public CharacterStatDto[]? CharacterStats { get; init; }
    }

    private sealed class CharacterStatDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("max_ascension")]
        public int? MaxAscension { get; init; }

        [JsonPropertyName("preferred_ascension")]
        public int? PreferredAscension { get; init; }
    }

    private sealed record AccountContext(
        SteamCacheLocationResult Location,
        RemoteCacheDocument? RemoteCacheDocument,
        string? RemoteCacheError);

    private sealed record FileStateSnapshot(
        string Path,
        bool Existed,
        byte[]? Bytes,
        DateTime? LastWriteTimeUtc);

    private sealed record SourceSnapshotForComparison(
        string SourcePath,
        string Sha256Hash,
        string Sha1Hash,
        long FileSize);
}
