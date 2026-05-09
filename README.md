# 杀戮尖塔2存档同步工具 / Slay the Spire 2 Save Sync Tool

`STS2 官方档 / Modded 档 progress.save 同步工具 made by 良风`

一个基于 `WPF + .NET 8` 的 Windows 小工具，用来手动同步 Slay the Spire 2 官方档和 Modded 档的 `progress.save`。适合遇到“不开 Mod 有进度，开 Mod 后进度不一致”或“想把 Modded 档进度带回普通档”的玩家。

## 下载

- Windows 版下载：<https://github.com/lfyxhappy/STS2-Save-Sync-Tool/releases/latest>
- 当前工具只处理 `progress.save`，不会修改游戏本体、mod、联机协议或官方存档结构。

## English

### What is this?

STS2 Save Sync Tool is a small Windows utility for Slay the Spire 2 players who want to manually sync `progress.save` between the normal save profile and the Modded save profile.

### Download

Download the latest Windows build from the [GitHub Releases page](https://github.com/lfyxhappy/STS2-Save-Sync-Tool/releases/latest). The current release requires the .NET 8 Desktop Runtime.

### Safety

The tool does not auto-sync or guess which side is newer. You choose the direction manually, and the target-side files are backed up before sync.

### Supported files

The current version only handles `progress.save`. It also updates the matching `progress.save.backup`, Steam local cache mirror, and `remotecache.vdf` metadata when syncing.

## 功能

- 自动扫描 `%AppData%\SlayTheSpire2\steam\<steamId>` 下的 `profile1-3`
- 对比普通档和 Modded 档的进度摘要
- 手动选择同步方向，不自动判断、不自动覆盖
- 同步目标侧的 `progress.save` 与 `progress.save.backup`
- 同步 Steam 本地缓存镜像 `remote/.../progress.save`
- 更新 `remotecache.vdf` 对应元数据，降低 Steam 云缓存回写冲突
- 同步前自动备份目标侧旧文件
- 支持手动覆盖 Steam 安装目录，适配非常规路径

## 使用步骤

1. 关闭 Slay the Spire 2 和 Steam 云同步中的相关弹窗。
2. 打开工具，选择你的 Steam 账号和 Profile。
3. 查看普通档 / Modded 档的时间、摘要和角色难度差异。
4. 点击“同步到 Modded”或“同步到普通档”。
5. 同步完成后，先启动游戏确认目标档进度是否正确。

## 安全说明

- 工具会在同步前备份目标侧旧文件。
- 备份目录位于 `%AppData%\SlayTheSpire2\save_sync_tool\backups`。
- 建议关闭游戏后再同步，避免游戏运行中写回旧存档。
- 如果 Steam 安装在非常规目录，可以在工具顶部手动填写 Steam 根目录。

## 构建

```powershell
dotnet build
dotnet publish -c Release
```

## 关键词

Slay the Spire 2, STS2, save sync, progress.save, modded save, Steam Cloud, WPF, .NET
