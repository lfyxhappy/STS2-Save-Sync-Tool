# STS2 Save Sync Tool

`STS2 官方档 / Modded 档进度同步 made by 良风`

一个基于 `WPF + .NET 8` 的 Slay the Spire 2 进度同步小工具，用来手动同步官方档和 modded 档的 `progress.save`。

## 功能

- 扫描 `%AppData%\\SlayTheSpire2\\steam\\<steamId>` 下的 `profile1-3`
- 对比普通档和 modded 档的进度摘要
- 手动选择同步方向，不自动覆盖
- 同步目标侧的：
  - `%AppData%` 下 `progress.save`
  - `%AppData%` 下 `progress.save.backup`
  - Steam 本地缓存镜像 `remote/.../progress.save`
  - `remotecache.vdf` 对应元数据
- 同步前自动备份目标侧旧文件
- 支持手动覆盖 Steam 安装目录，适配非常规路径

## 构建

```powershell
dotnet build
dotnet publish -c Release
```

## 说明

- 这是手动同步工具，不做自动判定“哪边更新”
- 当前只处理 `progress.save`
- 不修改游戏本体、mod、联机协议或官方存档结构
