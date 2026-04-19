using Sts2SaveSyncTool.Models;

namespace Sts2SaveSyncTool.Services;

public enum SyncDirection
{
    NormalToModded,
    ModdedToNormal
}

internal enum SaveSide
{
    Normal,
    Modded
}

internal sealed record BackupTargetDescriptor(string BackupName, string FilePath);

public sealed class SyncOperationResult
{
    public SyncOperationResult(ProfilePairState updatedPair, string message, string? backupDirectory)
    {
        UpdatedPair = updatedPair;
        Message = message;
        BackupDirectory = backupDirectory;
    }

    public ProfilePairState UpdatedPair { get; }

    public string Message { get; }

    public string? BackupDirectory { get; }
}
