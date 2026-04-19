using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using Sts2SaveSyncTool.Infrastructure;
using Sts2SaveSyncTool.Models;
using Sts2SaveSyncTool.Services;

namespace Sts2SaveSyncTool.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly SaveSyncService _saveSyncService;
    private readonly ToolSettingsService _toolSettingsService;
    private SteamAccountState? _selectedAccount;
    private bool _isBusy;
    private string _statusMessage = "准备就绪。";
    private Brush _statusBrush = Brushes.LightGray;
    private string _steamRootOverride = string.Empty;
    private string _effectiveSteamRootText = "Steam 根目录：未检测";
    private string _cacheEnvironmentText = "当前账号缓存：未检测";

    public MainWindowViewModel()
        : this(new SaveSyncService(new SyncBackupService()), new ToolSettingsService())
    {
    }

    internal MainWindowViewModel(SaveSyncService saveSyncService, ToolSettingsService toolSettingsService)
    {
        _saveSyncService = saveSyncService;
        _toolSettingsService = toolSettingsService;
    }

    public ObservableCollection<SteamAccountState> SteamAccounts { get; } = [];

    public ObservableCollection<ProfilePairState> ProfilePairs { get; } = [];

    public SteamAccountState? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string SteamRootOverride
    {
        get => _steamRootOverride;
        set => SetProperty(ref _steamRootOverride, value);
    }

    public string EffectiveSteamRootText
    {
        get => _effectiveSteamRootText;
        private set => SetProperty(ref _effectiveSteamRootText, value);
    }

    public string CacheEnvironmentText
    {
        get => _cacheEnvironmentText;
        private set => SetProperty(ref _cacheEnvironmentText, value);
    }

    public string SafetyTip => "建议关闭游戏后同步；Steam 离线模式也可能从本地缓存回写，本工具会一并更新缓存镜像与 remotecache.vdf。";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public Brush StatusBrush
    {
        get => _statusBrush;
        private set => SetProperty(ref _statusBrush, value);
    }

    public async Task InitializeAsync()
    {
        ToolSettings settings = _toolSettingsService.Load();
        SteamRootOverride = settings.SteamRootOverride ?? string.Empty;
        await RefreshAccountsAsync();
    }

    public async Task RefreshAccountsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        SetStatus("正在扫描 Steam 存档账号...");

        try
        {
            string? previousSteamId = SelectedAccount?.SteamId;
            IReadOnlyList<SteamAccountState> accounts = await Task.Run(_saveSyncService.DiscoverSteamAccounts);

            SteamAccounts.Clear();
            foreach (SteamAccountState account in accounts)
            {
                SteamAccounts.Add(account);
            }

            if (accounts.Count == 0)
            {
                SelectedAccount = null;
                ProfilePairs.Clear();
                EffectiveSteamRootText = "Steam 根目录：未检测";
                CacheEnvironmentText = "当前账号缓存：未检测";
                SetStatus("未找到 Steam 存档账号目录。", isError: true);
                return;
            }

            SelectedAccount = accounts.FirstOrDefault(account => account.SteamId == previousSteamId) ?? accounts[0];
            await LoadProfilePairsCoreAsync(SelectedAccount);
        }
        catch (Exception ex)
        {
            ProfilePairs.Clear();
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectAccountAsync(SteamAccountState? account)
    {
        if (account is null || IsBusy)
        {
            return;
        }

        if (SelectedAccount?.SteamId == account.SteamId && ProfilePairs.Count > 0)
        {
            return;
        }

        SelectedAccount = account;
        await RefreshSelectedAccountAsync();
    }

    public async Task RefreshSelectedAccountAsync()
    {
        if (SelectedAccount is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        SetStatus($"正在读取账号 {SelectedAccount.SteamId} 的档位状态...");

        try
        {
            await LoadProfilePairsCoreAsync(SelectedAccount);
        }
        catch (Exception ex)
        {
            ProfilePairs.Clear();
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task ApplySteamRootOverrideAsync()
    {
        if (IsBusy)
        {
            return;
        }

        string? normalizedPath;
        try
        {
            normalizedPath = NormalizeSteamRootOverride(SteamRootOverride);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(normalizedPath) && !Directory.Exists(normalizedPath))
        {
            SetStatus($"Steam 根目录不存在：{normalizedPath}", isError: true);
            return;
        }

        SteamRootOverride = normalizedPath ?? string.Empty;
        _toolSettingsService.Save(new ToolSettings { SteamRootOverride = normalizedPath });
        SetStatus(string.IsNullOrWhiteSpace(normalizedPath) ? "已清空 Steam 根目录覆盖。" : $"已保存 Steam 根目录覆盖：{normalizedPath}");

        if (SelectedAccount is not null)
        {
            await RefreshSelectedAccountAsync();
        }
    }

    public async Task ClearSteamRootOverrideAsync()
    {
        if (IsBusy)
        {
            return;
        }

        SteamRootOverride = string.Empty;
        _toolSettingsService.Save(new ToolSettings());
        SetStatus("已清空 Steam 根目录覆盖。");

        if (SelectedAccount is not null)
        {
            await RefreshSelectedAccountAsync();
        }
    }

    public async Task SyncAsync(ProfilePairState? pair, SyncDirection direction)
    {
        if (pair is null || SelectedAccount is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        SetStatus($"正在同步 {pair.ProfileTitle}...");

        try
        {
            SyncOperationResult result = await Task.Run(() => _saveSyncService.Sync(SelectedAccount, pair.ProfileId, direction, GetSteamRootOverrideOrNull()));
            ReplaceProfilePair(result.UpdatedPair);
            SetStatus(result.Message);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadProfilePairsCoreAsync(SteamAccountState account)
    {
        string? steamRootOverride = GetSteamRootOverrideOrNull();
        IReadOnlyList<ProfilePairState> pairs = await Task.Run(() => _saveSyncService.LoadProfilePairs(account, steamRootOverride));
        SteamCacheLocationResult environment = _saveSyncService.ResolveSteamEnvironment(account, steamRootOverride);

        ProfilePairs.Clear();
        foreach (ProfilePairState pair in pairs)
        {
            ProfilePairs.Add(pair);
        }

        EffectiveSteamRootText = $"Steam 根目录：{environment.SteamRootText}";
        string cacheStatus = pairs.FirstOrDefault()?.NormalSnapshot.CacheSnapshot.SyncSupportText ?? environment.CacheStatusText;
        CacheEnvironmentText = $"当前账号缓存：{cacheStatus}";
        SetStatus($"已加载账号 {account.SteamId} 的 Profile 1-3。");
    }

    private void ReplaceProfilePair(ProfilePairState updatedPair)
    {
        ProfilePairState? existing = ProfilePairs.FirstOrDefault(item => item.ProfileId == updatedPair.ProfileId);
        if (existing is not null)
        {
            existing.UpdateFrom(updatedPair);
            return;
        }

        ProfilePairs.Add(updatedPair);
    }

    private string? GetSteamRootOverrideOrNull()
    {
        return NormalizeSteamRootOverride(SteamRootOverride);
    }

    private static string? NormalizeSteamRootOverride(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
        try
        {
            return Path.GetFullPath(expanded);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Steam 根目录无效：{ex.Message}", ex);
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        StatusBrush = isError ? Brushes.OrangeRed : Brushes.LightGreen;
    }
}
