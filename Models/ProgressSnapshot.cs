using System.Globalization;

namespace Sts2SaveSyncTool.Models;

public sealed class ProgressSnapshot
{
    public ProgressSnapshot(
        string sideName,
        string filePath,
        bool exists,
        string backupPath,
        SteamCacheSnapshot cacheSnapshot,
        DateTime? lastWriteTimeUtc = null,
        long? fileSize = null,
        string? hash = null,
        string? sha1Hash = null,
        bool backupExists = false,
        string? backupHash = null,
        long? totalPlaytime = null,
        int? floorsClimbed = null,
        int? totalUnlocks = null,
        int? preferredMultiplayerAscension = null,
        IReadOnlyList<CharacterAscensionSnapshot>? characterAscensions = null,
        string? parseError = null)
    {
        SideName = sideName;
        FilePath = filePath;
        Exists = exists;
        BackupPath = backupPath;
        CacheSnapshot = cacheSnapshot;
        LastWriteTimeUtc = lastWriteTimeUtc;
        FileSize = fileSize;
        Hash = hash;
        Sha1Hash = sha1Hash;
        BackupExists = backupExists;
        BackupHash = backupHash;
        TotalPlaytime = totalPlaytime;
        FloorsClimbed = floorsClimbed;
        TotalUnlocks = totalUnlocks;
        PreferredMultiplayerAscension = preferredMultiplayerAscension;
        CharacterAscensions = characterAscensions ?? Array.Empty<CharacterAscensionSnapshot>();
        ParseError = parseError;
    }

    public string SideName { get; }

    public string FilePath { get; }

    public bool Exists { get; }

    public string BackupPath { get; }

    public SteamCacheSnapshot CacheSnapshot { get; }

    public DateTime? LastWriteTimeUtc { get; }

    public long? FileSize { get; }

    public string? Hash { get; }

    public string? Sha1Hash { get; }

    public bool BackupExists { get; }

    public string? BackupHash { get; }

    public long? TotalPlaytime { get; }

    public int? FloorsClimbed { get; }

    public int? TotalUnlocks { get; }

    public int? PreferredMultiplayerAscension { get; }

    public IReadOnlyList<CharacterAscensionSnapshot> CharacterAscensions { get; }

    public string? ParseError { get; }

    public bool HasParseError => !string.IsNullOrWhiteSpace(ParseError);

    public bool HasValidData => Exists && !HasParseError;

    public bool CanUseAsSource => HasValidData
        && FileSize.HasValue
        && !string.IsNullOrWhiteSpace(Hash)
        && !string.IsNullOrWhiteSpace(Sha1Hash);

    public bool SupportsTargetBundleSync => CacheSnapshot.CanParticipateInSync;

    public string FileStateText
    {
        get
        {
            if (!Exists)
            {
                return "缺失";
            }

            return HasParseError ? $"解析失败：{ParseError}" : "可用";
        }
    }

    public string LastWriteTimeText => LastWriteTimeUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "缺失";

    public string HashText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Hash))
            {
                return "缺失";
            }

            return Hash.Length > 12 ? Hash[..12] : Hash;
        }
    }

    public string BackupStateText
    {
        get
        {
            if (!BackupExists)
            {
                return "缺失";
            }

            if (!Exists || string.IsNullOrWhiteSpace(Hash) || string.IsNullOrWhiteSpace(BackupHash))
            {
                return "存在";
            }

            return string.Equals(Hash, BackupHash, StringComparison.OrdinalIgnoreCase)
                ? "与 progress.save 一致"
                : "与 progress.save 不一致";
        }
    }

    public string TotalPlaytimeText => FormatNumber(TotalPlaytime);

    public string FloorsClimbedText => FormatNumber(FloorsClimbed);

    public string TotalUnlocksText => FormatNumber(TotalUnlocks);

    public string PreferredMultiplayerAscensionText => FormatNumber(PreferredMultiplayerAscension);

    public string CharacterSummaryText
    {
        get
        {
            if (!Exists)
            {
                return "无文件";
            }

            if (HasParseError)
            {
                return $"无法读取：{ParseError}";
            }

            if (CharacterAscensions.Count == 0)
            {
                return "未找到角色难度数据";
            }

            return string.Join(Environment.NewLine, CharacterAscensions.Select(item => item.SummaryText));
        }
    }

    private static string FormatNumber(long? value)
    {
        return value.HasValue ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : "缺失";
    }

    private static string FormatNumber(int? value)
    {
        return value.HasValue ? value.Value.ToString("N0", CultureInfo.InvariantCulture) : "缺失";
    }
}
