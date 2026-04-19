using System.Globalization;
using System.IO;
using Microsoft.Win32;

namespace Sts2SaveSyncTool.Services;

internal sealed class SteamCacheLocator
{
    private const ulong AccountIdModulo = 4_294_967_296;
    private const string AppId = "2868840";

    public SteamCacheLocationResult Resolve(string steamId64, string? steamRootOverride)
    {
        string? effectiveRoot = NormalizeSteamRoot(steamRootOverride);
        string steamRootSource = "手动覆盖";

        if (string.IsNullOrWhiteSpace(effectiveRoot))
        {
            effectiveRoot = NormalizeSteamRoot(ReadRegistrySteamRoot());
            steamRootSource = "注册表";
        }

        if (string.IsNullOrWhiteSpace(effectiveRoot))
        {
            return new SteamCacheLocationResult(
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                "未找到 Steam 根目录，请在上方手动填写 Steam 安装目录。");
        }

        if (!ulong.TryParse(steamId64, NumberStyles.None, CultureInfo.InvariantCulture, out ulong steamIdValue))
        {
            return new SteamCacheLocationResult(
                true,
                effectiveRoot,
                steamRootSource,
                null,
                null,
                null,
                null,
                $"账号 {steamId64} 不是有效的 SteamID64。");
        }

        string userdataDirectoryName = (steamIdValue % AccountIdModulo).ToString(CultureInfo.InvariantCulture);
        string userdataRoot = Path.Combine(effectiveRoot, "userdata");
        string appCacheRoot = Path.Combine(userdataRoot, userdataDirectoryName, AppId);
        string remoteRoot = Path.Combine(appCacheRoot, "remote");
        string remoteCachePath = Path.Combine(appCacheRoot, "remotecache.vdf");

        if (!Directory.Exists(userdataRoot))
        {
            return new SteamCacheLocationResult(
                true,
                effectiveRoot,
                steamRootSource,
                userdataDirectoryName,
                appCacheRoot,
                remoteRoot,
                remoteCachePath,
                $"Steam 根目录 {effectiveRoot} 下缺少 userdata 目录。");
        }

        if (!Directory.Exists(appCacheRoot))
        {
            return new SteamCacheLocationResult(
                true,
                effectiveRoot,
                steamRootSource,
                userdataDirectoryName,
                appCacheRoot,
                remoteRoot,
                remoteCachePath,
                $"未找到 STS2 本地缓存目录：{appCacheRoot}");
        }

        return new SteamCacheLocationResult(
            true,
            effectiveRoot,
            steamRootSource,
            userdataDirectoryName,
            appCacheRoot,
            remoteRoot,
            remoteCachePath,
            null);
    }

    private static string? ReadRegistrySteamRoot()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeSteamRoot(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        try
        {
            return Path.GetFullPath(expanded);
        }
        catch
        {
            return null;
        }
    }
}

internal sealed class SteamCacheLocationResult
{
    public SteamCacheLocationResult(
        bool hasSteamRoot,
        string? steamRootPath,
        string? steamRootSource,
        string? userdataDirectoryName,
        string? appCacheRootPath,
        string? remoteRootPath,
        string? remoteCachePath,
        string? errorMessage)
    {
        HasSteamRoot = hasSteamRoot;
        SteamRootPath = steamRootPath;
        SteamRootSource = steamRootSource;
        UserdataDirectoryName = userdataDirectoryName;
        AppCacheRootPath = appCacheRootPath;
        RemoteRootPath = remoteRootPath;
        RemoteCachePath = remoteCachePath;
        ErrorMessage = errorMessage;
    }

    public bool HasSteamRoot { get; }

    public string? SteamRootPath { get; }

    public string? SteamRootSource { get; }

    public string? UserdataDirectoryName { get; }

    public string? AppCacheRootPath { get; }

    public string? RemoteRootPath { get; }

    public string? RemoteCachePath { get; }

    public string? ErrorMessage { get; }

    public bool HasCacheRoot => string.IsNullOrWhiteSpace(ErrorMessage)
        && !string.IsNullOrWhiteSpace(AppCacheRootPath)
        && !string.IsNullOrWhiteSpace(RemoteRootPath)
        && !string.IsNullOrWhiteSpace(RemoteCachePath);

    public string SteamRootText => string.IsNullOrWhiteSpace(SteamRootPath)
        ? "未定位到 Steam 根目录"
        : $"{SteamRootPath}（来源：{SteamRootSource ?? "未知"}）";

    public string CacheStatusText => string.IsNullOrWhiteSpace(ErrorMessage)
        ? $"已定位到 STS2 本地缓存：{AppCacheRootPath}"
        : ErrorMessage!;
}
