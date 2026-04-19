using System.Globalization;
using System.IO;

namespace Sts2SaveSyncTool.Services;

public sealed class SyncBackupService
{
    private readonly string _backupRoot;

    public SyncBackupService()
    {
        _backupRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SlayTheSpire2",
            "save_sync_tool",
            "backups");
    }

    internal string? BackupTargetFiles(string steamId, int profileId, SaveSide targetSide, IReadOnlyList<BackupTargetDescriptor> targets)
    {
        BackupTargetDescriptor[] existingFiles = targets
            .Where(item => File.Exists(item.FilePath))
            .GroupBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        if (existingFiles.Length == 0)
        {
            return null;
        }

        string backupDirectory = CreateUniqueBackupDirectory(steamId, profileId);
        Directory.CreateDirectory(backupDirectory);

        string prefix = targetSide == SaveSide.Normal ? "normal" : "modded";
        foreach (BackupTargetDescriptor existingFile in existingFiles)
        {
            string backupName = $"{prefix}.{existingFile.BackupName}";
            File.Copy(existingFile.FilePath, Path.Combine(backupDirectory, backupName), overwrite: true);
        }

        return backupDirectory;
    }

    private string CreateUniqueBackupDirectory(string steamId, int profileId)
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string baseDirectory = Path.Combine(_backupRoot, steamId, $"profile{profileId}", timestamp);
        string candidate = baseDirectory;
        int suffix = 1;

        while (Directory.Exists(candidate))
        {
            candidate = $"{baseDirectory}_{suffix}";
            suffix++;
        }

        return candidate;
    }
}
