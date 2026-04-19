using Sts2SaveSyncTool.Infrastructure;

namespace Sts2SaveSyncTool.Models;

public sealed class ProfilePairState : ObservableObject
{
    private bool _isCurrentProfile;
    private ProgressSnapshot _normalSnapshot;
    private ProgressSnapshot _moddedSnapshot;
    private ProgressDiffSummary _diffSummary;
    private bool _canSyncToModded;
    private bool _canSyncToNormal;

    public ProfilePairState(
        int profileId,
        bool isCurrentProfile,
        ProgressSnapshot normalSnapshot,
        ProgressSnapshot moddedSnapshot,
        ProgressDiffSummary diffSummary,
        bool canSyncToModded,
        bool canSyncToNormal)
    {
        ProfileId = profileId;
        _isCurrentProfile = isCurrentProfile;
        _normalSnapshot = normalSnapshot;
        _moddedSnapshot = moddedSnapshot;
        _diffSummary = diffSummary;
        _canSyncToModded = canSyncToModded;
        _canSyncToNormal = canSyncToNormal;
    }

    public int ProfileId { get; }

    public string ProfileTitle => $"Profile {ProfileId}";

    public bool IsCurrentProfile
    {
        get => _isCurrentProfile;
        private set => SetProperty(ref _isCurrentProfile, value);
    }

    public ProgressSnapshot NormalSnapshot
    {
        get => _normalSnapshot;
        private set => SetProperty(ref _normalSnapshot, value);
    }

    public ProgressSnapshot ModdedSnapshot
    {
        get => _moddedSnapshot;
        private set => SetProperty(ref _moddedSnapshot, value);
    }

    public ProgressDiffSummary DiffSummary
    {
        get => _diffSummary;
        private set => SetProperty(ref _diffSummary, value);
    }

    public bool CanSyncToModded
    {
        get => _canSyncToModded;
        private set => SetProperty(ref _canSyncToModded, value);
    }

    public bool CanSyncToNormal
    {
        get => _canSyncToNormal;
        private set => SetProperty(ref _canSyncToNormal, value);
    }

    public void UpdateFrom(ProfilePairState updated)
    {
        Update(
            updated.NormalSnapshot,
            updated.ModdedSnapshot,
            updated.DiffSummary,
            updated.IsCurrentProfile,
            updated.CanSyncToModded,
            updated.CanSyncToNormal);
    }

    public void Update(
        ProgressSnapshot normalSnapshot,
        ProgressSnapshot moddedSnapshot,
        ProgressDiffSummary diffSummary,
        bool isCurrentProfile,
        bool canSyncToModded,
        bool canSyncToNormal)
    {
        NormalSnapshot = normalSnapshot;
        ModdedSnapshot = moddedSnapshot;
        DiffSummary = diffSummary;
        IsCurrentProfile = isCurrentProfile;
        CanSyncToModded = canSyncToModded;
        CanSyncToNormal = canSyncToNormal;
    }
}
